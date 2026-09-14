using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenDockify.AiAssist;
using OpenDockify.Api;
using OpenDockify.Auth;
using OpenDockify.Auth.Services;
using OpenDockify.Data;
using OpenDockify.Esign;
using OpenDockify.Finance;
using OpenDockify.Generation;
using OpenDockify.Integrations;
using OpenDockify.Interviews;
using OpenDockify.Operations;
using OpenDockify.Operations.Services;
using OpenDockify.Rendering;
using OpenDockify.Sharing;
using OpenDockify.SystemConfig;
using OpenDockify.Templates;
using Platform.AspNetCore.DependencyInjection;
using Platform.AspNetCore.Errors;
using Platform.Identity.AspNetCore;
using Platform.Web;
using Platform.Web.Cors;
using Platform.Web.Cors.DependencyInjection;
using Platform.Web.DependencyInjection;
using Platform.Web.OpenApi;
using Platform.Web.OpenApi.DependencyInjection;
using Platform.Web.Resilience;
using Platform.Web.Resilience.DependencyInjection;
using Platform.Web.Telemetry;
using Platform.Web.Versioning;
using Platform.Web.Versioning.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Fail before migrations or HTTP startup when a production deployment still
// has missing or development-only security credentials, identical secrets, or
// a remote HTTP AI endpoint that bypasses the HTTPS-only posture.
SecurityBootstrapValidator.EnsureValid(
    builder.Configuration,
    builder.Environment.EnvironmentName);
WebEdgeBootstrapValidator.EnsureValid(
    builder.Configuration,
    builder.Environment.EnvironmentName);

// Minimal API shell + composition root. Domain modules register here as
// their changes land; the Data module provides the pluggable database
// foundation (SQLite default; Postgres/MySQL/SQL Server by config).
builder.Services.AddDatabaseModule(builder.Configuration);
builder.Services.AddAuthModule();
builder.Services.AddSeed<AdminSeeder>();
builder.Services.AddSystemConfigModule();
builder.Services.AddSeed<SettingsSeeder>();
builder.Services.AddTemplatesModule();
builder.Services.AddSeed<TemplateSeeder>();
builder.Services.AddFinanceModule();
builder.Services.AddRenderingModule();
builder.Services.AddGenerationModule();
builder.Services.AddSharingModule();
builder.Services.AddInterviewsModule();
builder.Services.AddOperationsModule(builder.Configuration);
builder.Services.AddIntegrationsModule(builder.Configuration);
builder.Services.AddAiAssistModule();
builder.Services.AddEsignModule();
builder.Services.AddStorageModule(builder.Configuration);

// Platform identity-lifecycle composition: wires the registered refresh /
// recovery / 2FA stores into the platform coordinator.
builder.Services.AddPlatformIdentityLifecycle();

// Apply bounded retry/timeout/circuit-breaker to the AI assist client. The
// platform owns the policy; the AI module just declares the client name.
builder.Services.AddHttpClient(OpenDockify.AiAssist.AiAssistClientNames.HttpClient)
    .AddPlatformHttpResilience();

// Platform edge pipeline: correlation, problem-details, CORS deny-default,
// HTTP resilience handler, API versioning, OpenAPI document registry, and
// redaction-safe telemetry names. Services are registered here; the matching
// middleware runs further below in the documented order.
builder.Services.AddPlatformWeb(web =>
{
    web.EnableSecurityHeaders = true;
    web.MaxRequestBodyBytes = 25 * 1024 * 1024;
    web.RequestTimeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddPlatformWebCors(
    builder.Environment,
    cors =>
    {
        cors.Policies.Clear();
        cors.Policies.Add(new PlatformWebCorsPolicyOptions
        {
            Name = WebEdgePolicies.CorsPolicyName,
            AllowedOrigins = WebEdgeBootstrapValidator.ResolveAllowedOrigins(
                builder.Configuration, builder.Environment.EnvironmentName),
            AllowedMethods = new List<string> { "GET", "HEAD", "POST", "PUT", "DELETE", "OPTIONS" },
            AllowedHeaders = new List<string>
            {
                "Authorization",
                "Content-Type",
                "Idempotency-Key",
                "X-Api-Version",
                "X-Correlation-Id",
            },
            ExposedHeaders = new List<string> { "X-Correlation-Id" },
            AllowCredentials = false,
            PreflightMaxAge = TimeSpan.FromMinutes(5),
        });
    });
builder.Services.AddPlatformHttpResilience(resilience =>
    resilience.IdempotentMethods.Add("POST"));
builder.Services.AddPlatformWebVersioning(versioning =>
    versioning.AssumeDefaultVersionWhenUnspecified = true);
builder.Services.AddPlatformWebOpenApi();
builder.Services.AddPlatformOpenApiDocument(new PlatformWebOpenApiDocumentOptions
{
    Name = "v1",
    Path = "/openapi/v1.json",
    Title = "OpenDockify",
    OpenApiVersion = "3.0.3",
});

// The platform /health endpoint requires a health-checks registration even
// when the deployer does not want readiness probes. Add a single empty
// registration so MapPlatformEndpoints can map the route.
builder.Services.AddHealthChecks();

// Forwarded headers (X-Forwarded-For/Proto) so IP rate limits work behind a
// trusted reverse proxy. Defaults to loopback only; production deployers
// must explicitly list trusted proxies/networks.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    WebEdgeBootstrapValidator.ConfigureForwardedHeaders(
        options, builder.Configuration, builder.Environment.EnvironmentName);
});

// JWT bearer auth: validate issuer/audience/lifetime and the HMAC signature
// using Jwt:Secret. Startup validation of the secret lives in
// JwtTokenService (Auth module).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? string.Empty)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireRole("Administrator"));
});

var loginAttemptsPerMinute = AuthSecurityOptions.GetLoginAttemptsPerMinute(builder.Configuration);
var registrationAttemptsPerHour = AuthSecurityOptions.GetRegistrationAttemptsPerHour(builder.Configuration);
var recoveryAttemptsPerHour = AuthSecurityOptions.GetRecoveryAttemptsPerHour(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.HttpContext.Request.Path.StartsWithSegments("/api/v1/automation"))
        {
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { error = new { code = "rate_limited", message = "Too many automation requests. Slow down and retry later.", fields = (object?)null } },
                cancellationToken);
            return;
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many authentication attempts. Try again later." },
            cancellationToken);
    };
    options.AddPolicy(AuthSecurityOptions.LoginPolicyName, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            WebEdgeBootstrapResolver.ClientPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginAttemptsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    options.AddPolicy(AuthSecurityOptions.RegistrationPolicyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                WebEdgeBootstrapResolver.ClientPartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = registrationAttemptsPerHour,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));
    options.AddPolicy(AuthSecurityOptions.RecoveryPolicyName, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            WebEdgeBootstrapResolver.ClientPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = recoveryAttemptsPerHour,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    options.AddPolicy("public-shares", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            WebEdgeBootstrapResolver.ClientPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    // Automation API: bounded per token (falls back to client IP pre-auth).
    options.AddPolicy(AutomationEndpoints.RateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst("token_id")?.Value
            ?? WebEdgeBootstrapResolver.ClientPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

var app = builder.Build();

// Health endpoints used by the Docker healthcheck and orchestration probes.
// The hand-rolled /healthz shape is what the Flutter client calls; the
// platform /health endpoint is mapped further down alongside OpenAPI.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// Forwarded headers must run before any IP-based logic (rate limits, audit
// keys, lockout windows) so that deployers behind a reverse proxy see the
// real client IP rather than the proxy address.
app.UseForwardedHeaders();

if (WebEdgeBootstrapValidator.IsHstsEnabled(builder.Configuration, builder.Environment.EnvironmentName))
{
    app.UseHsts();
}

// Correlation: read or generate X-Correlation-Id and echo it on the response.
// Security headers: X-Content-Type-Options, X-Frame-Options, Referrer-Policy,
// Content-Security-Policy: frame-ancestors 'none'. The platform runs
// correlation first so error responses can carry the id.
app.UsePlatformWeb();

// Maintenance gate: must precede auth so anonymous callers see the same
// 503 as authenticated ones during an outage.
app.Use(async (context, next) =>
{
    var maintenance = context.RequestServices.GetRequiredService<MaintenanceMode>();
    if (maintenance.IsEnabled
        && !context.Request.Path.StartsWithSegments("/healthz")
        && !context.Request.Path.StartsWithSegments("/health")
        && !context.Request.Path.StartsWithSegments("/api/admin/operations"))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "60";
        await context.Response.WriteAsJsonAsync(new { error = "Maintenance in progress." });
        return;
    }
    await next();
});

// CORS: must run after routing (minimal API inserts it automatically) and
// before authentication so preflight requests do not trigger auth.
app.UsePlatformWebCors(WebEdgePolicies.CorsPolicyName);

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ProblemDetails: unhandled exceptions become a sanitized 500 with a stable
// `code` extension; PlatformProblemException surfaces the supplied error.
app.UsePlatformProblemDetails();

app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapSystemConfigEndpoints();
app.MapTemplateEndpoints();
app.MapAdminTemplateEndpoints();
app.MapDocumentEndpoints();
app.MapSharingEndpoints();
app.MapInterviewEndpoints();
app.MapAiAssistEndpoints();
app.MapAdminAiUsageEndpoints();
app.MapOperationsEndpoints();
app.MapAutomationEndpoints();
app.MapIntegrationManagementEndpoints();

// Platform health + OpenAPI document endpoints.
app.MapPlatformEndpoints();
app.MapPlatformOpenApiDocuments();

// Self-hosters should not need to run `dotnet ef` manually: apply migrations
// and run idempotent seeders at startup.
await app.Services.MigrateAndSeedAsync(app.Lifetime.ApplicationStopping);

await app.RunAsync();
