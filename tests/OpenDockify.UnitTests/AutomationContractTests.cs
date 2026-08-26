using System.Text.Json;
using OpenDockify.Api;
using Xunit;

namespace OpenDockify.UnitTests;

/// <summary>
/// The served OpenAPI document and the committed docs/openapi.json are the same
/// embedded bytes; the document covers every automation path (spec:
/// machine-readable API contract).
/// </summary>
public sealed class AutomationContractTests
{
    [Fact]
    public void Served_document_is_byte_identical_to_the_committed_copy()
    {
        var repoRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");
        var committed = File.ReadAllBytes(Path.Combine(repoRoot, "docs", "openapi.json"));

        Assert.Equal(committed, AutomationContract.DocumentBytes);
    }

    [Fact]
    public void Contract_is_openapi_3_1_and_covers_every_automation_path()
    {
        using var document = JsonDocument.Parse(AutomationContract.DocumentBytes);
        var root = document.RootElement;

        Assert.Equal("3.1.0", root.GetProperty("openapi").GetString());

        var paths = root.GetProperty("paths");
        string[] expected =
        [
            "/api/v1/automation/templates",
            "/api/v1/automation/preview",
            "/api/v1/automation/finalize",
            "/api/v1/automation/operations/{id}",
            "/api/v1/automation/documents/{id}",
            "/api/v1/automation/documents/{id}/pdf",
        ];
        foreach (var path in expected)
        {
            Assert.True(paths.TryGetProperty(path, out _), $"missing path {path}");
        }

        // Structured error schema is referenced by error responses.
        Assert.True(root
            .GetProperty("components")
            .GetProperty("schemas")
            .TryGetProperty("Error", out _));

        // Finalize requires the idempotency header.
        var finalize = paths.GetProperty("/api/v1/automation/finalize").GetProperty("post");
        var parameters = finalize.GetProperty("parameters");
        Assert.Contains(parameters.EnumerateArray(), p => p.GetProperty("name").GetString() == "Idempotency-Key");
    }
}
