using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenDockify.Data;

/// <summary>
/// Lets `dotnet ef` build the model without booting the whole host: it uses
/// the SQLite provider by default (matches the runtime default). Migrations
/// are provider-agnostic, so generating them against SQLite is sufficient.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(
                "Data Source=opendockify.db",
                o => o.MigrationsAssembly("OpenDockify.Data"))
            .Options;

        return new AppDbContext(options);
    }
}
