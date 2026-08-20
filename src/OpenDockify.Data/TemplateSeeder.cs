using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Templates.Models;
using OpenDockify.Templates.Services;

namespace OpenDockify.Data;

/// <summary>
/// Seeds the 8 built-in templates (read-only, copied by users) when no
/// built-in exists yet. Idempotent: a no-op once any built-in is present, so
/// restarts never duplicate them.
///
/// Lives in OpenDockify.Data (the central reference) because it combines the
/// Templates module's static data with the seed registry — domain modules must
/// not reference OpenDockify.Data, so composite seeders live here.
/// </summary>
public sealed class TemplateSeeder : IDbSeeder
{
    public int Order => 120;

    public async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<AppDbContext>();

        var anyBuiltIn = await db.Set<Template>()
            .AnyAsync(t => t.IsBuiltIn, cancellationToken);
        if (anyBuiltIn)
        {
            return;
        }

        foreach (var data in BuiltInTemplates.All)
        {
            db.Set<Template>().Add(new Template
            {
                Id = Guid.NewGuid(),
                OwnerId = null,
                IsBuiltIn = true,
                IsPublic = true,
                Name = data.Name,
                Category = data.Category,
                Description = data.Description,
                RiskNoticeText = data.RiskNoticeText,
                Body = data.Body,
                DefinitionJson = JsonSerializer.Serialize(data.Definition, TemplateDefinitionValidator.JsonOptions),
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
