using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.TorSharp.Tools;

internal static class FetcherHelpers
{
    public static Task<string> GetStringAsync(
        this HttpClient httpClient,
        Uri requestUri,
        CancellationToken token)
    {
        return HttpHelpers.RetryAsync(async ct =>
        {
            // Cap each discovery request independently so a slow/unresponsive host
            // does not block for the full HttpClient.Timeout (100 s default).
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(HttpHelpers.DiscoveryTimeout);

            using var request = HttpHelpers.BuildGet(requestUri);
            using var response = await httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }, token);
    }

    public static async Task<DownloadableFile?> GetLatestDownloadableFileAsync(
        HttpClient httpClient,
        Uri baseUrl,
        string fileNamePattern,
        ZippedToolFormat format,
        CancellationToken token)
    {
        var versionsContent = await httpClient.GetStringAsync(baseUrl, token).ConfigureAwait(false);
        var versionInfos = GetLinks(versionsContent)
            .Select(x => new { Link = x, Version = GetVersion(x) })
            .Where(x => x.Version != null)
            .OrderByDescending(x => x.Version);

        foreach (var versionInfo in versionInfos)
        {
            var listUrl = new Uri(baseUrl, versionInfo.Link);
            var listContent = await httpClient.GetStringAsync(listUrl, token).ConfigureAwait(false);

            foreach (var link in GetLinks(listContent))
            {
                var match = Regex.Match(link, fileNamePattern, RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    continue;
                }

                if (!Version.TryParse(match.Groups["Version"].Value, out var parsedVersion))
                {
                    continue;
                }

                var downloadUrl = new Uri(listUrl, link);
                return new DownloadableFile(parsedVersion, downloadUrl, format);
            }
        }

        return null;
    }

    private static Version? GetVersion(string link)
    {
        var last = Regex
            .Matches(link, @"(?<Version>\d+(?:\.\d+)+)( |%20|/)")
            .OfType<Match>()
            .LastOrDefault();

        if (last == null)
        {
            return null;
        }

        return Version.TryParse(last.Groups["Version"].Value, out var version) ? version : null;
    }

    private static IEnumerable<string> GetLinks(string content)
    {
        return Regex
            .Matches(content, @"<a[^>]+?href=""(?<Link>[^""]+)"">")
            .OfType<Match>()
            .Select(x => x.Groups["Link"].Value);
    }
}
