using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nefarius.Utilities.TorProxy.DependencyInjection;

/// <summary>
/// Extension methods for registering TorProxy services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class TorProxyServiceCollectionExtensions
{
    /// <summary>
    /// Registers all TorProxy services and adds an <see cref="TorProxyHostedService"/> that
    /// auto-fetches tool binaries and starts the proxy on application startup.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="TorProxySettings"/>. When omitted the
    /// default settings are used.
    /// </param>
    /// <param name="configureHostedService">
    /// Optional delegate to configure <see cref="TorProxyHostedServiceOptions"/>. Use this
    /// to set <see cref="TorProxyHostedServiceOptions.AutoFetchTools"/> to <c>false</c> when
    /// you manage tool downloads yourself.
    /// </param>
    /// <returns>The original <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddTorProxy(
        this IServiceCollection services,
        Action<TorProxySettings>? configure = null,
        Action<TorProxyHostedServiceOptions>? configureHostedService = null)
    {
        // Bind settings through IOptions so they can be reconfigured via appsettings.json
        // or environment variables in addition to the configure delegate.
        var optionsBuilder = services.AddOptions<TorProxySettings>();
        if (configure != null)
        {
            optionsBuilder.Configure(configure);
        }

        // Also expose the concrete type directly for convenience (matches prior usage pattern).
        services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<TorProxySettings>>().Value);

        // Named HttpClient used internally by TorProxyToolFetcher to download tool binaries.
        services.AddHttpClient(TorProxyToolFetcherHttpClientName);

        // The tool fetcher — stateless, safe to share as singleton.
        services.TryAddSingleton<ITorProxyToolFetcher>(sp =>
            new TorProxyToolFetcher(
                sp.GetRequiredService<TorProxySettings>(),
                sp.GetRequiredService<System.Net.Http.IHttpClientFactory>()
                  .CreateClient(TorProxyToolFetcherHttpClientName)));

        // The proxy — stateful, must be a singleton.
        services.TryAddSingleton<ITorProxy>(sp =>
            new TorProxy(
                sp.GetRequiredService<TorProxySettings>(),
                sp.GetRequiredService<ILoggerFactory>()));

        // Hosted-service options.
        var hostedServiceOptionsBuilder = services.AddOptions<TorProxyHostedServiceOptions>();
        if (configureHostedService != null)
        {
            hostedServiceOptionsBuilder.Configure(configureHostedService);
        }

        services.AddHostedService<TorProxyHostedService>();

        return services;
    }

    /// <summary>
    /// Named <see cref="System.Net.Http.HttpClient"/> used by <see cref="TorProxyToolFetcher"/>
    /// to download Tor (and optional Privoxy) binaries. You can configure timeouts, proxies or
    /// handlers for this client via <c>services.AddHttpClient(<see cref="TorProxyToolFetcherHttpClientName"/>)</c>.
    /// </summary>
    public const string TorProxyToolFetcherHttpClientName = "TorProxy.Fetcher";
}
