// This sample shows how to integrate TorProxy with the .NET Generic Host (or ASP.NET Core).
//
// Key points:
//  • AddTorProxy() registers TorProxySettings (via IOptions), ITorProxy, ITorProxyToolFetcher,
//    and a TorProxyHostedService that auto-fetches the Tor binary and starts the proxy on
//    application startup.
//  • Tor's stdout/stderr is forwarded to Microsoft.Extensions.Logging — the leading
//    timestamp+level prefix is stripped so the host formatter is the single source of metadata.
//  • UseTorSocks5Proxy() wires a named HttpClient to route all requests through Tor's SOCKS5 port.
//
// To run:
//   dotnet run --project samples/GenericHostDI

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nefarius.Utilities.TorProxy.DependencyInjection;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // ── Tor proxy ────────────────────────────────────────────────────────────
        services.AddTorProxy(o =>
        {
            // Privoxy is disabled by default; use Tor SOCKS5 directly.
            o.PrivoxySettings.Disable = true;
            o.WriteToConsole = false;          // let ILogger handle all output
            // o.MinTorLogLevel = LogLevel.Information; // optional: suppress Debug lines
        });

        // ── Named HttpClients ────────────────────────────────────────────────────
        services.AddHttpClient("Crawler", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(45);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible)");
            })
            .UseTorSocks5Proxy()          // route through Tor SOCKS5
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        // A direct (non-Tor) client — no UseTorSocks5Proxy() call.
        services.AddHttpClient("CrawlerDirect", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible)");
        });

        services.AddHostedService<DemoWorker>();
    })
    .Build();

await host.RunAsync();

// ── Worker that demonstrates the two HttpClients ──────────────────────────────────────────────
internal sealed class DemoWorker : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DemoWorker> _logger;
    private readonly IHostApplicationLifetime _appLifetime;

    public DemoWorker(
        IHttpClientFactory httpClientFactory,
        ILogger<DemoWorker> logger,
        IHostApplicationLifetime appLifetime)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _appLifetime = appLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The TorProxyHostedService has already started Tor by the time BackgroundService runs.

        using var torClient = _httpClientFactory.CreateClient("Crawler");
        var torIp = await torClient.GetStringAsync("https://api.ipify.org", stoppingToken);
        _logger.LogInformation("IP via Tor:    {IP}", torIp.Trim());

        using var directClient = _httpClientFactory.CreateClient("CrawlerDirect");
        var directIp = await directClient.GetStringAsync("https://api.ipify.org", stoppingToken);
        _logger.LogInformation("IP direct:     {IP}", directIp.Trim());

        _logger.LogInformation("Same IP? {Same}", string.Equals(torIp.Trim(), directIp.Trim()));

        // Work is done — shut the host down cleanly.
        _appLifetime.StopApplication();
    }
}
