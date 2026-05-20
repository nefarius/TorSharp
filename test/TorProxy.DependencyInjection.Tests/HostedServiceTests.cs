using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Nefarius.Utilities.TorProxy;
using Nefarius.Utilities.TorProxy.DependencyInjection;
using Xunit;

namespace Nefarius.Utilities.TorProxy.DependencyInjection.Tests;

public class HostedServiceTests
{
    [Fact]
    public async Task StartAsync_AutoFetchTools_True_CallsFetchThenConfigureStart()
    {
        var proxy = Substitute.For<ITorProxy>();
        var fetcher = Substitute.For<ITorProxyToolFetcher>();
        var options = Options.Create(new TorProxyHostedServiceOptions { AutoFetchTools = true });

        var sut = new TorProxyHostedService(proxy, fetcher, options);
        await sut.StartAsync(CancellationToken.None);

        await fetcher.Received(1).FetchAsync();
        await proxy.Received(1).ConfigureAndStartAsync();
    }

    [Fact]
    public async Task StartAsync_AutoFetchTools_False_SkipsFetch()
    {
        var proxy = Substitute.For<ITorProxy>();
        var fetcher = Substitute.For<ITorProxyToolFetcher>();
        var options = Options.Create(new TorProxyHostedServiceOptions { AutoFetchTools = false });

        var sut = new TorProxyHostedService(proxy, fetcher, options);
        await sut.StartAsync(CancellationToken.None);

        await fetcher.DidNotReceive().FetchAsync();
        await proxy.Received(1).ConfigureAndStartAsync();
    }

    [Fact]
    public async Task StopAsync_CallsProxyStop()
    {
        var proxy = Substitute.For<ITorProxy>();
        var fetcher = Substitute.For<ITorProxyToolFetcher>();
        var options = Options.Create(new TorProxyHostedServiceOptions());

        var sut = new TorProxyHostedService(proxy, fetcher, options);
        await sut.StopAsync(CancellationToken.None);

        proxy.Received(1).Stop();
    }
}
