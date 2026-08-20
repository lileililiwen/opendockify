using OpenDockify.Data;

var builder = WebApplication.CreateBuilder(args);

// Minimal API shell + composition root. Domain modules register here as
// their changes land; the Data module provides the pluggable database
// foundation (SQLite default; Postgres/MySQL/SQL Server by config).
builder.Services.AddDatabaseModule(builder.Configuration);

var app = builder.Build();

// Health endpoint used by the Docker healthcheck.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// Self-hosters should not need to run `dotnet ef` manually: apply migrations
// and run idempotent seeders at startup.
await app.Services.MigrateAndSeedAsync(app.Lifetime.ApplicationStopping);

await app.RunAsync();
