using Microsoft.Extensions.DependencyInjection;

namespace OpenDockify.Data;

/// <summary>
/// Runs at startup after EF migrations. Modules register seeders via
/// <c>services.AddSeed&lt;TSeeder&gt;()</c>; each seeder MUST be idempotent —
/// a no-op when its data already exists (see Agents.md §4.2).
/// </summary>
public interface ISeedRegistry
{
    IReadOnlyList<IDbSeeder> Seeders { get; }
}

public sealed class SeedRegistry : ISeedRegistry
{
    public IReadOnlyList<IDbSeeder> Seeders { get; }

    public SeedRegistry(IEnumerable<IDbSeeder> seeders)
    {
        Seeders = seeders.OrderBy(s => s.Order).ToArray();
    }
}

/// <summary>
/// A single idempotent seed routine contributed by a domain module.
/// </summary>
public interface IDbSeeder
{
    int Order { get; }

    Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken);
}

public static class SeedServiceCollectionExtensions
{
    public static IServiceCollection AddSeed<TSeeder>(this IServiceCollection services)
        where TSeeder : class, IDbSeeder
    {
        services.AddSingleton<IDbSeeder, TSeeder>();
        return services;
    }
}
