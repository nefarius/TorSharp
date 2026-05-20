using System;
using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace Nefarius.Utilities.TorProxy.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IHttpClientBuilder"/> that route a named
/// <see cref="System.Net.Http.HttpClient"/> through the Tor SOCKS5 proxy.
/// </summary>
public static class TorProxyHttpClientBuilderExtensions
{
    /// <summary>
    /// Configures the primary message handler of the named <see cref="System.Net.Http.HttpClient"/>
    /// to route all requests through the Tor SOCKS5 port configured in
    /// <see cref="TorProxyTorSettings.SocksPort"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="TorProxySettings"/> are resolved from the <see cref="IServiceProvider"/>
    /// at handler-creation time, so <c>SetHandlerLifetime</c> still applies correctly — you do
    /// not need to call <see cref="UseTorSocks5Proxy"/> again after changing ports at runtime.
    /// </remarks>
    /// <param name="builder">The <see cref="IHttpClientBuilder"/> to configure.</param>
    /// <returns>The original <paramref name="builder"/> for chaining.</returns>
    public static IHttpClientBuilder UseTorSocks5Proxy(this IHttpClientBuilder builder)
    {
        return builder.ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var settings = sp.GetRequiredService<TorProxySettings>();
            return new System.Net.Http.SocketsHttpHandler
            {
                Proxy = new WebProxy(new Uri("socks5://localhost:" + settings.TorSettings.SocksPort)),
                UseProxy = true,
            };
        });
    }
}
