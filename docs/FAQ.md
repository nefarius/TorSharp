# FAQ / Troubleshooting

## Migrating from Knapcode.TorSharp

If you were using `Knapcode.TorSharp`, here is the migration table:

| Old (Knapcode.TorSharp) | New (Nefarius.Utilities.TorProxy) |
|---|---|
| `using Knapcode.TorSharp;` | `using Nefarius.Utilities.TorProxy;` |
| `TorSharpProxy` | `TorProxy` |
| `ITorSharpProxy` | `ITorProxy` |
| `TorSharpProxyExtensions` | `TorProxyExtensions` |
| `TorSharpSettings` | `TorProxySettings` |
| `TorSharpToolFetcher` | `TorProxyToolFetcher` |
| `ITorSharpToolFetcher` | `ITorProxyToolFetcher` |
| `TorSharpPrivoxySettings` | `TorProxyPrivoxySettings` |
| `TorSharpTorSettings` | `TorProxyTorSettings` |
| `TorSharpException` | `TorProxyException` |
| `TorSharpArchitecture` | `TorProxyArchitecture` |
| `TorSharpOSPlatform` | `TorProxyOSPlatform` |

## Running on ARM64 (or other unsupported CPU architectures)

### Why it fails today

The Tor Project publishes `tor-expert-bundle` binaries for the following targets only:

| OS | Architectures |
|---|---|
| Windows | i686, x86_64 |
| Linux | i686, x86_64 |
| macOS | x86_64, aarch64 |
| Android | x86, x86_64, armv7, aarch64 |

There is no `tor-expert-bundle-linux-aarch64-*` and no `tor-expert-bundle-windows-arm64-*`. The [TorSharp.Mirror](https://github.com/nefarius/TorSharp.Mirror) binary cache mirrors whatever the Tor Project publishes, so it cannot serve them either. Privoxy upstream (`silvester.org.uk`) only publishes `amd64` and `i386` Debian packages.

### What the library does today

`TorProxySettings` auto-detects the process architecture via `RuntimeInformation.ProcessArchitecture`. Any value other than `X86` or `X64` (e.g. `Arm64`, `Arm`, `RiscV64`) maps to `TorProxyArchitecture.Unknown`. As soon as a tool resolver is asked to pick a binary — in `TorProxyToolFetcher.FetchAsync()` or `TorProxy.ConfigureAndStartAsync()` — it throws a `TorProxyException` with a message that names the real process architecture and points here.

### Workaround: Windows on ARM (recommended)

Publish your .NET application as `win-x64` instead of `win-arm64`:

```bash
dotnet publish -r win-x64 --self-contained
```

Windows 11 on ARM transparently emulates x64 processes. `RuntimeInformation.ProcessArchitecture` reports `X64` for an x64-published binary, so the library detects the architecture correctly and operates unchanged.

### Workaround: Linux ARM64

There is no in-library workaround — `TorSettings.ExecutablePathOverride` alone is not sufficient because the architecture check happens before the override is honoured. Instead, install Tor from your distribution's package manager and talk to it directly:

```bash
# Debian / Ubuntu (arm64 packages are published by the Tor Project)
sudo apt install -y tor
```

Point your `HttpClient` at the system Tor's SOCKS5 port (default `9050`) without using `TorProxyToolFetcher` or `TorProxy`:

```csharp
var handler = new HttpClientHandler
{
    Proxy = new WebProxy(new Uri("socks5://localhost:9050"))
};
using var httpClient = new HttpClient(handler);
```

For a Debian/Ubuntu arm64 system, the official Tor Project apt repository (`deb.torproject.org`) provides up-to-date packages for bookworm, jammy, noble, and other distributions:

```
https://deb.torproject.org/torproject.org
```

### macOS on ARM

macOS support is not planned regardless of architecture.

## The tool fetcher is throwing an exception. What do I do?

By default `TorProxyToolFetcher` tries to download binaries from the
[TorSharp.Mirror](https://github.com/nefarius/TorSharp.Mirror) first (a long-term binary
cache with SHA256 verification), then falls back to the original upstream sites.  If an
exception still occurs, here are the next steps:

1. **Check the mirror.** Visit
   [github.com/nefarius/TorSharp.Mirror/releases](https://github.com/nefarius/TorSharp.Mirror/releases)
   to see when the mirror was last refreshed.

1. **Opt out of the mirror** and use upstream discovery directly:
   ```csharp
   var settings = new TorProxySettings { UseMirror = false };
   ```

1. [Open an issue](https://github.com/nefarius/TorSharp/issues/new) so we can look into it.

1. Work around the issue by setting up the tools manually and not using `TorProxyToolFetcher`. [See below](#how-do-i-set-up-the-tools-manually).

1. Investigate the issue yourself. The [TorProxy.Sandbox](https://github.com/nefarius/TorSharp/blob/master/samples/TorProxy.Sandbox/Program.cs) project is helpful for this. Pull requests accepted.

## How do I set up the tools manually?

If you don't want to use the `TorProxyToolFetcher` to download the latest version of the tools for you or if you want to use a specific version of Tor and Privoxy, follow these steps.

1. Make a directory that will hold the zipped Tor and Privoxy binaries.
1. Put a Tor Win32 ZIP in that folder with the file name like: `tor-win32-{version}.zip`
   - `{version}` must be parsable as a `System.Version` meaning it is `major.minor[.build[.revision]]`.
   - Example: `tor-win32-0.3.5.8.zip`
   - The ZIP is expected to have `Tor\tor.exe`.
1. Put a Privoxy Win32 ZIP in that folder with a file name like: `privoxy-win32-{version}.zip`
   - Again, `{version}` must be parsable as a `System.Version`.
   - Example: `privoxy-win32-3.0.26.zip`
   - The ZIP is expected to have `privoxy.exe`.
1. Initialize a `TorProxySettings` instance where `ZippedToolsDirectory` is the directory created above.
1. Pass this settings instance to the `TorProxy` constructor.

## Can I run multiple instances in parallel?

Yes, you can. See this sample: [`samples/MultipleInstances/Program.cs`](https://github.com/nefarius/TorSharp/tree/master/samples/MultipleInstances/Program.cs).

However, you need to adhere to the following guidance.

None of the types in this library should be considered thread safe. Use separate instances for each parallel task/thread.
- `TorProxy`: this is stateful and should not be shared.
- `TorProxySettings`: the values held in the settings class need to be different for parallel threads, so it doesn't make sense to share instances.
- `TorProxyToolFetcher`: this is stateless so it may be safe, but I would keep this a singleton since you shouldn't have multiple copies of the zipped tools (just multiple copies of the *extracted tools*).

Parallel threads must have different values for these settings. The defaults will not work.

- **Must be made unique by you:**
  - `TorProxySettings.ExtractedToolsDirectory`: this is the parent directory of the tool working directories. Specify a different value for each thread. In the sample above, each parallel task uses a sibling directory, e.g. `{some_root}/a`, `{some_root}/b`, etc.
  - `TorProxySettings.PrivoxySettings.Port`: this is the Privoxy listen port. Each Privoxy process needs its own port. Only relevant when Privoxy is opted in (`PrivoxySettings.Disable = false`); ignored by default since Privoxy is disabled.
  - `TorProxySettings.TorSettings.SocksPort`: this is the Tor SOCKS listen port. Each Tor process needs its own port.
- **Must be unique, but only if you set them:**
  - `TorProxySettings.TorSettings.ControlPort`: this is the Tor control port used for the control protocol. Each Tor process needs its own port.
  - `TorProxySettings.TorSettings.AdditionalSockPorts`: if used, it must have unique values.
  - `TorProxySettings.TorSettings.HttpTunnelPort`: if used, it must have a unique value.
  - `TorProxySettings.TorSettings.DataDirectory`: the default is based `ExtractedToolsDirectory`, but if you manually set it, it must be unique.

In general, directory configuration values must be different from all of the other directories, except `TorProxySettings.ZippedToolsDirectory` which should not be downloaded to in parallel by `TorProxyToolFetcher` but can be read from in parallel with multiple `TorProxy` instances. Port configuration values need to all be unique.

## How do I change what TorProxy logs?

By default, TorProxy lets the tools (Tor, Privoxy) log to the main process stdout and stderr. If you want to disable this behavior, set `TorProxySettings.WriteToConsole` to `false`. If you want to intercept the output from the tools, attach to the `TorProxy.OutputDataReceived` (for stdout) and `TorProxy.ErrorDataReceived` (for stderr) events. In your event handler, you can log to some external sink or enqueue the line for processing. The event handlers are fired from a task using the default task scheduler so this blocks one of the shared worker threads. Don't do too much heavy lifting there! If you want to know which tool sent the log message, look at the `DataEventArgs.ExecutablePath` property.

For a full sample, see this: [`samples/CustomLogging/Program.cs`](https://github.com/nefarius/TorSharp/blob/master/samples/CustomLogging/Program.cs).

## Privoxy fails to start — missing shared libraries

It's possible some expected shared libraries aren't present. Try to look at the error message and judge which library needs to be installed from your distro's package repository.

### Ubuntu 20.04

**Problem:** On Ubuntu 20.04 the following errors may appear:

```
/tmp/TorExtracted/privoxy-linux64-3.0.29/usr/sbin/privoxy: error while loading shared libraries: libmbedtls.so.12: cannot open shared object file: No such file or directory
```

**Solution:** install the missing dependencies. Note that these steps **install packages from the Debian repository**. This is very much not recommended by official guidance. For my own testing, it works well enough. I use this trick in the GitHub Action workflow. Do this at your own risk!

```console
[joel@ubuntu]$ curl -O http://ftp.us.debian.org/debian/pool/main/m/mbedtls/libmbedcrypto3_2.16.0-1_amd64.deb
[joel@ubuntu]$ curl -O http://ftp.us.debian.org/debian/pool/main/m/mbedtls/libmbedx509-0_2.16.0-1_amd64.deb
[joel@ubuntu]$ curl -O http://ftp.us.debian.org/debian/pool/main/m/mbedtls/libmbedtls12_2.16.0-1_amd64.deb
[joel@ubuntu]$ echo "eb6751c98adfdf0e7a5a52fbd1b5a284ffd429e73abb4f0a4497374dd9f142c7 libmbedcrypto3_2.16.0-1_amd64.deb" > SHA256SUMS
[joel@ubuntu]$ echo "e8ea5dd71b27591c0f55c1d49f597d3d3b5c747bde25d3b3c3b03ca281fc3045 libmbedx509-0_2.16.0-1_amd64.deb" >> SHA256SUMS
[joel@ubuntu]$ echo "786c7e7805a51f3eccd449dd44936f735afa97fc5f3702411090d8b40f0e9eda libmbedtls12_2.16.0-1_amd64.deb" >> SHA256SUMS
[joel@ubuntu]$ sha256sum -c SHA256SUMS || { echo "Checksum failed. Aborting."; exit 1; }
[joel@ubuntu]$ sudo apt-get install -y --allow-downgrades ./*.deb
```

I have the checksum verification in there because these pages have SSL problems and the advertised URL is HTTP not HTTPS (by design I think).

### Debian 10

**Problem:** On Debian 10 the following errors may appear:

```
/tmp/TorExtracted/privoxy-linux64-3.0.29/usr/sbin/privoxy: error while loading shared libraries: libbrotlidec.so.1: cannot open shared object file: No such file or directory
```

```
/tmp/TorExtracted/privoxy-linux64-3.0.29/usr/sbin/privoxy: error while loading shared libraries: libmbedtls.so.12: cannot open shared object file: No such file or directory
```

**Solution:** install two missing dependencies. Thanks for [the heads up](https://github.com/joelverhagen/TorSharp/issues/64#issuecomment-774701302), [@cod3rshotout](https://github.com/cod3rshotout)!

```console
[joel@debian10]$ sudo apt-get install -y libbrotli1 libmbedtls-dev
```

## Privoxy fails to start — try `ExecutablePathOverride`

On Linux, the Privoxy binaries fetched seem to be built for the latest Debian and Ubuntu distributions. I can confirm that some other distributions don't work.

My guess is that there are missing shared libraries that are different on the running platform than the Debian platform that Privoxy was compiled for. The easiest workaround is to install Privoxy to your system and set the `TorProxySettings.PrivoxySettings.ExecutablePathOverride` configuration setting to `"privoxy"` (i.e. use Privoxy from PATH).

After you install it, make sure `privoxy` is in the PATH.

```console
[joel@linux]$ which privoxy
/usr/sbin/privoxy
```

After this is done, just configure TorProxy to use the system Privoxy with the `ExecutablePathOverride` setting:

```csharp
var settings = new TorProxySettings();
settings.PrivoxySettings.ExecutablePathOverride = "privoxy";
```

Note that you may encounter warning or error messages in the output due to new configuration being used with an older executable. I haven't run into any problems with this myself but it's possible things could get weird.

### CentOS 7

**Problem:** the following error appears:

```
/tmp/TorExtracted/privoxy-linux64-3.0.28/usr/sbin/privoxy: error while loading shared libraries: libpcre.so.3: cannot open shared object file: No such file or directory
```

**Solution:** install Privoxy. It is available on `epel-release`.

```console
[joel@centos]$ sudo yum install epel-release -y
...
[joel@centos]$ sudo yum install privoxy -y
```

### Debian 9

**Problem:** the following errors may appear:

```
/tmp/TorExtracted/privoxy-linux64-3.0.29/usr/sbin/privoxy: error while loading shared libraries: libbrotlidec.so.1: cannot open shared object file: No such file or directory
```

```
/tmp/TorExtracted/privoxy-linux64-3.0.29/usr/sbin/privoxy: error while loading shared libraries: libmbedtls.so.12: cannot open shared object file: No such file or directory
```

**Solution:** install Privoxy. It is available in the default source lists.

```console
[joel@debian9]$ sudo apt-get install privoxy -y
```
