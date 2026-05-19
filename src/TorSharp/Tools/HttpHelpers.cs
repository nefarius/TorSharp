using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.TorSharp.Tools;

internal static class HttpHelpers
{
    private static readonly string _version = GetAssemblyVersion();

    /// <summary>
    /// User-Agent sent with all outbound requests so upstream servers can identify
    /// the client and operators can debug rate-limiting issues.
    /// </summary>
    public static string UserAgent { get; } =
        $"TorSharp/{_version} (+https://github.com/nefarius/TorSharp)";

    /// <summary>
    /// Maximum number of attempts for retryable HTTP requests.
    /// </summary>
    private const int MaxAttempts = 3;

    /// <summary>
    /// Per-request wall-clock timeout applied to discovery (HTML/RSS scraping) calls in
    /// <see cref="FetcherHelpers"/>. Kept well below <see cref="HttpClient.Timeout"/> so
    /// an unresponsive upstream fails fast rather than blocking for the full client timeout.
    /// </summary>
    internal static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Executes <paramref name="action"/> with up to <see cref="MaxAttempts"/> retries
    /// on transient HTTP / IO failures (5xx, 408, 429, network errors).
    /// </summary>
    public static async Task<T> RetryAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await action(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsRetryable(ex))
            {
                lastException = ex;
                // Exponential back-off: 1s, 2s, …  (capped at 16s)
                var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt - 1), 16));
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw lastException!;
    }

    /// <summary>
    /// Builds a <see cref="HttpRequestMessage"/> GET with the shared User-Agent header set.
    /// </summary>
    public static HttpRequestMessage BuildGet(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        return request;
    }

    private static bool IsRetryable(Exception ex)
    {
        if (ex is IOException) return true;
        if (ex is OperationCanceledException) return false;

        if (ex is HttpRequestException hre)
        {
#if NET5_0_OR_GREATER
            // StatusCode was added in .NET 5.  When present it means the server
            // returned a response; use it to decide.  When absent the exception
            // is a network-level failure (DNS, TCP, TLS) which is always retryable.
            return hre.StatusCode.HasValue
                ? IsRetryableStatusCode(hre.StatusCode.Value)
                : true;
#else
            // netstandard2.0: StatusCode is not available.  Conservatively
            // treat as non-retryable to avoid masking permanent 4xx errors.
            return false;
#endif
        }

        if (ex is HttpListenerException hle)
        {
            return hle.ErrorCode == 995; // IO cancelled
        }

        return false;
    }

    /// <summary>
    /// Executes the non-generic (void) async action with up to <see cref="MaxAttempts"/> retries.
    /// </summary>
    public static async Task RetryAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        await RetryAsync<bool>(async ct =>
        {
            await action(ct).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsRetryableStatusCode(HttpStatusCode code)
    {
        return (int)code == 429                             // TooManyRequests (added .NET 5+)
            || code == HttpStatusCode.RequestTimeout        // 408
            || code == HttpStatusCode.BadGateway            // 502
            || code == HttpStatusCode.ServiceUnavailable    // 503
            || code == HttpStatusCode.GatewayTimeout        // 504
            || (int)code >= 500;
    }

    private static string GetAssemblyVersion()
    {
        try
        {
            return typeof(HttpHelpers).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?? typeof(HttpHelpers).Assembly.GetName().Version?.ToString()
                ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
