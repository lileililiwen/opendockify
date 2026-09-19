using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenDockify.AiAssist;
using OpenDockify.Api;
using OpenDockify.Auth;
using OpenDockify.Auth.Services;
using OpenDockify.Data;
using OpenDockify.Data.Audit;
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
using Platform.Auditing.AspNetCore.DependencyInjection;
using Platform.Identity.AspNetCore;
using Platform.Jobs.Hangfire;
using Platform.Jobs.Hangfire.DependencyInjection;
using Platform.Observability.DependencyInjection;
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

SecurityBootstrapValidator.EnsureValid(
    builder.Configuration,
    builder.Environment.EnvironmentName);
WebEdgeBootstrapValidator.EnsureValid(
    builder.Configuration,
    builder.Environment.EnvironmentName);

builder.Services.AddDatabaseModule(builder.Configuration);
builder.Services.AddPlatformMigrator();
builder.Services.AddAuditModule(builder.Configuration);
builder.Services.AddSeed<AuditRetentionJobHandlerSeed>();
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

builder.Services.AddPlatformIdentityLifecycle();

builder.Services.AddHttpClient(OpenDockify.AiAssist.AiAssistClientNames.HttpClient)
    .AddPlatformHttpResilience();

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

builder.Services.AddPlatformObservability(o =>
{
    o.ApplicationName = "OpenDockify";
});

builder.Services.AddPlatformAuditingAspNetCore(options =>
{
    options.Enabled = true;
});

builder.Services.AddPlatformHangfireJobs(o =>
{
    o.Storage = HangfireStorageResolver.Resolve(builder.Configuration);
    if (o.Storage == HangfireStorageKind.PostgreSql)
    {
        o.PostgreSqlConnectionString = builder.Configuration.GetConnectionString("Hangfire")
            ?? builder.Configuration["BackgroundJobs:Hangfire:PostgreSqlConnectionString"]
            ?? builder.Configuration["Jobs:Hangfire:PostgreSqlConnectionString"];
    }
    o.DashboardEnabled = false;
    o.DashboardRoute = "/admin/jobs";
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ProgramTags.Ready)
    .AddCheck<StorageHealthCheck>("storage", tags: ProgramTags.Ready);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    WebEdgeBootstrapValidator.ConfigureForwardedHeaders(
        options, builder.Configuration, builder.Environment.EnvironmentName);
});

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

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapReadinessEndpoints();

app.UseForwardedHeaders();

if (WebEdgeBootstrapValidator.IsHstsEnabled(builder.Configuration, builder.Environment.EnvironmentName))
{
    app.UseHsts();
}

app.UsePlatformWeb();

app.Use(async (context, next) =>
{
    var maintenance = context.RequestServices.GetRequiredService<MaintenanceMode>();
    if (maintenance.IsEnabled
        && !context.Request.Path.StartsWithSegments("/healthz")
        && !context.Request.Path.StartsWithSegments("/readyz")
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

app.UsePlatformWebCors(WebEdgePolicies.CorsPolicyName);

app.UseRateLimiter();
// Audit capture runs before authentication so denied statuses (401/403
// from JWT validation) are recorded as security events; the middleware
// records after the downstream pipeline completes.
app.UsePlatformAuditing();
app.UseAuthentication();
app.UseAuthorization();

app.UsePlatformProblemDetails();

app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapAuditEndpoints();
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

app.MapPlatformEndpoints();
app.MapPlatformOpenApiDocuments();

app.Services
    .RegisterOperationsRecurringJobs(builder.Configuration)
    .RegisterInterviewRecurringJobs()
    .RegisterIntegrationRecurringJobs()
    .RegisterDataRetentionRecurringJob();

await app.Services.MigrateAndSeedAsync(app.Lifetime.ApplicationStopping);

await app.RunAsync();

namespace OpenDockify.Api
{
    public sealed partial class Program;

    /// <summary>
    /// Resolves the Hangfire storage backend from configuration. Reads
    /// <c>Jobs:Hangfire:Storage</c> first (the OpenSpec-documented key),
    /// then the platform-default <c>BackgroundJobs:Hangfire:Storage</c>.
    /// Anything unrecognized falls back to InMemory (the self-hosted
    /// default; Postgres is opt-in).
    /// </summary>
    internal static class HangfireStorageResolver
    {
        internal static HangfireStorageKind Resolve(IConfiguration configuration)
        {
            var raw = configuration["Jobs:Hangfire:Storage"]
                ?? configuration["BackgroundJobs:Hangfire:Storage"];
            return raw?.Trim().ToLowerInvariant() switch
            {
                "postgresql" or "postgres" or "npgsql" => HangfireStorageKind.PostgreSql,
                _ => HangfireStorageKind.InMemory,
            };
        }
    }

    internal static class ProgramTags
    {
        public static readonly string[] Ready = { "ready" };
    }

    /// <summary>
    /// Seeder wrapper that registers the audit-retention recurring job
    /// after the platform-jobs registry is built. Wrapped as an
    /// <see cref="OpenDockify.Data.IDbSeeder"/> so it runs in the existing
    /// startup seeder pipeline.
    /// </summary>
    public sealed class AuditRetentionJobHandlerSeed : OpenDockify.Data.IDbSeeder
    {
        public int Order => 1000;

        public Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            var registry = services.GetRequiredService<Platform.Jobs.IRecurringJobRegistry>();
            registry.Register(Platform.Jobs.RecurringJobAttribute.GetDescriptor(
                typeof(AuditRetentionJobHandler)));
            return Task.CompletedTask;
        }
    }

    public static class DataRecurringJobRegistrationExtensions
    {
        public static IServiceProvider RegisterDataRetentionRecurringJob(this IServiceProvider services)
        {
            // The retention job is registered via the AuditRetentionJobHandlerSeed
            // so it is part of the documented startup pipeline. This method is
            // a no-op extension point kept for symmetry with the other modules.
            return services;
        }
    }
}

