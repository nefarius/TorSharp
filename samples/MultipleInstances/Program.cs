using System.Net;
using Nefarius.Utilities.TorProxy;

// Share the same downloaded tools with all instances.
var baseSettings = new TorProxySettings();
using (var httpClient = new HttpClient())
{
    var fetcher = new TorProxyToolFetcher(baseSettings, httpClient);
    await fetcher.FetchAsync();
}

// Start the parallel instances with a barrier to ensure some parallelism.
var parallelInstances = 4;
var barrier = new Barrier(parallelInstances);
var tasks = Enumerable
    .Range(0, parallelInstances)
    .Select(i => RunInstanceAsync(baseSettings, i.ToString(), 10000 + (i * 10), barrier))
    .ToList();
await Task.WhenAny(tasks);
await Task.WhenAll(tasks);

async Task RunInstanceAsync(TorProxySettings baseSettings, string name, int startingPort, Barrier barrier)
{
    var settings = new TorProxySettings
    {
        // The extracted tools directory must not be shared.
        ExtractedToolsDirectory = Path.Combine(baseSettings.ExtractedToolsDirectory, name),

        // The zipped tools directory can be shared, as long as the tool fetcher does not run in parallel.
        ZippedToolsDirectory = baseSettings.ZippedToolsDirectory,

        // Each Tor process needs its own SOCKS and control ports.
        TorSettings = { SocksPort = startingPort, ControlPort = startingPort + 1 },
    };

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
            var ipA = (await httpClient.GetStringAsync("https://api.ipify.org")).Trim();
            Console.WriteLine($"[Instance {name}] {ipA}");
            barrier.SignalAndWait();
            var ipB = (await httpClient.GetStringAsync("https://api.ipify.org")).Trim();
            Console.WriteLine($"[Instance {name}] {ipB}");
        }

        proxy.Stop();
    }
}
