using Microsoft.EntityFrameworkCore;
using OpenDockify.Auth.Services;
using OpenDockify.Templates.Models;

namespace OpenDockify.Templates.Services;

public enum TemplateErrorKind
{
    None,
    Validation,
    Forbidden,
    NotFound,
    TooLarge,
}

public sealed record TemplateResult(Template? Template, TemplateErrorKind Error, string? ErrorMessage)
{
    public static TemplateResult Success(Template template)
    {
        return new(template, TemplateErrorKind.None, null);
    }

    public static TemplateResult Failure(TemplateErrorKind kind, string error)
    {
        return new(null, kind, error);
    }
}

/// <summary>Payload for creating or updating a template (definition as raw JSON).</summary>
public sealed record TemplateDraft(
    string Name,
    string Category,
    string Description,
    string RiskNoticeText,
    string Body,
    string DefinitionJson);

/// <summary>
/// Template CRUD, copy, marketplace, and admin global-template upsert, with
/// multi-user isolation: users see only public templates and their own private
/// templates, and built-ins are immutable (copy to edit).
/// </summary>
public sealed class TemplateService(DbContext db)
{
    public async Task<IReadOnlyList<Template>> GetMarketplaceAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var templates = await db.Set<Template>()
            .Where(t => t.IsBuiltIn || t.IsPublic || t.OwnerId == userId)
            .OrderBy(t => t.IsBuiltIn ? 0 : 1)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return templates;
    }

    public async Task<OwnedResourceResult<Template>> GetByIdAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var template = await db.Set<Template>()
            .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (template is null || (!template.IsPublic && !template.IsBuiltIn && template.OwnerId != userId))
        {
            return OwnedResourceResult.Missing<Template>();
        }

        return OwnedResourceResult.Found(template);
    }

    public async Task<TemplateResult> CreateAsync(
        Guid userId,
        TemplateDraft draft,
        CancellationToken cancellationToken)
    {
        var validation = ValidateDraft(draft);
        if (validation.Error is not null)
        {
            return TemplateResult.Failure(validation.Kind, validation.Error);
        }

        var template = new Template
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            IsBuiltIn = false,
            IsPublic = false,
            Name = draft.Name.Trim(),
            Category = draft.Category.Trim(),
            Description = draft.Description?.Trim() ?? string.Empty,
            RiskNoticeText = draft.RiskNoticeText?.Trim() ?? string.Empty,
            Body = draft.Body,
            DefinitionJson = draft.DefinitionJson,
        };

        db.Set<Template>().Add(template);
        await db.SaveChangesAsync(cancellationToken);

        return TemplateResult.Success(template);
    }

    public async Task<TemplateResult> UpdateAsync(
        Guid userId,
        Guid id,
        TemplateDraft draft,
        CancellationToken cancellationToken)
    {
        var template = await db.Set<Template>()
            .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (template is null)
        {
            return TemplateResult.Failure(TemplateErrorKind.NotFound, "Template not found.");
        }

        if (template.IsBuiltIn)
        {
            return TemplateResult.Failure(TemplateErrorKind.Forbidden, "Built-in templates cannot be modified. Copy them to create an editable version.");
        }

        if (template.OwnerId != userId)
        {
            return TemplateResult.Failure(TemplateErrorKind.NotFound, "Template not found.");
        }

        var validation = ValidateDraft(draft);
        if (validation.Error is not null)
        {
            return TemplateResult.Failure(validation.Kind, validation.Error);
        }

        template.Name = draft.Name.Trim();
        template.Category = draft.Category.Trim();
        template.Description = draft.Description?.Trim() ?? string.Empty;
        template.RiskNoticeText = draft.RiskNoticeText?.Trim() ?? string.Empty;
        template.Body = draft.Body;
        template.DefinitionJson = draft.DefinitionJson;
        template.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return TemplateResult.Success(template);
    }

    public async Task<TemplateResult> DeleteAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var template = await db.Set<Template>()
            .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (template is null)
        {
            return TemplateResult.Failure(TemplateErrorKind.NotFound, "Template not found.");
        }

        if (template.IsBuiltIn)
        {
            return TemplateResult.Failure(TemplateErrorKind.Forbidden, "Built-in templates cannot be deleted.");
        }

        if (template.OwnerId != userId)
        {
            return TemplateResult.Failure(TemplateErrorKind.NotFound, "Template not found.");
        }

        db.Set<Template>().Remove(template);
        await db.SaveChangesAsync(cancellationToken);

        return TemplateResult.Success(template);
    }

    /// <summary>
    /// Deep-clones any accessible template (built-in, public, or own) into a
    /// private copy owned by the caller; the original is untouched.
    /// </summary>
    public async Task<TemplateResult> CopyAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var template = await db.Set<Template>()
            .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (template is null || (!template.IsPublic && !template.IsBuiltIn && template.OwnerId != userId))
        {
            return TemplateResult.Failure(TemplateErrorKind.NotFound, "Template not found.");
        }

        var validation = TemplateDefinitionValidator.Validate(template.DefinitionJson, template.Body);
        if (validation.Error is not null)
        {
            var kind = validation.Kind == DefinitionErrorKind.TooLarge
                ? TemplateErrorKind.TooLarge
                : TemplateErrorKind.Validation;
            return TemplateResult.Failure(kind, validation.Error);
        }

        var copy = new Template
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            IsBuiltIn = false,
            IsPublic = false,
            Name = $"{template.Name}（副本）",
            Category = template.Category,
            Description = template.Description,
            RiskNoticeText = template.RiskNoticeText,
            Body = template.Body,
            DefinitionJson = template.DefinitionJson,
        };

        db.Set<Template>().Add(copy);
        await db.SaveChangesAsync(cancellationToken);

        return TemplateResult.Success(copy);
    }

    /// <summary>
    /// Creates or updates a public global template (admin-only). Built-ins are
    /// immutable even for admins.
    /// </summary>
    public async Task<TemplateResult> AdminUpsertAsync(
        Guid? id,
        TemplateDraft draft,
        CancellationToken cancellationToken)
    {
        var validation = ValidateDraft(draft);
        if (validation.Error is not null)
        {
            return TemplateResult.Failure(validation.Kind, validation.Error);
        }

        Template template;
        if (id is null)
        {
            template = new Template
            {
                Id = Guid.NewGuid(),
                OwnerId = null,
                IsBuiltIn = false,
                IsPublic = true,
            };
            db.Set<Template>().Add(template);
        }
        else
        {
            var existing = await db.Set<Template>()
                .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

            if (existing is null)
            {
                return TemplateResult.Failure(TemplateErrorKind.NotFound, "Template not found.");
            }

            if (existing.IsBuiltIn)
            {
                return TemplateResult.Failure(TemplateErrorKind.Forbidden, "Built-in templates cannot be modified.");
            }

            template = existing;
            template.IsPublic = true;
            template.OwnerId = null;
        }

        template.Name = draft.Name.Trim();
        template.Category = draft.Category.Trim();
        template.Description = draft.Description?.Trim() ?? string.Empty;
        template.RiskNoticeText = draft.RiskNoticeText?.Trim() ?? string.Empty;
        template.Body = draft.Body;
        template.DefinitionJson = draft.DefinitionJson;
        template.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return TemplateResult.Success(template);
    }

    private static (TemplateErrorKind Kind, string? Error) ValidateDraft(TemplateDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            return (TemplateErrorKind.Validation, "Template name is required.");
        }

        if (draft.Name.Length > 200)
        {
            return (TemplateErrorKind.Validation, "Template name is too long (max 200 characters).");
        }

        if (string.IsNullOrWhiteSpace(draft.Category))
        {
            return (TemplateErrorKind.Validation, "Template category is required.");
        }

        if (string.IsNullOrWhiteSpace(draft.Body))
        {
            return (TemplateErrorKind.Validation, "Template body is required.");
        }

        var definitionValidation = TemplateDefinitionValidator.Validate(draft.DefinitionJson, draft.Body);
        if (definitionValidation.Error is not null)
        {
            var kind = definitionValidation.Kind == DefinitionErrorKind.TooLarge
                ? TemplateErrorKind.TooLarge
                : TemplateErrorKind.Validation;
            return (kind, definitionValidation.Error);
        }

        return (TemplateErrorKind.None, null);
    }
}
