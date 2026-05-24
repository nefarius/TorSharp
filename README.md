# <img src="assets/NSS-128x128.png" align="left" />Nefarius.Utilities.TorProxy

[![.NET](https://github.com/nefarius/TorSharp/actions/workflows/build.yml/badge.svg)](https://github.com/nefarius/TorSharp/actions/workflows/build.yml)
![.NET Standard 2.0](https://img.shields.io/badge/.NET-Standard%202.0-blue)
![.NET 8](https://img.shields.io/badge/.NET-8-blue)
![.NET 9](https://img.shields.io/badge/.NET-9-blue)
![.NET 10](https://img.shields.io/badge/.NET-10-blue)
[![Assisted by Cursor AI](https://img.shields.io/badge/Assisted%20by-Cursor%20AI-8B5CF6?style=flat)](https://cursor.com/)  

&nbsp;

> [!NOTE]
> **Fork notice.** `Nefarius.Utilities.TorProxy` is a maintained fork of
> [`Knapcode.TorSharp`](https://github.com/joelverhagen/TorSharp) by
> [Joel Verhagen](https://github.com/joelverhagen). All original copyright is preserved
> (see [LICENSE](LICENSE)); the fork's modifications are © 2024-2026 Nefarius.
> The upstream project remains the canonical reference. See
> [CHANGELOG.md](CHANGELOG.md) for what changed in this fork.

[![Nuget](https://img.shields.io/nuget/v/Nefarius.Utilities.TorProxy?label=Nefarius.Utilities.TorProxy)](https://www.nuget.org/packages/Nefarius.Utilities.TorProxy/)
[![Nuget](https://img.shields.io/nuget/dt/Nefarius.Utilities.TorProxy)](https://www.nuget.org/packages/Nefarius.Utilities.TorProxy/)
[![Nuget](https://img.shields.io/nuget/v/Nefarius.Utilities.TorProxy.DependencyInjection?label=Nefarius.Utilities.TorProxy.DependencyInjection)](https://www.nuget.org/packages/Nefarius.Utilities.TorProxy.DependencyInjection/)
[![Nuget](https://img.shields.io/nuget/dt/Nefarius.Utilities.TorProxy.DependencyInjection)](https://www.nuget.org/packages/Nefarius.Utilities.TorProxy.DependencyInjection/)

Use Tor for your C# HTTP clients via .NET's built-in SOCKS5 support. Privoxy is available as an opt-in HTTP-proxy front-end for legacy consumers.

## Features

- Routes `HttpClient` traffic through Tor with zero external dependencies beyond the library itself.
- Automatically downloads and verifies Tor (and optionally Privoxy) binaries via the [TorSharp.Mirror](https://github.com/nefarius/TorSharp.Mirror) long-term binary cache (SHA256-verified, nightly refresh) with transparent fallback to upstream.
- SOCKS5-first: Tor is exposed directly via its SOCKS5 port; `.NET 6+` `HttpClientHandler` works out of the box.
- Optional Privoxy HTTP-proxy front-end for clients that cannot use SOCKS5 (opt-in via `PrivoxySettings.Disable = false`).
- `IHostedService` integration for ASP.NET Core / Generic Host via the companion `Nefarius.Utilities.TorProxy.DependencyInjection` package.
- Tor stdout/stderr forwarded to `ILogger` with leading timestamp/severity stripped for clean, deduplication-free output.
- Supports running multiple parallel Tor instances (each with its own ports and directories).
- Configurable mirror URL for self-hosted binary caches.
- Process management via [CliWrap](https://github.com/Tyrrrz/CliWrap); no PInvoke or platform-specific job objects.

## Limitations

- macOS support is not planned.
- ARM64 (Windows on ARM, Linux aarch64) is not directly supported because the Tor Project does not publish a `tor-expert-bundle` for these targets. See [docs/FAQ.md](docs/FAQ.md#running-on-arm64-or-other-unsupported-cpu-architectures) for workarounds.
- .NET Framework targets were dropped in v3.0.0. Use v2.x for .NET Framework 4.6.2/4.7.2 support.
- Privoxy is disabled by default since v6.0.0; callers that relied on the HTTP proxy front-end must explicitly set `PrivoxySettings.Disable = false`.
- No type is thread-safe. Use separate instances per parallel task (see [docs/FAQ.md](docs/FAQ.md)).

## Supported systems

| Component | Supported |
|---|---|
| .NET | .NET Standard 2.0, .NET 8, .NET 9, .NET 10 |
| .NET Framework | v2.x only (4.6.2 / 4.7.2); dropped in v3+ |
| Windows | 10 / Server 2019 and later; 11 / Server 2022 and later |
| Linux | Ubuntu 22.04, Ubuntu 24.04, Debian 11, Debian 12 |
| Linux (system binary) | CentOS/RHEL and Alpine via `ExecutablePathOverride` |
| macOS | Not planned |
| CPU architectures | x86 (32-bit) and x64 / x86_64 (64-bit). ARM64 is not directly supported; see [FAQ](docs/FAQ.md#running-on-arm64-or-other-unsupported-cpu-architectures). |

## Install

Core library (imperative API, `netstandard2.0` + `net8.0` + `net9.0` + `net10.0`):

```bash
dotnet add package Nefarius.Utilities.TorProxy
```

ASP.NET Core / Generic Host integration (`net8.0` + `net9.0` + `net10.0`):

```bash
dotnet add package Nefarius.Utilities.TorProxy.DependencyInjection
```

## Quick start — SOCKS5 (default)

.NET 6+ has built-in SOCKS5 proxy support, so Privoxy is not needed. `TorProxy` defaults to Tor-only mode and exposes the SOCKS5 port directly.

See [`samples/NativeSocksProxy/Program.cs`](https://github.com/nefarius/TorSharp/tree/master/samples/NativeSocksProxy/Program.cs) for a working sample.

```csharp
var settings = new TorProxySettings(); // Privoxy is disabled by default

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
```

## ASP.NET Core / Generic Host (DI)

The companion package `Nefarius.Utilities.TorProxy.DependencyInjection` provides a
single-call registration that wires the settings (options pattern), the proxy singleton, the
tool fetcher, an `IHostedService` that auto-starts Tor on application startup, and a
`UseTorSocks5Proxy()` extension for named `HttpClient` registrations.

Tor's stdout/stderr output is forwarded directly to `ILogger` — the leading
timestamp and level prefix emitted by the Tor process are stripped automatically so
console formatters produce clean, deduplicated output:

```text
[14:08:32 INF] Nefarius.Utilities.TorProxy.Tor: Heartbeat: Tor's uptime is 1 day 18:00 hours, …
```

Instead of the doubled metadata you would see otherwise:

```text
[14:08:32 INF] TOR Proxy: May 20 14:08:32.000 [notice] Heartbeat: Tor's uptime is 1 day 18:00 hours, …
```

See [`samples/GenericHostDI/Program.cs`](https://github.com/nefarius/TorSharp/tree/master/samples/GenericHostDI/Program.cs) for a complete working sample.

```csharp
// Program.cs (ASP.NET Core / Generic Host)

builder.Services.AddTorProxy(o =>
{
    o.PrivoxySettings.Disable = true;      // use SOCKS5 directly (default)
    o.WriteToConsole = false;              // let ILogger handle output
    // o.MinTorLogLevel = LogLevel.Information;   // optional: allow Information and above only
});

// Route requests through Tor SOCKS5 with a single call.
builder.Services.AddHttpClient("Crawler", c =>
    {
        c.Timeout = TimeSpan.FromSeconds(45);
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
    })
    .UseTorSocks5Proxy()
    .SetHandlerLifetime(TimeSpan.FromMinutes(5));

// A direct (non-Tor) client — omit UseTorSocks5Proxy().
builder.Services.AddHttpClient("CrawlerDirect", c =>
{
    c.Timeout = TimeSpan.FromSeconds(45);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
});
```

### Hosted-service options

By default `TorProxyHostedService` fetches the Tor binary on startup (`AutoFetchTools = true`). To skip if you manage downloads yourself:

```csharp
builder.Services.AddTorProxy(
    configure: o => { /* settings */ },
    configureHostedService: o => o.AutoFetchTools = false);
```

### Filtering Tor log output

Via settings:

```csharp
builder.Services.AddTorProxy(o =>
{
    o.MinTorLogLevel = LogLevel.Information; // drops Trace and Debug
});
```

Via `appsettings.json` using the standard logging filter:

```json
{
  "Logging": {
    "LogLevel": {
      "Nefarius.Utilities.TorProxy.Tor": "Information"
    }
  }
}
```

## Legacy HTTP-proxy clients — opt-in Privoxy

If your HTTP client cannot use a SOCKS5 proxy directly (e.g. `netstandard2.0` consumers or third-party libraries that only accept an HTTP proxy URL), you can opt in to the bundled Privoxy front-end. **Privoxy is disabled by default** — you must explicitly set `PrivoxySettings.Disable = false`.

See [`samples/TorProxy.Sandbox/Program.cs`](https://github.com/nefarius/TorSharp/tree/master/samples/TorProxy.Sandbox/Program.cs) for a working sample.

```csharp
// configure
var settings = new TorProxySettings
{
   ZippedToolsDirectory = Path.Combine(Path.GetTempPath(), "TorZipped"),
   ExtractedToolsDirectory = Path.Combine(Path.GetTempPath(), "TorExtracted"),
   PrivoxySettings = { Disable = false, Port = 1337 }, // opt in explicitly
   TorSettings =
   {
      SocksPort = 1338,
      ControlPort = 1339,
      ControlPassword = "foobar",
   },
};

// download tools (Tor + Privoxy)
await new TorProxyToolFetcher(settings, new HttpClient()).FetchAsync();

// execute
var proxy = new TorProxy(settings);
var handler = new HttpClientHandler
{
    Proxy = new WebProxy(new Uri("http://localhost:" + settings.PrivoxySettings.Port))
};
var httpClient = new HttpClient(handler);
await proxy.ConfigureAndStartAsync();
Console.WriteLine(await httpClient.GetStringAsync("http://api.ipify.org"));
await proxy.GetNewIdentityAsync();
Console.WriteLine(await httpClient.GetStringAsync("http://api.ipify.org"));
proxy.Stop();
```

## Mirror

`TorProxyToolFetcher` uses a **long-term binary cache** at
[github.com/nefarius/TorSharp.Mirror](https://github.com/nefarius/TorSharp.Mirror) by
default (`TorProxySettings.UseMirror = true`).

### How it works

1. The fetcher downloads
   `https://github.com/nefarius/TorSharp.Mirror/releases/latest/download/manifest.json`
   which lists the latest cached version of each binary with its SHA256 digest.
2. The matching binary is downloaded directly from the GitHub Release asset.
3. After download, the SHA256 is verified against the manifest value.
4. If the mirror is unreachable or does not contain an entry for the current platform,
   the fetcher automatically falls back to the original upstream discovery logic.

### Refresh cadence

The mirror workflow runs **nightly** and publishes a new dated GitHub Release
(`mirror-YYYY.MM.DD`).  The `releases/latest` redirect always points to the most recent
run.

### Opt out

```csharp
var settings = new TorProxySettings { UseMirror = false };
```

### Self-host

Point the fetcher at your own mirror by serving a compatible `manifest.json` at a stable
HTTPS URL:

```csharp
var settings = new TorProxySettings
{
    MirrorManifestUrl = "https://my-internal-mirror.example.com/torsharp/manifest.json"
};
```

The manifest schema is published with every mirror release as `manifest.schema.json`.

## Notice

This product is produced independently from the Tor® anonymity software and carries no guarantee from [The Tor Project](https://www.torproject.org/) about quality, suitability or anything else.

## FAQ / Troubleshooting

See [`docs/FAQ.md`](docs/FAQ.md) for setup troubleshooting, the `Knapcode.TorSharp` migration
table, running multiple parallel instances, custom logging, and Linux Privoxy dependency
workarounds.

## License and credits

- License: [MIT](LICENSE) — original copyright © 2020 Joel Verhagen; fork modifications © 2024-2026 Nefarius.
- Upstream project: [joelverhagen/TorSharp](https://github.com/joelverhagen/TorSharp)
- Changelog: [CHANGELOG.md](CHANGELOG.md)
- Process management: [CliWrap](https://github.com/Tyrrrz/CliWrap) by Oleksii Holub
