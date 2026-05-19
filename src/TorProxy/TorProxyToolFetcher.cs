using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Nefarius.Utilities.TorProxy.Tools;
using Nefarius.Utilities.TorProxy.Tools.Privoxy;
using Nefarius.Utilities.TorProxy.Tools.Tor;

namespace Nefarius.Utilities.TorProxy;

public interface ITorProxyToolFetcher
{
    Task<ToolUpdates> CheckForUpdatesAsync();
    Task FetchAsync();
    Task FetchAsync(ToolUpdates updates);
}

/// <summary>
/// Fetch the latest version of the tools from the internet.
/// </summary>
public class TorProxyToolFetcher : ITorProxyToolFetcher
{
    private static bool SecureProtocolsEnabled = false;

    private readonly TorProxySettings _settings;
    private readonly ISimpleHttpClient _simpleHttpClient;
    private readonly IProgress<DownloadProgress>? _progress;
    private readonly PrivoxyFetcher _privoxyFetcher;
    private readonly TorFetcher _torFetcher;
    private readonly MirrorManifestFetcher _mirrorFetcher;

    public TorProxyToolFetcher(TorProxySettings settings, HttpClient client)
        : this(settings, client, new SimpleHttpClient(client), progress: null)
    {
    }

    internal TorProxyToolFetcher(
        TorProxySettings settings,
        HttpClient client,
        ISimpleHttpClient simpleHttpClient,
        IProgress<DownloadProgress>? progress)
    {
        _settings = settings;
        _simpleHttpClient = simpleHttpClient;
        _progress = progress;
        _privoxyFetcher = new PrivoxyFetcher(settings, client);
        _torFetcher = new TorFetcher(settings, client);
        _mirrorFetcher = new MirrorManifestFetcher(client, settings);
    }

    /// <summary>
    /// Checks for updates of the tools and returns what is found. Inspect the
    /// <see cref="ToolUpdates.HasUpdate"/> property to determine whether updates are available. This is
    /// determined by checking the the latest versions are already downloads to the
    /// <see cref="TorProxySettings.ZippedToolsDirectory"/>.
    /// </summary>
    public async Task<ToolUpdates> CheckForUpdatesAsync()
    {
        var updates = await CheckForUpdatesAsync(allowExistingTools: false).ConfigureAwait(false);
        return new ToolUpdates(updates.Privoxy, updates.Tor!);
    }

    private async Task<PartialToolUpdates> CheckForUpdatesAsync(bool allowExistingTools)
    {
        EnableSecurityProtocols();

        // Skip the network round-trip to the mirror when all required tools are
        // already present locally and the caller has opted into reusing them.
        var privoxyExists = _settings.PrivoxySettings.Disable
            || ToolUtility.GetLatestToolOrNull(_settings, ToolUtility.GetPrivoxyToolSettings(_settings)) != null;
        var torExists =
            ToolUtility.GetLatestToolOrNull(_settings, ToolUtility.GetTorToolSettings(_settings)) != null;
        var allToolsPresent = allowExistingTools && privoxyExists && torExists;

        // Try to fetch the mirror manifest once for both tools.
        MirrorManifest? mirrorManifest = null;
        if (!allToolsPresent && _settings.UseMirror && !string.IsNullOrWhiteSpace(_settings.MirrorManifestUrl))
        {
            mirrorManifest = await _mirrorFetcher.TryGetManifestAsync().ConfigureAwait(false);
        }

        ToolUpdate? privoxy = null;
        if (!_settings.PrivoxySettings.Disable)
        {
            var mirrorFile = mirrorManifest != null
                ? MirrorManifestFetcher.GetPrivoxyEntry(mirrorManifest, _settings)
                : null;

            privoxy = await CheckForUpdateAsync(
                ToolUtility.GetPrivoxyToolSettings(_settings),
                new FallbackFileFetcher(mirrorFile, _privoxyFetcher),
                allowExistingTools).ConfigureAwait(false);
        }

        var torMirrorFile = mirrorManifest != null
            ? MirrorManifestFetcher.GetTorEntry(mirrorManifest, _settings)
            : null;

        var tor = await CheckForUpdateAsync(
            ToolUtility.GetTorToolSettings(_settings),
            new FallbackFileFetcher(torMirrorFile, _torFetcher),
            allowExistingTools).ConfigureAwait(false);

        return new PartialToolUpdates
        {
            Privoxy = privoxy,
            Tor = tor,
        };
    }

    private async Task<ToolUpdate?> CheckForUpdateAsync(
        ToolSettings toolSettings,
        IFileFetcher fetcher,
        bool useExistingTools)
    {
        var latestLocal = ToolUtility.GetLatestToolOrNull(_settings, toolSettings);
        if (useExistingTools && latestLocal != null)
        {
            return null;
        }

        var latestDownload = await fetcher.GetLatestAsync().ConfigureAwait(false);
        var fileExtension = ArchiveUtility.GetFileExtension(latestDownload.Format);
        var fileName = $"{toolSettings.Prefix}{latestDownload.Version}{fileExtension}";
        var destinationPath = Path.Combine(_settings.ZippedToolsDirectory, fileName);

        ToolUpdateStatus status;
        if (latestLocal == null)
        {
            status = ToolUpdateStatus.NoLocalVersion;
        }
        else if (!File.Exists(destinationPath))
        {
            status = ToolUpdateStatus.NewerVersionAvailable;
        }
        else
        {
            status = ToolUpdateStatus.NoUpdateAvailable;
        }

        return new ToolUpdate(status, latestLocal?.Version, destinationPath, latestDownload);
    }

    /// <summary>
    /// Downloads the latest version of the tools to the configured
    /// <see cref="TorProxySettings.ZippedToolsDirectory"/> given the tool updates already discovered. This should
    /// be called after getting the result of <see cref="CheckForUpdatesAsync()"/>.
    /// </summary>
    public async Task FetchAsync(ToolUpdates updates)
    {
        if (updates == null)
        {
            throw new ArgumentNullException(nameof(updates));
        }

        if (updates.Privoxy != null)
        {
            await DownloadFileAsync(updates.Privoxy).ConfigureAwait(false);
        }

        await DownloadFileAsync(updates.Tor).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads the latest version of the tools to the configured
    /// <see cref="TorProxySettings.ZippedToolsDirectory"/>. This method always tries to download the latest
    /// version of the tools unless <see cref="TorProxySettings.UseExistingTools"/> is set to true.
    /// </summary>
    public async Task FetchAsync()
    {
        var updates = await CheckForUpdatesAsync(_settings.UseExistingTools).ConfigureAwait(false);

        if (updates.Privoxy != null)
        {
            await DownloadFileAsync(updates.Privoxy).ConfigureAwait(false);
        }

        if (updates.Tor != null)
        {
            await DownloadFileAsync(updates.Tor).ConfigureAwait(false);
        }
    }

    private async Task DownloadFileAsync(ToolUpdate update)
    {
        if (update.Status != ToolUpdateStatus.NoUpdateAvailable || _settings.ReloadTools)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(update.DestinationPath)!);

            try
            {
                await _simpleHttpClient.DownloadToFileAsync(
                    update.LatestDownload.Url,
                    update.DestinationPath,
                    _progress).ConfigureAwait(false);

                try
                {
                    await ArchiveUtility.TestAsync(
                        update.LatestDownload.Format,
                        update.DestinationPath).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new TorProxyException(
                        $"The tool downloaded from '{update.LatestDownload.Url.AbsoluteUri}' could not be read as a " +
                        $"{ArchiveUtility.GetFileExtension(update.LatestDownload.Format)} file.", ex);
                }

                // Verify SHA256 when the manifest provided one (mirror path).
                var expectedSha256 = update.LatestDownload.Sha256;
                if (!string.IsNullOrEmpty(expectedSha256))
                {
                    var actualSha256 = ComputeSha256(update.DestinationPath);
                    if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new TorProxyException(
                            $"SHA256 mismatch for '{update.DestinationPath}' downloaded from " +
                            $"'{update.LatestDownload.Url.AbsoluteUri}'." +
                            $" Expected: {expectedSha256}. Actual: {actualSha256}.");
                    }
                }
            }
            catch
            {
                try
                {
                    File.Delete(update.DestinationPath);
                }
                catch
                {
                    // Best effort.
                }

                throw;
            }
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private void EnableSecurityProtocols()
    {
        // Enable all security protocols for older platforms. On modern .NET (8+) HttpClient manages this
        // automatically and ServicePointManager has no effect on SocketsHttpHandler.
        if (_settings.EnableSecurityProtocolsForFetcher && !SecureProtocolsEnabled)
        {
#if NETSTANDARD
            var protocols = new[]
            {
                SecurityProtocolType.Tls,
                SecurityProtocolType.Tls11,
                SecurityProtocolType.Tls12,
            };

            foreach (var protocol in protocols)
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= protocol;
                }
                catch (NotSupportedException)
                {
                    // Not much we can do if the protocol isn't supported. Move on and try the next one.
                }
            }
#endif
            SecureProtocolsEnabled = true;
        }
    }

    private class PartialToolUpdates
    {
        public ToolUpdate? Privoxy { get; set; }
        public ToolUpdate? Tor { get; set; }
    }

    /// <summary>
    /// Returns the pre-resolved mirror entry immediately if available, otherwise delegates
    /// to the upstream <see cref="IFileFetcher"/>. This keeps all fallback logic in one place
    /// and avoids making the mirror a hot-path dependency.
    /// </summary>
    private class FallbackFileFetcher : IFileFetcher
    {
        private readonly DownloadableFile? _mirrorFile;
        private readonly IFileFetcher _upstream;

        public FallbackFileFetcher(DownloadableFile? mirrorFile, IFileFetcher upstream)
        {
            _mirrorFile = mirrorFile;
            _upstream = upstream;
        }

        public Task<DownloadableFile> GetLatestAsync()
        {
            return _mirrorFile != null
                ? Task.FromResult(_mirrorFile)
                : _upstream.GetLatestAsync();
        }
    }
}
