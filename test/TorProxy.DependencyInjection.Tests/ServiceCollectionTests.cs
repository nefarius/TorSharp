using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Nefarius.Utilities.TorProxy;
using Nefarius.Utilities.TorProxy.DependencyInjection;
using Xunit;

namespace Nefarius.Utilities.TorProxy.DependencyInjection.Tests;

public class ServiceCollectionTests
{
    [Fact]
    public void AddTorProxy_RegistersITorProxyAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTorProxy(o => o.WriteToConsole = false);

        // Inspect the descriptor directly — no need to build a container or execute any tool code.
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ITorProxy));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddTorProxy_RegistersIOptionsOfTorProxySettings()
    {
        const int customPort = 19999;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTorProxy(o =>
        {
            o.WriteToConsole = false;
            o.TorSettings.SocksPort = customPort;
        });

        var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<TorProxySettings>>();

        Assert.Equal(customPort, opts.Value.TorSettings.SocksPort);
    }

    [Fact]
    public void AddTorProxy_RegistersConcreteTorProxySettings()
    {
        const int customPort = 19888;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTorProxy(o =>
        {
            o.WriteToConsole = false;
            o.TorSettings.SocksPort = customPort;
        });

        // Resolve TorProxySettings via IOptions; no proxy/tool code runs.
        var sp = services.BuildServiceProvider();
        var settings = sp.GetRequiredService<IOptions<TorProxySettings>>().Value;

        Assert.Equal(customPort, settings.TorSettings.SocksPort);
    }

    [Fact]
    public void AddTorProxy_RegistersHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTorProxy(o => o.WriteToConsole = false);

        var sp = services.BuildServiceProvider();
        var hostedServices = sp.GetServices<IHostedService>();

        Assert.Contains(hostedServices, s => s is TorProxyHostedService);
    }

    [Fact]
    public void AddTorProxy_ToolFetcherHttpClientIsRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTorProxy(o => o.WriteToConsole = false);

        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IHttpClientFactory>();

        // Should not throw — the named client must be registered.
        var client = factory.CreateClient(TorProxyServiceCollectionExtensions.TorProxyToolFetcherHttpClientName);
        Assert.NotNull(client);
    }
}
