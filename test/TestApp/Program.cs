using System.Net;
using Nefarius.Utilities.TorProxy;
using Nefarius.Utilities.TorProxy.Tests.TestSupport;

if (args.Length != 3)
{
    Console.WriteLine("There must be exactly three command line arguments:");
    Console.WriteLine();
    Console.WriteLine("  1. a string parseable as a boolean, which is whether to write tool output to the console");
    Console.WriteLine("  2. the zipped tools directory");
    Console.WriteLine("  3. the extracted tools directory");
    Console.WriteLine();
    Console.WriteLine($"{args.Length} arguments were provided:");
    Console.WriteLine();
    for (var i = 0; i < args.Length; i++)
    {
        Console.WriteLine($"  {i + 1}. '{args[i]}'");
    }
    return 1;
}

if (!bool.TryParse(args[0], out var writeToConsole))
{
    Console.WriteLine($"Argument 1 must be a boolean, but got: '{args[0]}'");
    return 1;
}

using var reservedPorts = ReservedPorts.Reserve(2);

var settings = new TorProxySettings
{
    TorSettings =
    {
        SocksPort = reservedPorts.Ports[0],
        ControlPort = reservedPorts.Ports[1],
    },
    WriteToConsole = writeToConsole,
    ZippedToolsDirectory = args[1],
    ExtractedToolsDirectory = args[2],
};

using (var httpClient = new HttpClient())
{
    var fetcher = new TorProxyToolFetcher(settings, httpClient);
    await fetcher.FetchAsync();
}

using (var proxy = new TorProxy(settings))
{
    var handler = new HttpClientHandler
    {
        Proxy = new WebProxy(new Uri("socks5://localhost:" + settings.TorSettings.SocksPort))
    };

    using (handler)
    using (var httpClient = new HttpClient(handler))
    {
        await proxy.ConfigureAndStartAsync();
        await httpClient.GetStringAsync("http://api.ipify.org");
    }

    proxy.Stop();
}

Console.WriteLine("TestApp says it's done!");

return 0;
