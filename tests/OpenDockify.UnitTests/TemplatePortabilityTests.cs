using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenDockify.Data;
using OpenDockify.Templates.Models;
using OpenDockify.Templates.Services;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class TemplatePortabilityTests
{
    [Fact]
    public void Canonicalization_sorts_nested_object_properties_and_preserves_arrays()
    {
        using var first = JsonDocument.Parse("""{"z":1,"definition":{"b":2,"a":1},"items":[{"y":2,"x":1}]}""");
        using var second = JsonDocument.Parse("""{"items":[{"x":1,"y":2}],"definition":{"a":1,"b":2},"z":1}""");

        var left = TemplatePackageService.Canonicalize(first.RootElement).GetRawText();
        var right = TemplatePackageService.Canonicalize(second.RootElement).GetRawText();

        Assert.Equal(right, left);
        Assert.Equal("""{"definition":{"a":1,"b":2},"items":[{"x":1,"y":2}],"z":1}""", left);
    }

    [Fact]
    public void Canonicalization_rejects_excessive_json_depth_before_package_processing()
    {
        var json = new string('[', 40) + "0" + new string(']', 40);
        Assert.ThrowsAny<JsonException>(() => JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 }));
    }

    [Fact]
    public async Task Validation_returns_structured_error_for_missing_required_objects()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new TemplatePackageService(db, new TemplateService(db));

        var result = await service.ValidateAsync("{\"formatVersion\":1}"u8.ToArray(), default);

        Assert.False(result.Valid);
        Assert.NotNull(result.Error);
    }
}

public sealed class TemplatePackageLifecycleTests
{
    private static readonly Guid _owner = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Export_import_revision_rollback_and_isolation_preserve_history_atomically()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var templates = new TemplateService(db);
        var packages = new TemplatePackageService(db, templates);
        var draft = new TemplateDraft("Portable", "legal", "v1", "review", "Hello", "{\"fields\":[],\"clauses\":[]}");

        var created = (await templates.CreateAsync(_owner, draft, default)).Template!;
        var first = (await packages.ExportAsync(_owner, created.Id, default)).Bytes!;
        var second = (await packages.ExportAsync(_owner, created.Id, default)).Bytes!;
        Assert.Equal(first, second);
        Assert.False((await templates.GetByIdAsync(_owner, created.Id, default)).NotFound);
        Assert.True((await templates.GetByIdAsync(Guid.NewGuid(), created.Id, default)).NotFound);

        var validation = await packages.ValidateAsync(first, default);
        Assert.True(validation.Valid);
        Assert.True(validation.Conflict);
        var beforeReject = await db.Set<Template>().CountAsync();
        var rejected = await packages.ImportAsync(_owner, first, validation.Receipt!, "reject", default);
        Assert.True(rejected.Conflict);
        Assert.Equal(beforeReject, await db.Set<Template>().CountAsync());

        validation = await packages.ValidateAsync(first, default);
        var copy = await packages.ImportAsync(_owner, first, validation.Receipt!, "create-copy", default);
        Assert.NotNull(copy.Template);
        Assert.Equal(created.StableId, copy.Template.SourceStableId);
        Assert.Equal(created.StableId, (await db.Set<TemplateRevision>().SingleAsync(x => x.TemplateId == copy.Template.Id)).SourceStableId);

        await templates.UpdateAsync(_owner, created.Id, draft with { Name = "Portable v2" }, default);
        var rollback = await templates.RollbackAsync(_owner, created.Id, 1, default);
        Assert.Equal(3, rollback.Template!.CurrentRevision);
        Assert.Equal("Portable", rollback.Template.Name);
        Assert.Equal(3, await db.Set<TemplateRevision>().CountAsync(x => x.TemplateId == created.Id));

        var tampered = first.ToArray();
        tampered[^2] ^= 1;
        Assert.False((await packages.ValidateAsync(tampered, default)).Valid);
    }
}
