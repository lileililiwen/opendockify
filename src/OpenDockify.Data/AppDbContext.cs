using Microsoft.EntityFrameworkCore;

namespace OpenDockify.Data;

/// <summary>
/// Central EF Core DbContext for the OpenDockify modular monolith.
/// Each domain module contributes entities through its own
/// <c>IEntityTypeConfiguration&lt;T&gt;</c>; when a module lands, its assembly
/// gets ONE new <c>ApplyConfigurationsFromAssembly</c> line below (plus its
/// DbSet), never any other edit to this file.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
