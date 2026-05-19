using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.TorSharp.Tools;

internal interface ISimpleHttpClient
{
    Task DownloadToFileAsync(Uri requestUri, string path, IProgress<DownloadProgress>? progress);
}

internal class SimpleHttpClient : ISimpleHttpClient
{
    private readonly HttpClient _httpClient;

    public SimpleHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task DownloadToFileAsync(Uri requestUri, string path, IProgress<DownloadProgress>? progress)
    {
        return HttpHelpers.RetryAsync(
            ct => DownloadCoreAsync(requestUri, path, progress, ct));
    }

    private async Task DownloadCoreAsync(
        Uri requestUri,
        string path,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var request = HttpHelpers.BuildGet(requestUri);
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var fileStream = new FileStream(path, FileMode.Create);

        var downloadId = Guid.NewGuid();
        long totalRead = 0;
        Report(progress, downloadId, DownloadProgressState.Starting, requestUri, totalRead, response);

        var buffer = new byte[81920];
        int read;
        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
        {
            totalRead += read;
            await fileStream.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
            Report(progress, downloadId, DownloadProgressState.Progress, requestUri, totalRead, response);
        }

        Report(progress, downloadId, DownloadProgressState.Complete, requestUri, totalRead, response);
    }

    private static void Report(
        IProgress<DownloadProgress>? progress,
        Guid downloadId,
        DownloadProgressState state,
        Uri requestUri,
        long totalRead,
        HttpResponseMessage response)
    {
        progress?.Report(new DownloadProgress(downloadId, state, requestUri, totalRead, response.Content?.Headers.ContentLength));
    }
}
