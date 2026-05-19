using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nefarius.Utilities.TorProxy.Tools.Tor;

internal class TorFetcher : IFileFetcher
{
    private static readonly Uri BaseUrl = new Uri("https://dist.torproject.org/torbrowser/");
    private readonly TorProxySettings _settings;
    private readonly HttpClient _httpClient;

    public TorFetcher(TorProxySettings settings, HttpClient httpClient)
    {
        _settings = settings;
        _httpClient = httpClient;
    }

    public async Task<DownloadableFile> GetLatestAsync()
    {
        var fileNamePatternAndFormat = _settings.TorFilePatternResolver?.Invoke(_settings, BaseUrl) ?? GetFileNamePatternAndFormat();

        DownloadableFile? downloadableFile;
        try
        {
            // GetLatestDownloadableFileAsync calls GetStringAsync which already
            // applies HttpHelpers.RetryAsync per request; no outer retry needed.
            downloadableFile = await FetcherHelpers.GetLatestDownloadableFileAsync(
                _httpClient,
                BaseUrl,
                fileNamePatternAndFormat.Pattern,
                fileNamePatternAndFormat.Format,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (TorProxyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TorProxyException(
                $"Could not fetch the Tor version list from {BaseUrl.AbsoluteUri}: {ex.Message}", ex);
        }

        if (downloadableFile == null)
        {
            throw new TorProxyException(
                $"No version of Tor could be found under base URL {BaseUrl.AbsoluteUri} with pattern " +
                $"{fileNamePatternAndFormat.Pattern}.");
        }

        return downloadableFile;
    }

    private FileNamePatternAndFormat GetFileNamePatternAndFormat()
    {
        string? pattern = null;
        var format = default(ZippedToolFormat);

        if (_settings.OSPlatform == TorProxyOSPlatform.Windows)
        {
            pattern = _settings.Architecture switch
            {
                TorProxyArchitecture.X86 => @"tor-expert-bundle-windows-i686-(?<Version>[\d\.]+)\.tar\.gz$",
                TorProxyArchitecture.X64 => @"tor-expert-bundle-windows-x86_64-(?<Version>[\d\.]+)\.tar\.gz$",
                _ => null,
            };
            format = ZippedToolFormat.TarGz;
        }
        else if (_settings.OSPlatform == TorProxyOSPlatform.Linux)
        {
            pattern = _settings.Architecture switch
            {
                TorProxyArchitecture.X86 => @"tor-expert-bundle-linux-i686-(?<Version>[\d\.]+)\.tar\.gz$",
                TorProxyArchitecture.X64 => @"tor-expert-bundle-linux-x86_64-(?<Version>[\d\.]+)\.tar\.gz$",
                _ => null,
            };
            format = ZippedToolFormat.TarGz;
        }

        if (pattern == null)
        {
            _settings.RejectRuntime($"fetch Tor from {BaseUrl.AbsoluteUri}");
        }

        return new FileNamePatternAndFormat(pattern!, format);
    }
}
