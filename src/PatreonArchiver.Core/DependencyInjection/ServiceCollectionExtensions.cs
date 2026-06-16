using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PatreonArchiver.Core.Abstractions;
using PatreonArchiver.Core.Configuration;
using PatreonArchiver.Core.Cookies;
using PatreonArchiver.Core.Downloading;
using PatreonArchiver.Core.Parsing;
using PatreonArchiver.Core.Persistence;
using PatreonArchiver.Core.Publishing;
using PatreonArchiver.Core.Resolving;
using PatreonArchiver.Core.Storage;
using PatreonArchiver.Core.Sync;

namespace PatreonArchiver.Core.DependencyInjection;

/// <summary>Composition root for the Core services. The single entry point a host wires up.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPatreonArchiverCore(
        this IServiceCollection services, Action<CoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        services.Configure(configure);

        // TryAdd lets a host pre-register its own implementations (e.g. a test clock).
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<SqliteConnectionFactory>();
        services.TryAddSingleton<IArchiveRepository, SqliteArchiveRepository>();
        services.TryAddSingleton<IPostPageParser, AngleSharpPostPageParser>();
        services.TryAddSingleton<IMhtmlExtractor, MimeMhtmlExtractor>();

        services.AddHttpClient<IUrlResolver, CloudflareStreamResolver>(client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; PatreonArchiver)"));

        services.TryAddSingleton<IToolLocator, ToolLocator>();
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();
        services.TryAddSingleton<IPublisher, AtomicPublisher>();
        services.TryAddSingleton<IDiskSpaceGuard, DriveDiskSpaceGuard>();
        services.TryAddSingleton<IDownloadEngine, YtDlpDownloadEngine>();

        services.TryAddSingleton<ICookieExporter, NetscapeCookieExporter>();
        services.TryAddSingleton<IBatchThrottle, RandomBatchThrottle>();
        services.TryAddSingleton<ISyncOrchestrator, SyncOrchestrator>();

        return services;
    }
}
