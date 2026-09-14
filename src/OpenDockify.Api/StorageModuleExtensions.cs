using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Storage.Contracts;
using Platform.Storage.Keys;
using Platform.Storage.Local;
using Platform.Storage.S3;

namespace OpenDockify.Api;

/// <summary>
/// Object-storage composition root. Reads <c>Storage:Provider</c> from
/// configuration and registers a single <see cref="IObjectStorage"/>
/// implementation as the default for the whole process. Local atomic
/// storage is the zero-setup default; an S3-compatible endpoint is
/// selectable for deployers who want object storage off-host.
/// </summary>
public static class StorageModuleExtensions
{
    /// <summary>Known provider names. Local is the default.</summary>
    public static class Providers
    {
        public const string Local = "local";
        public const string S3 = "s3";
    }

    public const string SectionName = "Storage";
    public const string LocalSectionName = "Storage:Local";
    public const string S3SectionName = "Storage:S3";
    public const string RootPathKey = "Storage:Local:RootPath";
    public const string DefaultLocalRoot = "/app/data/objects";

    public static IServiceCollection AddStorageModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var provider = (configuration["Storage:Provider"] ?? Providers.Local).Trim().ToLowerInvariant();
        if (provider != Providers.Local && provider != Providers.S3)
        {
            throw new InvalidOperationException(
                $"Unsupported Storage:Provider '{provider}'. Supported: {Providers.Local}, {Providers.S3}.");
        }

        services.RemoveAll<StorageOptions>();
        services.AddSingleton(_ => BuildLimits(configuration));

        if (provider == Providers.S3)
        {
            var s3Options = BuildS3Options(configuration);
            services.RemoveAll<S3StorageOptions>();
            services.AddSingleton(s3Options);
            services.TryAddSingleton<IAmazonS3>(_ =>
            {
                var region = configuration["Storage:S3:Region"] ?? "us-east-1";
                var config = new AmazonS3Config
                {
                    RegionEndpoint = RegionEndpoint.GetBySystemName(region),
                    ForcePathStyle = true,
                };
                var endpoint = configuration["Storage:S3:Endpoint"];
                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    config.ServiceURL = endpoint;
                }
                var accessKey = configuration["Storage:S3:AccessKey"];
                var secretKey = configuration["Storage:S3:SecretKey"];
                if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
                {
                    return new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), config);
                }
                return new AmazonS3Client(config);
            });
            services.AddSingleton<IObjectStorage>(sp => new S3Storage(
                sp.GetRequiredService<IAmazonS3>(),
                sp.GetRequiredService<S3StorageOptions>()));
        }
        else
        {
            var root = configuration[RootPathKey];
            if (string.IsNullOrWhiteSpace(root))
            {
                root = DefaultLocalRoot;
            }
            services.AddSingleton<IObjectStorage>(_ => new LocalFileStorage(root, BuildLimits(configuration)));
        }

        // The provider also exposes safe status (no secrets) for readiness.
        services.TryAddSingleton<StorageHealthProbe>();
        return services;
    }

    private static StorageOptions BuildLimits(IConfiguration configuration)
    {
        var limits = new StorageOptions();
        var maximum = configuration["Storage:MaximumObjectBytes"];
        if (long.TryParse(maximum, out var parsed) && parsed > 0)
        {
            limits = new StorageOptions
            {
                MaximumObjectBytes = parsed,
                MaximumPresignLifetime = limits.MaximumPresignLifetime,
                OperationTimeout = limits.OperationTimeout,
            };
        }
        var presign = configuration["Storage:MaximumPresignLifetimeMinutes"];
        if (int.TryParse(presign, out var minutes) && minutes > 0)
        {
            limits = new StorageOptions
            {
                MaximumObjectBytes = limits.MaximumObjectBytes,
                MaximumPresignLifetime = TimeSpan.FromMinutes(minutes),
                OperationTimeout = limits.OperationTimeout,
            };
        }
        limits.Validate();
        return limits;
    }

    private static S3StorageOptions BuildS3Options(IConfiguration configuration)
    {
        var bucket = configuration["Storage:S3:BucketName"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(bucket))
        {
            throw new InvalidOperationException(
                "Storage:S3:BucketName is required when Storage:Provider=s3.");
        }
        var prefix = configuration["Storage:S3:KeyPrefix"] ?? string.Empty;
        var options = new S3StorageOptions
        {
            BucketName = bucket,
            KeyPrefix = prefix,
            Limits = BuildLimits(configuration),
        };
        options.Validate();
        return options;
    }
}

/// <summary>
/// Resolves a safe <see cref="StorageProviderStatus"/> snapshot for the
/// currently registered storage provider. Exposed for the operations
/// readiness endpoint and must never surface credentials or paths.
/// </summary>
public sealed class StorageHealthProbe(IObjectStorage storage)
{
    public StorageProviderStatus Current
    {
        get
        {
            if (storage is IStorageProviderStatus status)
            {
                return status.Status;
            }
            return new StorageProviderStatus("unknown", StorageProviderState.Healthy);
        }
    }
}

/// <summary>
/// Helpers that mint storage keys with the project convention while
/// staying inside the platform's <see cref="StorageObjectKey"/> invariants
/// (no traversal, no empty segments, no control characters).
/// </summary>
public static class StorageKeys
{
    /// <summary>Document PDF key under the per-user namespace.</summary>
    public static StorageObjectKey ForDocumentPdf(Guid ownerId, Guid documentId)
    {
        return new StorageObjectKey($"pdfs/{ownerId:N}/{documentId:N}.pdf");
    }

    /// <summary>Backup bundle key (deployer-provided file name, normalised).</summary>
    public static StorageObjectKey ForBackupBundle(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Backup name is required.", nameof(name));
        var trimmed = name.EndsWith(".odbak", StringComparison.OrdinalIgnoreCase) ? name : name + ".odbak";
        return new StorageObjectKey($"backups/{trimmed}");
    }
}
