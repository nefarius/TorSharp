using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Nefarius.Utilities.TorProxy;
using Nefarius.Utilities.TorProxy.DependencyInjection;
using Xunit;

namespace Nefarius.Utilities.TorProxy.DependencyInjection.Tests;

public class TorSocks5ProxyHandlerTests
{
    [Fact]
    public void UseTorSocks5Proxy_PrimaryHandlerHasCorrectProxyUri()
    {
        const int socksPort = 19050;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTorProxy(o =>
        {
            o.WriteToConsole = false;
            o.TorSettings.SocksPort = socksPort;
        });
        services.AddHttpClient("TestClient")
                .UseTorSocks5Proxy();

        var sp = services.BuildServiceProvider();

        // IHttpMessageHandlerFactory lets us inspect the handler chain.
        var handlerFactory = sp.GetRequiredService<IHttpMessageHandlerFactory>();
        var handler = handlerFactory.CreateHandler("TestClient");

        // Walk the DelegatingHandler chain to find the innermost SocketsHttpHandler.
        HttpMessageHandler? inner = handler;
        while (inner is DelegatingHandler dh)
        {
            inner = dh.InnerHandler;
        }

        var socketsHandler = Assert.IsType<SocketsHttpHandler>(inner);
        var proxy = Assert.IsType<WebProxy>(socketsHandler.Proxy);
        Assert.Equal($"socks5://localhost:{socksPort}", proxy.Address?.ToString().TrimEnd('/'));
    }
}
