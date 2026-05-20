# Changelog

## 7.0.0
* Added `Nefarius.Utilities.TorProxy.DependencyInjection` companion package (`net8.0`/`net9.0`) with:
  * `AddTorProxy(configure, configureHostedService)` — single-call `IServiceCollection` registration that wires `TorProxySettings` (options pattern), `ITorProxy`, `ITorProxyToolFetcher`, and a `TorProxyHostedService`.
  * `TorProxyHostedService` / `TorProxyHostedServiceOptions` — `IHostedService` that auto-fetches tool binaries and starts/stops the proxy with the Generic Host lifetime. `AutoFetchTools` can be set to `false` to skip the download step.
  * `UseTorSocks5Proxy()` extension on `IHttpClientBuilder` — routes the named `HttpClient`'s primary handler through the configured Tor SOCKS5 port.
* Added `ILoggerFactory` constructor overload on `TorProxy`. Tor (and Privoxy) output lines are parsed, the leading timestamp+severity prefix is stripped (see `LogTimestampStripping`), and forwarded to distinct logger categories (`Nefarius.Utilities.TorProxy.Tor`, `Nefarius.Utilities.TorProxy.Privoxy`) so log output is clean and filterable.
* Added `TorProxySettings.LogTimestampStripping` (default `true`), `MinTorLogLevel` (default `Debug`), and `MinPrivoxyLogLevel` (default `Debug`) settings knobs.

## 6.0.0
* **Breaking change:** `TorProxyPrivoxySettings.Disable` now defaults to `true`. Privoxy is opt-in.
* By default only Tor is fetched, extracted, configured, and started. Use the Tor SOCKS5 port directly via `socks5://localhost:{TorSettings.SocksPort}` (requires .NET 6+).
* `TorProxyToolFetcher` skips all Privoxy mirror/upstream lookups and downloads unless `PrivoxySettings.Disable = false`.
* CI no longer installs Privoxy Linux runtime dependencies on standard build runs (only on scheduled upstream-health runs).
* Docker and Docker-Alpine samples updated to use native SOCKS5; Privoxy is no longer installed in the container images by default.
* **Migration:** callers that relied on the HTTP-proxy front-end must explicitly opt in: `settings.PrivoxySettings.Disable = false`. Everything else (port, address, `ExecutablePathOverride`, download/extraction) works unchanged once opted in.

## 5.0.0
* Forked and republished as `Nefarius.Utilities.TorProxy`. Original `Knapcode.TorSharp` copyright (© 2020 Joel Verhagen) preserved.
* Package id changed: `Knapcode.TorSharp` → `Nefarius.Utilities.TorProxy`.
* Assembly and root namespace changed: `Knapcode.TorSharp` → `Nefarius.Utilities.TorProxy`.
* All public types renamed: `TorSharp*` → `TorProxy*` (e.g. `TorSharpProxy` → `TorProxy`, `TorSharpSettings` → `TorProxySettings`). See migration table in README.
* Default tools cache directory changed: `%TEMP%\Knapcode.TorSharp` → `%TEMP%\Nefarius.Utilities.TorProxy`.
* HTTP User-Agent changed: `TorSharp/{version}` → `Nefarius.Utilities.TorProxy/{version}`.

## 4.0.0
* Switch process management to [CliWrap](https://github.com/Tyrrrz/CliWrap) (v3.10.1), removing all hand-rolled PInvoke code.
* Drop `ToolRunnerType` enum and `TorSharpSettings.ToolRunnerType` — there is now a single built-in runner for all platforms.
* Drop `TorSharpSettings.VirtualDesktopName` — virtual-desktop isolation is no longer used.
* All PInvoke files (`Desktop`, `Job`, `Process`, `FileStreamEventEmitter`, `SafeDesktopHandle`, `SafeJobHandle`) removed.

## 3.1.0
* Add default-on mirror (`TorSharpSettings.UseMirror`): `TorSharpToolFetcher` now resolves
  Tor and Privoxy binaries from [TorSharp.Mirror](https://github.com/nefarius/TorSharp.Mirror)
  (a nightly-refreshed GitHub Releases cache) before falling back to upstream discovery.
* SHA256 verification: when a mirror entry is used, the downloaded archive is verified
  against the digest in `manifest.json`; a mismatch throws `TorSharpException`.
* HTTP hardening: all outbound requests now send a `TorSharp/{version}` User-Agent and
  retry up to 3× on transient 5xx / network errors with exponential back-off.
* Drop broken Privoxy upstream sources: `privoxy.org` RSS (returning HTTP 500) and
  SourceForge RSS (Cloudflare-blocked) are removed; `silvester.org.uk` remains as the
  sole upstream fallback.
* CI improvements: test-cache key is now bucketed to refresh with workflow changes;
  discovery responses (HTML/JSON) are cached alongside binary downloads to reduce
  upstream load; Linux Privoxy runtime deps are read dynamically from `manifest.json`
  instead of being hardcoded.
* New `TorSharpSettings.MirrorManifestUrl` for self-hosted mirrors.

## 3.0.0
* Drop .NET Framework 4.6.2 and 4.7.2 targets; add net8.0 and net9.0 targets.
* Modernize code: file-scoped namespaces, nullable annotations, LibraryImport P/Invoke.
* Switch to Central Package Management; bump all NuGet dependencies.

## 2.15.0
* Update Tor file pattern due to change in distribution file name format.

## 2.14.0
* Fix bug where relative directory paths didn't work right.

## 2.13.0
* Fix release build problem where it was building for x64 for not MSIL (x64 preferred).

## 2.12.0
* Fix bug with ExitNodes Tor setting having wrong GeoIP file path, add default Tor control password.

## 2.11.0
* Add ability to intercept and silence tool logging to stdout and stderr.

## 2.10.0
* Allow Privoxy to be disabled (.NET 6 supports SOCKS natively), fix some parallel bugs.

## 2.9.0
* Update to new Tor URLs, fixing tool fetcher, drop .NET 4.5 and add .NET 4.6.2.

## 2.8.1
* Fix potential deadlock in UI apps due to unnecessary Task.Yield.

## 2.8.0
* Add HttpTunnelPort Tor setting, use tor-win64, add .NET Framework 4.7.2 target.

## 2.7.0
* Separate proxy Configure and Start steps, support old behavior with extension method.

## 2.6.0
* Make SendCommandAsync public and add AdditionalSocksPorts.

## 2.5.0
* Don't try to use virtual desktops on Windows 7 or earlier.

## 2.4.0
* Add support for custom Tor bridges, allow configurable max Privoxy connections.

## 2.3.0
* Add support for Tor bridges (e.g. obfs4).

## 2.2.0
* Add Tor control methods to get traffic read and written, fix config bug.

## 2.1.0
* Allow override of executable paths (enables using tools from PATH).

## 2.0.1
* Fix bug in Windows Privoxy zipped tool prefix (was "win32", should be "win32-").

## 2.0.0
* Add support .NET Core on Linux.

## 2.0.0-beta1
* Add support for .NET Core on Windows.

## 1.9.1
* Fix null reference exception in TorControlClient.Dispose().

## 1.9.0
* Add UseExistingTools and CheckForUpdatesAsync so that TorSharpToolFetcher can not download so much.

## 1.8.6
* Improve error experience when one of the tool ZIP files is invalid.

## 1.8.5
* Add ListenAddress to TorSharpPrivoxySettings to allow external connections.

## 1.8.4
* Improve missing tool message and don't fail due to a SecurityProtocolType not being supported.

## 1.8.3
* Ignore version directories that don't just have digits.

## 1.8.2
* Enable three different ways of downloading Privoxy. Fastest one wins.

## 1.8.1
* Use Privoxy RSS feed for fetching tools instead of SourceForge.

## 1.8.0
* Add support for HTTPS proxy in Tor and organize TorSettings.

## 1.7.0
* Default Tor DataDirectory to a path relative to the Tor executable.

## 1.6.2
* Fix a bug where ToolRunnerType.VirtualDesktop was breaking on Windows 10 (version 1709).

## 1.6.1
* Fix a bug in the Tor fetcher caused by Tor versions with no Windows build.

## 1.6.0
* Add configuration API for tor exit nodes.

## 1.5.3
* Fix P/Invoke stubs to work on x64 so ToolRunnerType.VirtualDesktop works.

## 1.5.2
* Paths now fully work when they have spaces in them.

## 1.5.1
* Fix bug where usernames with spaces in them are not supported.

## 1.5.0
* Hash Tor passwords in process (by duplicating the hash algorithm).

## 1.4.0
* Add tool runner without jobs, configure Tor data directory, fix file system race condition.

## 1.3.1
* Fix dispose issue in TorControlClient.

## 1.3.0
* Extract interfaces and rename ToolFetcher to TorSharpToolFetcher.

## 1.2.0
* Add tool to download the latest Tor and Privoxy.

## 1.1.0
* Target .NET 4.5 instead of .NET 4.5.2.

## 1.0.0
* Initial release.
