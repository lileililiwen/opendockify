using OpenDockify.Integrations.Services;

namespace OpenDockify.Api;

/// <summary>
/// Machine-readable contract for the automation API. The committed
/// <c>docs/openapi.json</c> and the served document are the same embedded
/// bytes, so they cannot drift (enforced by a test).
/// </summary>
public static class AutomationContract
{
    private const string _resourceName = "OpenDockify.Api.OpenApi.automation-v1.json";

    private static readonly Lazy<byte[]> _documentBytes = new(LoadDocumentBytes);

    public static byte[] DocumentBytes => _documentBytes.Value;

    private static byte[] LoadDocumentBytes()
    {
        var assembly = typeof(AutomationContract).Assembly;
        using var stream = assembly.GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException($"Embedded OpenAPI resource '{_resourceName}' is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
