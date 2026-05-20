using System.Net;
using Nefarius.Utilities.TorProxy;

// .NET 6+ has built-in SOCKS5 support, so Privoxy is not needed for modern consumers.
// Privoxy is now opt-in (disabled by default); the explicit Disable = true below is
// illustrative — it reflects what TorProxySettings already defaults to.
// https://devblogs.microsoft.com/dotnet/dotnet-6-networking-improvements/#socks-proxy-support
var settings = new TorProxySettings
{
    PrivoxySettings = { Disable = true } // default since 6.0; kept here for clarity
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
