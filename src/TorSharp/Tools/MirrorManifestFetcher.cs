using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.TorSharp.Tools;

/// <summary>
/// Fetches and deserializes the TorSharp mirror manifest, then resolves
/// <see cref="DownloadableFile"/> entries for Tor and Privoxy based on the
/// current <see cref="TorSharpSettings"/> platform / architecture.
/// </summary>
internal class MirrorManifestFetcher
{
    private readonly HttpClient _httpClient;
    private readonly TorSharpSettings _settings;

    public MirrorManifestFetcher(HttpClient httpClient, TorSharpSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    /// <summary>
    /// Attempts to download and parse the mirror manifest. Returns <c>null</c> on any
    /// failure so the caller can fall back to upstream discovery without crashing.
    /// </summary>
    public async Task<MirrorManifest?> TryGetManifestAsync(CancellationToken cancellationToken = default)
    {
        var url = _settings.MirrorManifestUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", HttpHelpers.UserAgent);

            using var response = await _httpClient.SendAsync(
                request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<MirrorManifest>(json, MirrorJsonContext.Default.MirrorManifest);
        }
        catch
        {
            return null;
        }
    }

    // -----------------------------------------------------------------------
    // Static helpers — map TorSharpSettings platform/arch to manifest keys
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns a <see cref="DownloadableFile"/> for Tor from the manifest, or <c>null</c>
    /// if the current platform/architecture is not present in the manifest.
    /// </summary>
    public static DownloadableFile? GetTorEntry(MirrorManifest manifest, TorSharpSettings settings)
    {
        var key = GetTorKey(settings);
        if (key == null || manifest.Tor == null || !manifest.Tor.TryGetValue(key, out var entry))
        {
            return null;
        }

        return ToDownloadableFile(entry, ZippedToolFormat.TarGz);
    }

    /// <summary>
    /// Returns a <see cref="DownloadableFile"/> for Privoxy from the manifest, or <c>null</c>
    /// if the current platform/architecture is not present in the manifest.
    /// </summary>
    public static DownloadableFile? GetPrivoxyEntry(MirrorManifest manifest, TorSharpSettings settings)
    {
        var key = GetPrivoxyKey(settings);
        if (key == null || manifest.Privoxy == null || !manifest.Privoxy.TryGetValue(key, out var entry))
        {
            return null;
        }

        var format = settings.OSPlatform == TorSharpOSPlatform.Windows
            ? ZippedToolFormat.Zip
            : ZippedToolFormat.Deb;

        return ToDownloadableFile(entry, format);
    }

    private static string? GetTorKey(TorSharpSettings settings)
    {
        var os = settings.OSPlatform switch
        {
            TorSharpOSPlatform.Windows => "windows",
            TorSharpOSPlatform.Linux => "linux",
            _ => null,
        };
        if (os == null) return null;

        var arch = settings.Architecture switch
        {
            TorSharpArchitecture.X86 => "x86",
            TorSharpArchitecture.X64 => "x86_64",
            _ => null,
        };
        if (arch == null) return null;

        return $"{os}-{arch}";
    }

    private static string? GetPrivoxyKey(TorSharpSettings settings)
    {
        if (settings.OSPlatform == TorSharpOSPlatform.Windows)
        {
            return "windows";
        }

        if (settings.OSPlatform == TorSharpOSPlatform.Linux)
        {
            return settings.Architecture switch
            {
                TorSharpArchitecture.X86 => "linux-i386",
                TorSharpArchitecture.X64 => "linux-amd64",
                _ => null,
            };
        }

        return null;
    }

    private static DownloadableFile? ToDownloadableFile(MirrorEntry entry, ZippedToolFormat format)
    {
        if (!Version.TryParse(entry.Version, out var version))
        {
            return null;
        }

        if (!Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return new DownloadableFile(version, uri, format, entry.Sha256);
    }
}

// ---------------------------------------------------------------------------
// Mirror manifest model
// ---------------------------------------------------------------------------

internal class MirrorManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("generatedAt")]
    public string? GeneratedAt { get; set; }

    [JsonPropertyName("tor")]
    public Dictionary<string, MirrorEntry>? Tor { get; set; }

    [JsonPropertyName("privoxy")]
    public Dictionary<string, MirrorEntry>? Privoxy { get; set; }
}

internal class MirrorEntry
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    [JsonPropertyName("runtimeDeps")]
    public string[]? RuntimeDeps { get; set; }
}

// Source-generated JSON context for AOT/trimming friendliness
[JsonSerializable(typeof(MirrorManifest))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class MirrorJsonContext : JsonSerializerContext
{
}
