namespace Nefarius.Utilities.TorProxy.DependencyInjection;

/// <summary>
/// Controls the behaviour of <see cref="TorProxyHostedService"/>.
/// </summary>
public sealed class TorProxyHostedServiceOptions
{
    /// <summary>
    /// When <c>true</c> (the default), <see cref="TorProxyHostedService"/> will call
    /// <c>FetchAsync</c> on the <see cref="ITorProxyToolFetcher"/> on application startup before
    /// starting the proxy. Set to <c>false</c> if you manage tool downloads yourself (e.g. in a
    /// deployment pipeline) and want to skip the network round-trip at runtime.
    /// </summary>
    public bool AutoFetchTools { get; set; } = true;
}
