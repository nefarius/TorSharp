using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Knapcode.TorSharp.Tools;

namespace Knapcode.TorSharp;

/// <summary>
/// Settings for how TorSharp behaves.
/// </summary>
public class TorSharpSettings
{
    public static readonly string DefaultToolsDirectory = Path.Combine(Path.GetTempPath(), "Knapcode.TorSharp");

    public const string DefaultMirrorManifestUrl =
        "https://github.com/nefarius/TorSharp.Mirror/releases/latest/download/manifest.json";

    public TorSharpSettings()
    {
        ReloadTools = false;
        EnableSecurityProtocolsForFetcher = true;
        ZippedToolsDirectory = Path.Combine(DefaultToolsDirectory, "ZippedTools");
        ExtractedToolsDirectory = Path.Combine(DefaultToolsDirectory, "ExtractedTools");
        WaitForConnect = TimeSpan.FromSeconds(5);
        UseMirror = true;
        MirrorManifestUrl = DefaultMirrorManifestUrl;

        if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            OSPlatform = TorSharpOSPlatform.Windows;
        }
        else if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
        {
            OSPlatform = TorSharpOSPlatform.Linux;
        }
        else
        {
            OSPlatform = TorSharpOSPlatform.Unknown;
        }

        Architecture = RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X86 => TorSharpArchitecture.X86,
            System.Runtime.InteropServices.Architecture.X64 => TorSharpArchitecture.X64,
            _ => TorSharpArchitecture.Unknown,
        };

        PrivoxySettings = new TorSharpPrivoxySettings();
        TorSettings = new TorSharpTorSettings();
    }

    /// <summary>
    /// If true, the tools (Tor and Privoxy) will be re-downloaded by <see cref="TorSharpToolFetcher"/> even if the
    /// latest version is already downloaded and will be re-extracted by <see cref="TorSharpProxy"/> even if the
    /// latest version is already extracted. This is disabled by default.
    /// </summary>
    public bool ReloadTools { get; set; }

    /// <summary>
    /// Enable all SSL/TLS protocol on <see cref="System.Net.ServicePointManager.SecurityProtocol"/> so that
    /// requests to fetch the tools don't fail due to protocol mismatches. This is enabled by default.
    /// </summary>
    public bool EnableSecurityProtocolsForFetcher { get; set; }

    /// <summary>
    /// Override the file pattern used for downloading a Privoxy binary from a given URL. The settings instance will
    /// be this instance itself, for convenience. If this delegate returns null or if the delegate itself is set to
    /// null, the default pattern is used. This is useful when the remote tool distribution changes the file pattern
    /// and the TorSharp code hasn't been updated yet.
    /// </summary>
    public Func<TorSharpSettings, Uri, FileNamePatternAndFormat>? PrivoxyFilePatternResolver { get; set; }

    /// <summary>
    /// Override the file pattern used for downloading a Tor binary from a given URL. The settings instance will
    /// be this instance itself, for convenience. If this delegate returns null or if the delegate itself is set to
    /// null, the default pattern is used. This is useful when the remote tool distribution changes the file pattern
    /// and the TorSharp code hasn't been updated yet.
    /// </summary>
    public Func<TorSharpSettings, Uri, FileNamePatternAndFormat>? TorFilePatternResolver { get; set; }

    /// <summary>
    /// The directory to download the zipped tools. This defaults to <c>%TEMP%\Knapcode.TorSharp\ZippedTools</c>.
    /// </summary>
    public string ZippedToolsDirectory { get; set; }

    /// <summary>
    /// The directory to extract the tools to and run them from.  This defaults to
    /// <c>%TEMP%\Knapcode.TorSharp\ExtractedTools</c>.
    /// </summary>
    public string ExtractedToolsDirectory { get; set; }

    /// <summary>
    /// Instead of downloading the latest version of the tools in <see cref="TorSharpToolFetcher"/>, use any
    /// existing tools already downloaded to <see cref="ZippedToolsDirectory"/>.
    /// </summary>
    public bool UseExistingTools { get; set; }

    /// <summary>
    /// What strategy to use when fetching the latest version of a tool. This behavior matters when there are
    /// multiple URLs or feeds that a checked for a single tool.
    /// </summary>
    public ToolDownloadStrategy ToolDownloadStrategy { get; set; }

    /// <summary>
    /// When <c>true</c> (the default), <see cref="TorSharpToolFetcher"/> first tries to resolve tool
    /// versions and download binaries from the mirror specified by <see cref="MirrorManifestUrl"/>.
    /// If the mirror is unreachable or does not contain an entry for the current platform, the fetcher
    /// falls back to the normal upstream discovery logic. Set to <c>false</c> to skip the mirror entirely.
    /// </summary>
    public bool UseMirror { get; set; }

    /// <summary>
    /// URL of the mirror manifest JSON. Defaults to the official TorSharp.Mirror at
    /// <c>https://github.com/nefarius/TorSharp.Mirror/releases/latest/download/manifest.json</c>.
    /// Override this to point at a self-hosted mirror that serves a compatible manifest.
    /// Ignored when <see cref="UseMirror"/> is <c>false</c>.
    /// </summary>
    public string? MirrorManifestUrl { get; set; }

    /// <summary>
    /// The operating system that TorSharp should assume it is running on. Automatically detected via
    /// <see cref="RuntimeInformation"/>.
    /// </summary>
    public TorSharpOSPlatform OSPlatform { get; set; }

    /// <summary>
    /// The CPU architecture that TorSharp should assume it is running on. Automatically detected using the
    /// architecture of the process.
    /// </summary>
    public TorSharpArchitecture Architecture { get; set; }

    /// <summary>
    /// How long to wait for each tool to accept connections while starting up. This allows for the tools to start
    /// up before completing the initialization process. Defaults to 5 seconds. This can be completely disabled by
    /// setting this property to <see cref="TimeSpan.Zero"/>.
    /// </summary>
    public TimeSpan WaitForConnect { get; set; }

    /// <summary>
    /// Write the output of the underlying tools (Tor, Privoxy) to this process's stdout and stderr. Defaults to true.
    /// Use the <see cref="ITorSharpProxy.OutputDataReceived"/> and <see cref="ITorSharpProxy.ErrorDataReceived"/> to
    /// listen the output yourself.
    /// </summary>
    public bool WriteToConsole { get; set; } = true;

    /// <summary>
    /// Settings specific to Privoxy.
    /// </summary>
    public TorSharpPrivoxySettings PrivoxySettings { get; set; }

    /// <summary>
    /// Settings specific to Tor.
    /// </summary>
    public TorSharpTorSettings TorSettings { get; set; }

    [Obsolete("Use the " + nameof(TorSharpPrivoxySettings) + "." + nameof(TorSharpPrivoxySettings.Port) + " property instead.")]
    public int PrivoxyPort
    {
        get => PrivoxySettings?.Port ?? TorSharpPrivoxySettings.DefaultPort;
        set => EnsurePrivoxySettings().Port = value;
    }

    [Obsolete("Use the " + nameof(TorSharpTorSettings) + "." + nameof(TorSharpTorSettings.SocksPort) + " property instead.")]
    public int TorSocksPort
    {
        get => TorSettings?.SocksPort ?? TorSharpTorSettings.DefaultSocksPort;
        set => EnsureTorSettings().SocksPort = value;
    }

    [Obsolete("Use the " + nameof(TorSharpTorSettings) + "." + nameof(TorSharpTorSettings.ControlPort) + " property instead.")]
    public int TorControlPort
    {
        get => TorSettings?.ControlPort ?? TorSharpTorSettings.DefaultControlPort;
        set => EnsureTorSettings().ControlPort = value;
    }

    [Obsolete("Use the " + nameof(TorSharpTorSettings) + "." + nameof(TorSharpTorSettings.ExitNodes) + " property instead.")]
    public string? TorExitNodes
    {
        get => TorSettings?.ExitNodes;
        set => EnsureTorSettings().ExitNodes = value;
    }

    [Obsolete("Use the " + nameof(TorSharpTorSettings) + "." + nameof(TorSharpTorSettings.StrictNodes) + " property instead.")]
    public bool? TorStrictNodes
    {
        get => TorSettings?.StrictNodes;
        set => EnsureTorSettings().StrictNodes = value;
    }

    [Obsolete("Use the " + nameof(TorSharpTorSettings) + "." + nameof(TorSharpTorSettings.ControlPassword) + " property instead.")]
    public string? TorControlPassword
    {
        get => TorSettings?.ControlPassword;
        set => EnsureTorSettings().ControlPassword = value;
    }

    [Obsolete("Use the " + nameof(TorSharpTorSettings) + "." + nameof(TorSharpTorSettings.HashedControlPassword) + " property instead.")]
    public string? HashedTorControlPassword
    {
        get => TorSettings?.HashedControlPassword;
        set => EnsureTorSettings().HashedControlPassword = value;
    }

    [Obsolete("Use the " + nameof(TorSharpTorSettings) + "." + nameof(TorSharpTorSettings.DataDirectory) + " property instead.")]
    public string? TorDataDirectory
    {
        get => TorSettings?.DataDirectory;
        set => EnsureTorSettings().DataDirectory = value;
    }

    private TorSharpTorSettings EnsureTorSettings()
    {
        TorSettings ??= new TorSharpTorSettings();
        return TorSettings;
    }

    private TorSharpPrivoxySettings EnsurePrivoxySettings()
    {
        PrivoxySettings ??= new TorSharpPrivoxySettings();
        return PrivoxySettings;
    }

    internal void RejectRuntime(string action)
    {
        var message = new StringBuilder();
        message.Append($"Cannot {action} on {OSPlatform} OS and {Architecture} architecture.");
        message.Append($" OS description: {RuntimeInformation.OSDescription}.");
        throw new TorSharpException(message.ToString());
    }
}
