using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.TorSharp.Tools.Tor;

internal class TorFetcher : IFileFetcher
{
    private static readonly Uri BaseUrl = new Uri("https://dist.torproject.org/torbrowser/");
    private readonly TorSharpSettings _settings;
    private readonly HttpClient _httpClient;

    public TorFetcher(TorSharpSettings settings, HttpClient httpClient)
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
            downloadableFile = await HttpHelpers.RetryAsync(ct =>
                FetcherHelpers.GetLatestDownloadableFileAsync(
                    _httpClient,
                    BaseUrl,
                    fileNamePatternAndFormat.Pattern,
                    fileNamePatternAndFormat.Format,
                    ct)).ConfigureAwait(false);
        }
        catch (TorSharpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TorSharpException(
                $"Could not fetch the Tor version list from {BaseUrl.AbsoluteUri}: {ex.Message}", ex);
        }

        if (downloadableFile == null)
        {
            throw new TorSharpException(
                $"No version of Tor could be found under base URL {BaseUrl.AbsoluteUri} with pattern " +
                $"{fileNamePatternAndFormat.Pattern}.");
        }

        return downloadableFile;
    }

    private FileNamePatternAndFormat GetFileNamePatternAndFormat()
    {
        string? pattern = null;
        var format = default(ZippedToolFormat);

        if (_settings.OSPlatform == TorSharpOSPlatform.Windows)
        {
            pattern = _settings.Architecture switch
            {
                TorSharpArchitecture.X86 => @"tor-expert-bundle-windows-i686-(?<Version>[\d\.]+)\.tar\.gz$",
                TorSharpArchitecture.X64 => @"tor-expert-bundle-windows-x86_64-(?<Version>[\d\.]+)\.tar\.gz$",
                _ => null,
            };
            format = ZippedToolFormat.TarGz;
        }
        else if (_settings.OSPlatform == TorSharpOSPlatform.Linux)
        {
            pattern = _settings.Architecture switch
            {
                TorSharpArchitecture.X86 => @"tor-expert-bundle-linux-i686-(?<Version>[\d\.]+)\.tar\.gz$",
                TorSharpArchitecture.X64 => @"tor-expert-bundle-linux-x86_64-(?<Version>[\d\.]+)\.tar\.gz$",
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
