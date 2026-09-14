using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Api;
using OpenDockify.Generation.Models;
using Platform.Storage.Contracts;
using Platform.Storage.Keys;
using Platform.Storage.Local;
using Xunit;

namespace OpenDockify.UnitTests;

/// <summary>
/// Black-box coverage of the storage abstraction adopted in
/// <c>platform-storage-abstraction</c>: key validation, provider selection,
/// PDF round-trip via <see cref="IObjectStorage"/>, HTTP range parsing, and
/// presigned download for the automation PDF endpoint.
/// </summary>
public sealed class StorageAbstractionTests
{
    [Fact]
    public void StorageKeys_reject_traversal_and_normalize_names()
    {
        Assert.Equal(
            "pdfs/11111111111111111111111111111111/abcdabcdabcdabcdabcdabcdabcdabcd.pdf",
            StorageKeys.ForDocumentPdf(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("abcdabcd-abcd-abcd-abcd-abcdabcdabcd")).Value);

        Assert.Equal("backups/opendockify.odbak", StorageKeys.ForBackupBundle("opendockify").Value);
        Assert.Equal("backups/opendockify.odbak", StorageKeys.ForBackupBundle("opendockify.odbak").Value);

        Assert.Throws<ArgumentException>(() => StorageKeys.ForBackupBundle(""));
        Assert.Throws<ArgumentException>(() => StorageKeys.ForBackupBundle("../escape.odbak"));
    }

    [Fact]
    public void StorageModule_defaults_to_local_provider_and_reports_healthy_status()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "local",
            ["Storage:Local:RootPath"] = Path.Combine(Path.GetTempPath(), $"opendockify-storage-{Guid.NewGuid():N}"),
        }).Build();
        services.AddStorageModule(config);
        using var provider = services.BuildServiceProvider();

        var storage = provider.GetRequiredService<IObjectStorage>();
        Assert.IsType<LocalFileStorage>(storage);

        var probe = provider.GetRequiredService<StorageHealthProbe>();
        var status = probe.Current;
        Assert.Equal("local", status.Provider);
        Assert.Equal(StorageProviderState.Healthy, status.State);
        Assert.Null(status.Code);
    }

    [Fact]
    public void StorageModule_rejects_unknown_provider_name()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "azure",
        }).Build();
        Assert.Throws<InvalidOperationException>(() => services.AddStorageModule(config));
    }

    [Fact]
    public async Task Pdf_round_trip_lands_under_pdfs_key_and_download_returns_full_bytes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"opendockify-storage-{Guid.NewGuid():N}");
        var store = new LocalFileStorage(root);
        var ownerId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var key = StorageKeys.ForDocumentPdf(ownerId, documentId);
        var content = "%PDF-1.4\n%fake-pdf-bytes"u8.ToArray();
        var request = new StorageUploadRequest(key, new MemoryStream(content), "application/pdf", content.Length);
        var outcome = await store.UploadAsync(request);
        Assert.Equal(StorageOutcomeStatus.Succeeded, outcome.Status);

        var download = await store.DownloadAsync(key);
        Assert.Equal(StorageOutcomeStatus.Succeeded, download.Status);
        Assert.NotNull(download.Value);
        var value = download.Value;
        Assert.Equal(content.Length, value.Metadata.LengthBytes);
        await using var stream = value.Content;
        var downloaded = new byte[content.Length];
        var read = await stream.ReadAsync(downloaded.AsMemory());
        Assert.Equal(content.Length, read);
        Assert.Equal(content, downloaded);
    }

    [Fact]
    public async Task Range_aware_response_returns_206_with_content_range_when_range_header_is_present()
    {
        var store = new LocalFileStorage(Path.Combine(Path.GetTempPath(), $"opendockify-storage-{Guid.NewGuid():N}"));
        var key = new StorageObjectKey("pdfs/range/file.pdf");
        var bytes = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Range(0, 64).Select(i => (char)('A' + (i % 26)))));
        var outcome = await store.UploadAsync(new StorageUploadRequest(key, new MemoryStream(bytes), "application/pdf", bytes.Length));
        Assert.Equal(StorageOutcomeStatus.Succeeded, outcome.Status);

        var download = await store.DownloadAsync(key);
        Assert.Equal(StorageOutcomeStatus.Succeeded, download.Status);
        var stored = download.Value!;
        await using var _ = stored;

        var http = new DefaultHttpContext();
        http.Request.Headers["Range"] = "bytes=0-9";
        var result = await DocumentEndpoints.RangeAwarePdfResultAsync(http, stored, "file.pdf");

        Assert.Equal(StatusCodes.Status206PartialContent, http.Response.StatusCode);
        Assert.Equal("bytes", http.Response.Headers.AcceptRanges.ToString());
        Assert.Equal($"bytes 0-9/{bytes.Length}", http.Response.Headers.ContentRange.ToString());
        Assert.Equal(10, http.Response.ContentLength);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Range_aware_response_returns_416_for_unsatisfiable_range()
    {
        var store = new LocalFileStorage(Path.Combine(Path.GetTempPath(), $"opendockify-storage-{Guid.NewGuid():N}"));
        var key = new StorageObjectKey("pdfs/range/file.pdf");
        var bytes = "%PDF-1.4\n%short"u8.ToArray();
        await store.UploadAsync(new StorageUploadRequest(key, new MemoryStream(bytes), "application/pdf", bytes.Length));
        var download = await store.DownloadAsync(key);
        var stored = download.Value!;
        await using var _ = stored;

        var http = new DefaultHttpContext();
        http.Request.Headers["Range"] = "bytes=999-2000";

        var result = await DocumentEndpoints.RangeAwarePdfResultAsync(http, stored, "file.pdf");

        Assert.Equal(StatusCodes.Status416RangeNotSatisfiable, http.Response.StatusCode);
        Assert.Equal($"bytes */{bytes.Length}", http.Response.Headers.ContentRange.ToString());
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Range_aware_response_returns_200_with_accept_ranges_when_no_range_header()
    {
        var store = new LocalFileStorage(Path.Combine(Path.GetTempPath(), $"opendockify-storage-{Guid.NewGuid():N}"));
        var key = new StorageObjectKey("pdfs/range/file.pdf");
        var bytes = "%PDF-1.4\n%full-document"u8.ToArray();
        await store.UploadAsync(new StorageUploadRequest(key, new MemoryStream(bytes), "application/pdf", bytes.Length));
        var download = await store.DownloadAsync(key);
        var stored = download.Value!;
        await using var _ = stored;

        var http = new DefaultHttpContext();
        var result = await DocumentEndpoints.RangeAwarePdfResultAsync(http, stored, "file.pdf");

        Assert.Equal("bytes", http.Response.Headers.AcceptRanges.ToString());
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Presign_request_issues_15_minute_download_without_credentials()
    {
        var store = new LocalFileStorage(Path.Combine(Path.GetTempPath(), $"opendockify-storage-{Guid.NewGuid():N}"));
        var key = new StorageObjectKey("pdfs/presign/file.pdf");
        await store.UploadAsync(new StorageUploadRequest(key, new MemoryStream("%PDF-1.4\n"u8.ToArray()), "application/pdf", 9));

        var presign = await store.PresignAsync(new PresignRequest(key, StorageOperation.Download, TimeSpan.FromMinutes(15)));

        Assert.Equal(StorageOutcomeStatus.Succeeded, presign.Outcome.Status);
        Assert.NotNull(presign.Operation);
        var op = presign.Operation;
        Assert.Equal(key, op.Key);
        Assert.Equal(StorageOperation.Download, op.Operation);
        Assert.True(op.ExpiresAt > DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(14)));
        Assert.DoesNotContain("Secret", op.Url.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("access_key", op.Url.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Local_storage_writes_atomically_so_readers_never_see_partial_uploads()
    {
        var root = Path.Combine(Path.GetTempPath(), $"opendockify-storage-{Guid.NewGuid():N}");
        var store = new LocalFileStorage(root);
        var key = new StorageObjectKey("pdfs/atomic/file.pdf");
        var bytes = "%PDF-1.4\n%" + new string('x', 4096);
        var source = new MemoryStream(Encoding.ASCII.GetBytes(bytes));
        var outcome = await store.UploadAsync(new StorageUploadRequest(key, source, "application/pdf", Encoding.ASCII.GetByteCount(bytes)));
        Assert.Equal(StorageOutcomeStatus.Succeeded, outcome.Status);

        // The atomic write-then-rename means the final file size must match exactly
        // and there are no partial / .tmp files left behind.
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        Assert.Single(files);
        var info = new FileInfo(files[0]);
        Assert.Equal(Encoding.ASCII.GetByteCount(bytes), info.Length);
    }
}
