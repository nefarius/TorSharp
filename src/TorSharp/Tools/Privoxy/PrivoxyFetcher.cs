using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Knapcode.TorSharp.Tools.Privoxy;

internal class PrivoxyFetcher : IFileFetcher
{
    // silvester.org.uk is the most reliable upstream source for Privoxy packages.
    // privoxy.org's RSS feed returns HTTP 500 intermittently, and SourceForge blocks
    // automated clients behind Cloudflare. Both are therefore removed from the default
    // source set; silvester.org.uk is the sole upstream fallback when the mirror is
    // unavailable.
    private static readonly Uri PrivoxyMirrorBaseUrl = new Uri("https://www.silvester.org.uk/privoxy/");

    private readonly HttpClient _httpClient;
    private readonly TorSharpSettings _settings;

    public PrivoxyFetcher(TorSharpSettings settings, HttpClient httpClient)
    {
        _settings = settings;
        _httpClient = httpClient;
    }

    public async Task<DownloadableFile> GetLatestAsync()
    {
        DownloadableFile? result;
        try
        {
            result = await GetLatestOrNullFromFileListingAsync(PrivoxyMirrorBaseUrl, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (TorSharpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Wrap transport errors (timeout, DNS, TCP, HTTP errors) so callers that
            // handle TorSharpException (e.g. skipOnExceptions in RetryTheory) can
            // react uniformly instead of getting a raw TaskCanceledException.
            throw new TorSharpException(
                $"Could not fetch the Privoxy version list from {PrivoxyMirrorBaseUrl}: {ex.Message}", ex);
        }

        if (result == null)
        {
            throw new TorSharpException(
                $"No version of Privoxy could be found. " +
                $"The upstream source at {PrivoxyMirrorBaseUrl} did not return a matching package.");
        }

        return result;
    }

    private async Task<DownloadableFile?> GetLatestOrNullFromFileListingAsync(Uri baseUrl, CancellationToken token)
    {
        var directory = GetFileListingDirectory(baseUrl);
        var osBaseUrl = new Uri(baseUrl, $"{directory}/");
        var fileNamePatternAndFormat = _settings.PrivoxyFilePatternResolver?.Invoke(_settings, osBaseUrl)
            ?? GetFileNamePatternAndFormat(osBaseUrl);

        return await FetcherHelpers.GetLatestDownloadableFileAsync(
            _httpClient,
            osBaseUrl,
            fileNamePatternAndFormat.Pattern,
            fileNamePatternAndFormat.Format,
            token).ConfigureAwait(false);
    }

    private string GetFileListingDirectory(Uri baseUrl)
    {
        string? directory = null;
        if (_settings.OSPlatform == TorSharpOSPlatform.Windows)
        {
            directory = "Windows";
        }
        else if (_settings.OSPlatform == TorSharpOSPlatform.Linux)
        {
            directory = "Debian";
        }

        if (directory == null)
        {
            _settings.RejectRuntime($"fetch Privoxy from {baseUrl.AbsoluteUri}");
        }

        return directory!;
    }

    private FileNamePatternAndFormat GetFileNamePatternAndFormat(Uri baseUrl)
    {
        string? pattern = null;
        var format = default(ZippedToolFormat);

        if (_settings.OSPlatform == TorSharpOSPlatform.Windows)
        {
            pattern = @"privoxy[-_](?<Version>[\d\.]+)\.zip$";
            format = ZippedToolFormat.Zip;
        }
        else if (_settings.OSPlatform == TorSharpOSPlatform.Linux)
        {
            if (_settings.Architecture == TorSharpArchitecture.X86)
            {
                pattern = @"privoxy[-_](?<Version>[\d\.]+)([-_]\d(~pp\+\d)?_)?i386\.deb$";
                format = ZippedToolFormat.Deb;
            }
            else if (_settings.Architecture == TorSharpArchitecture.X64)
            {
                pattern = @"privoxy[-_](?<Version>[\d\.]+)([-_]\d(~pp\+\d)?_)?amd64\.deb$";
                format = ZippedToolFormat.Deb;
            }
        }

        if (pattern == null)
        {
            _settings.RejectRuntime($"fetch Privoxy from {baseUrl.AbsoluteUri}");
        }

        return new FileNamePatternAndFormat(pattern!, format);
    }
}
