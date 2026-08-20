using Microsoft.Extensions.DependencyInjection;

namespace OpenDockify.Esign;

/// <summary>
/// RESERVED module registration. E-signature is out of MVP scope: the module
/// deliberately registers NO executable signing implementation — only the
/// schema (entities) and the <c>ISigningOrchestrator</c> interface skeleton
/// exist. Certificate issuance and timestamping are the deployer's
/// responsibility (see <c>ISigningOrchestrator</c>).
/// </summary>
public static class EsignModuleExtensions
{
    public static IServiceCollection AddEsignModule(this IServiceCollection services)
    {
        return services;
    }
}
