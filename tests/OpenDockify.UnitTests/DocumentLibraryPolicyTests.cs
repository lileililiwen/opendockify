using OpenDockify.Generation.Services;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class DocumentLibraryPolicyTests
{
    [Fact]
    public void Query_trims_search_and_parses_filter_and_sort()
    {
        var success = DocumentLibraryQuery.TryCreate(
            "  Loan  ",
            "archived",
            "title",
            out var query,
            out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("Loan", query.Search);
        Assert.Equal(DocumentArchiveFilter.Archived, query.Archive);
        Assert.Equal(DocumentLibrarySort.Title, query.Sort);
    }

    [Theory]
    [InlineData("unknown", "newest")]
    [InlineData("active", "unknown")]
    public void Query_rejects_unknown_options(string archive, string sort)
    {
        Assert.False(DocumentLibraryQuery.TryCreate(null, archive, sort, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Query_rejects_search_over_100_characters()
    {
        Assert.False(DocumentLibraryQuery.TryCreate(new string('x', 101), null, null, out _, out var error));
        Assert.Contains("100", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Metadata_rejects_blank_title(string title)
    {
        var result = DocumentLibraryPolicy.ValidateTitle(title);

        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Metadata_trims_valid_title_and_rejects_oversized_title()
    {
        Assert.Equal("Loan agreement", DocumentLibraryPolicy.ValidateTitle("  Loan agreement  ").Title);
        Assert.NotNull(DocumentLibraryPolicy.ValidateTitle(new string('x', 201)).Error);
    }

    [Fact]
    public void Empty_stored_title_falls_back_to_template_name()
    {
        Assert.Equal("Loan IOU", DocumentLibraryPolicy.ResolveTitle(string.Empty, "Loan IOU"));
        Assert.Equal("Custom title", DocumentLibraryPolicy.ResolveTitle("Custom title", "Loan IOU"));
    }

    [Fact]
    public void Version_graph_returns_connected_branch_oldest_first()
    {
        var root = Guid.NewGuid();
        var child = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var unrelated = Guid.NewGuid();
        var links = new[]
        {
            new DocumentVersionLink(child, root, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
            new DocumentVersionLink(unrelated, null, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            new DocumentVersionLink(branch, root, new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)),
            new DocumentVersionLink(root, null, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
        };

        var connected = DocumentLibraryPolicy.FindConnectedVersions(links, child);

        Assert.Equal(new[] { root, child, branch }, connected.Select(item => item.Id));
    }
}
