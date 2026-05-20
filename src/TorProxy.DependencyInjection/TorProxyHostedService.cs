using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Nefarius.Utilities.TorProxy.DependencyInjection;

/// <summary>
/// <see cref="IHostedService"/> that manages the lifetime of the Tor proxy alongside the
/// .NET Generic Host. On <see cref="StartAsync"/> it optionally fetches the required tool
/// binaries and then configures and starts the proxy. On <see cref="StopAsync"/> it stops
/// the proxy gracefully.
/// </summary>
public sealed class TorProxyHostedService : IHostedService
{
    private readonly ITorProxy _proxy;
    private readonly ITorProxyToolFetcher _fetcher;
    private readonly TorProxyHostedServiceOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="TorProxyHostedService"/>.
    /// </summary>
    public TorProxyHostedService(
        ITorProxy proxy,
        ITorProxyToolFetcher fetcher,
        IOptions<TorProxyHostedServiceOptions> options)
    {
        _proxy = proxy;
        _fetcher = fetcher;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.AutoFetchTools)
        {
            await _fetcher.FetchAsync().ConfigureAwait(false);
        }

        await _proxy.ConfigureAndStartAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _proxy.Stop();
        return Task.CompletedTask;
    }
}
