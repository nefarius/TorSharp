using System.Net;
using Nefarius.Utilities.TorProxy;

// Starting on .NET 6, there is built-in support for SOCKS proxies. This means we don't need Privoxy!
// https://devblogs.microsoft.com/dotnet/dotnet-6-networking-improvements/#socks-proxy-support
var settings = new TorProxySettings
{
    PrivoxySettings = { Disable = true }
};

// download Tor
using (var httpClient = new HttpClient())
{
    var fetcher = new TorProxyToolFetcher(settings, httpClient);
    await fetcher.FetchAsync();
}

// execute
using (var proxy = new TorProxy(settings))
{
    await proxy.ConfigureAndStartAsync();

    var handler = new HttpClientHandler
    {
        Proxy = new WebProxy(new Uri("socks5://localhost:" + settings.TorSettings.SocksPort))
    };

    using (handler)
    using (var httpClient = new HttpClient(handler))
    {
        var result = await httpClient.GetStringAsync("https://check.torproject.org/api/ip");

        Console.WriteLine();
        Console.WriteLine("Are we using Tor?");
        Console.WriteLine(result);
    }

    proxy.Stop();
}
