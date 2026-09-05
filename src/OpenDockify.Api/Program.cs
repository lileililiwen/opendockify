using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
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

var builder = WebApplication.CreateBuilder(args);

// Fail before migrations or HTTP startup when a production deployment still
// has missing or development-only security credentials.
SecurityBootstrapValidator.EnsureValid(
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
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginAttemptsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    options.AddPolicy(AuthSecurityOptions.RegistrationPolicyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = registrationAttemptsPerHour,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));
    options.AddPolicy("public-shares", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
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
            ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

var app = builder.Build();

// Health endpoint used by the Docker healthcheck.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var maintenance = context.RequestServices.GetRequiredService<MaintenanceMode>();
    if (maintenance.IsEnabled
        && !context.Request.Path.StartsWithSegments("/healthz")
        && !context.Request.Path.StartsWithSegments("/api/admin/operations"))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "60";
        await context.Response.WriteAsJsonAsync(new { error = "Maintenance in progress." });
        return;
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();

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

// Self-hosters should not need to run `dotnet ef` manually: apply migrations
// and run idempotent seeders at startup.
await app.Services.MigrateAndSeedAsync(app.Lifetime.ApplicationStopping);

await app.RunAsync();
