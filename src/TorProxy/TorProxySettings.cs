using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Nefarius.Utilities.TorProxy.Tools;

namespace Nefarius.Utilities.TorProxy;

/// <summary>
/// Settings for how TorProxy behaves.
/// </summary>
public class TorProxySettings
{
    public static readonly string DefaultToolsDirectory = Path.Combine(Path.GetTempPath(), "Nefarius.Utilities.TorProxy");

    public const string DefaultMirrorManifestUrl =
        "https://github.com/nefarius/TorSharp.Mirror/releases/latest/download/manifest.json";

    public TorProxySettings()
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
            OSPlatform = TorProxyOSPlatform.Windows;
        }
        else if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
        {
            OSPlatform = TorProxyOSPlatform.Linux;
        }
        else
        {
            OSPlatform = TorProxyOSPlatform.Unknown;
        }

        Architecture = RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X86 => TorProxyArchitecture.X86,
            System.Runtime.InteropServices.Architecture.X64 => TorProxyArchitecture.X64,
            _ => TorProxyArchitecture.Unknown,
        };

        PrivoxySettings = new TorProxyPrivoxySettings();
        TorSettings = new TorProxyTorSettings();
    }

    /// <summary>
    /// If true, the tools (Tor and Privoxy) will be re-downloaded by <see cref="TorProxyToolFetcher"/> even if the
    /// latest version is already downloaded and will be re-extracted by <see cref="TorProxy"/> even if the
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
    /// and the TorProxy code hasn't been updated yet.
    /// </summary>
    public Func<TorProxySettings, Uri, FileNamePatternAndFormat>? PrivoxyFilePatternResolver { get; set; }

    /// <summary>
    /// Override the file pattern used for downloading a Tor binary from a given URL. The settings instance will
    /// be this instance itself, for convenience. If this delegate returns null or if the delegate itself is set to
    /// null, the default pattern is used. This is useful when the remote tool distribution changes the file pattern
    /// and the TorProxy code hasn't been updated yet.
    /// </summary>
    public Func<TorProxySettings, Uri, FileNamePatternAndFormat>? TorFilePatternResolver { get; set; }

    /// <summary>
    /// The directory to download the zipped tools. This defaults to <c>%TEMP%\Nefarius.Utilities.TorProxy\ZippedTools</c>.
    /// </summary>
    public string ZippedToolsDirectory { get; set; }

    /// <summary>
    /// The directory to extract the tools to and run them from.  This defaults to
    /// <c>%TEMP%\Nefarius.Utilities.TorProxy\ExtractedTools</c>.
    /// </summary>
    public string ExtractedToolsDirectory { get; set; }

    /// <summary>
    /// Instead of downloading the latest version of the tools in <see cref="TorProxyToolFetcher"/>, use any
    /// existing tools already downloaded to <see cref="ZippedToolsDirectory"/>.
    /// </summary>
    public bool UseExistingTools { get; set; }

    /// <summary>
    /// What strategy to use when fetching the latest version of a tool. This behavior matters when there are
    /// multiple URLs or feeds that a checked for a single tool.
    /// </summary>
    public ToolDownloadStrategy ToolDownloadStrategy { get; set; }

    /// <summary>
    /// When <c>true</c> (the default), <see cref="TorProxyToolFetcher"/> first tries to resolve tool
    /// versions and download binaries from the mirror specified by <see cref="MirrorManifestUrl"/>.
    /// If the mirror is unreachable or does not contain an entry for the current platform, the fetcher
    /// falls back to the normal upstream discovery logic. Set to <c>false</c> to skip the mirror entirely.
    /// </summary>
    public bool UseMirror { get; set; }

    /// <summary>
    /// URL of the mirror manifest JSON. Defaults to the official external mirror repository
    /// (github.com/nefarius/TorSharp.Mirror) at
    /// <c>https://github.com/nefarius/TorSharp.Mirror/releases/latest/download/manifest.json</c>.
    /// Override this to point at a self-hosted mirror that serves a compatible manifest.
    /// Ignored when <see cref="UseMirror"/> is <c>false</c>.
    /// </summary>
    public string? MirrorManifestUrl { get; set; }

    /// <summary>
    /// The operating system that TorProxy should assume it is running on. Automatically detected via
    /// <see cref="RuntimeInformation"/>.
    /// </summary>
    public TorProxyOSPlatform OSPlatform { get; set; }

    /// <summary>
    /// The CPU architecture that TorProxy should assume it is running on. Automatically detected using the
    /// architecture of the process.
    /// </summary>
    public TorProxyArchitecture Architecture { get; set; }

    /// <summary>
    /// How long to wait for each tool to accept connections while starting up. This allows for the tools to start
    /// up before completing the initialization process. Defaults to 5 seconds. This can be completely disabled by
    /// setting this property to <see cref="TimeSpan.Zero"/>.
    /// </summary>
    public TimeSpan WaitForConnect { get; set; }

    /// <summary>
    /// Write the output of the underlying tools (Tor, Privoxy) to this process's stdout and stderr. Defaults to true.
    /// Use the <see cref="ITorProxy.OutputDataReceived"/> and <see cref="ITorProxy.ErrorDataReceived"/> to
    /// listen the output yourself.
    /// </summary>
    public bool WriteToConsole { get; set; } = true;

    /// <summary>
    /// Settings specific to Privoxy.
    /// </summary>
    public TorProxyPrivoxySettings PrivoxySettings { get; set; }

    /// <summary>
    /// Settings specific to Tor.
    /// </summary>
    public TorProxyTorSettings TorSettings { get; set; }

    [Obsolete("Use the " + nameof(TorProxyPrivoxySettings) + "." + nameof(TorProxyPrivoxySettings.Port) + " property instead.")]
    public int PrivoxyPort
    {
        get => PrivoxySettings?.Port ?? TorProxyPrivoxySettings.DefaultPort;
        set => EnsurePrivoxySettings().Port = value;
    }

    [Obsolete("Use the " + nameof(TorProxyTorSettings) + "." + nameof(TorProxyTorSettings.SocksPort) + " property instead.")]
    public int TorSocksPort
    {
        get => TorSettings?.SocksPort ?? TorProxyTorSettings.DefaultSocksPort;
        set => EnsureTorSettings().SocksPort = value;
    }

    [Obsolete("Use the " + nameof(TorProxyTorSettings) + "." + nameof(TorProxyTorSettings.ControlPort) + " property instead.")]
    public int TorControlPort
    {
        get => TorSettings?.ControlPort ?? TorProxyTorSettings.DefaultControlPort;
        set => EnsureTorSettings().ControlPort = value;
    }

    [Obsolete("Use the " + nameof(TorProxyTorSettings) + "." + nameof(TorProxyTorSettings.ExitNodes) + " property instead.")]
    public string? TorExitNodes
    {
        get => TorSettings?.ExitNodes;
        set => EnsureTorSettings().ExitNodes = value;
    }

    [Obsolete("Use the " + nameof(TorProxyTorSettings) + "." + nameof(TorProxyTorSettings.StrictNodes) + " property instead.")]
    public bool? TorStrictNodes
    {
        get => TorSettings?.StrictNodes;
        set => EnsureTorSettings().StrictNodes = value;
    }

    [Obsolete("Use the " + nameof(TorProxyTorSettings) + "." + nameof(TorProxyTorSettings.ControlPassword) + " property instead.")]
    public string? TorControlPassword
    {
        get => TorSettings?.ControlPassword;
        set => EnsureTorSettings().ControlPassword = value;
    }

    [Obsolete("Use the " + nameof(TorProxyTorSettings) + "." + nameof(TorProxyTorSettings.HashedControlPassword) + " property instead.")]
    public string? HashedTorControlPassword
    {
        get => TorSettings?.HashedControlPassword;
        set => EnsureTorSettings().HashedControlPassword = value;
    }

    [Obsolete("Use the " + nameof(TorProxyTorSettings) + "." + nameof(TorProxyTorSettings.DataDirectory) + " property instead.")]
    public string? TorDataDirectory
    {
        get => TorSettings?.DataDirectory;
        set => EnsureTorSettings().DataDirectory = value;
    }

    private TorProxyTorSettings EnsureTorSettings()
    {
        TorSettings ??= new TorProxyTorSettings();
        return TorSettings;
    }

    private TorProxyPrivoxySettings EnsurePrivoxySettings()
    {
        PrivoxySettings ??= new TorProxyPrivoxySettings();
        return PrivoxySettings;
    }

    internal void RejectRuntime(string action)
    {
        var message = new StringBuilder();
        message.Append($"Cannot {action} on {OSPlatform} OS and {Architecture} architecture.");
        message.Append($" OS description: {RuntimeInformation.OSDescription}.");
        throw new TorProxyException(message.ToString());
    }
}
