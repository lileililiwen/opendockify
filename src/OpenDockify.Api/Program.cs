using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenDockify.Api;
using OpenDockify.Auth;
using OpenDockify.Data;
using OpenDockify.SystemConfig;

var builder = WebApplication.CreateBuilder(args);

// Minimal API shell + composition root. Domain modules register here as
// their changes land; the Data module provides the pluggable database
// foundation (SQLite default; Postgres/MySQL/SQL Server by config).
builder.Services.AddDatabaseModule(builder.Configuration);
builder.Services.AddAuthModule();
builder.Services.AddSeed<AdminSeeder>();
builder.Services.AddSystemConfigModule();
builder.Services.AddSeed<SettingsSeeder>();

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

var app = builder.Build();

// Health endpoint used by the Docker healthcheck.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapSystemConfigEndpoints();

// Self-hosters should not need to run `dotnet ef` manually: apply migrations
// and run idempotent seeders at startup.
await app.Services.MigrateAndSeedAsync(app.Lifetime.ApplicationStopping);

await app.RunAsync();
