using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Knapcode.TorSharp.Tests.TestSupport
{
    /// <summary>
    /// A <see cref="DelegatingHandler"/> that caches HTTP GET responses (status 200) on
    /// disk, keyed by a SHA1 of the request URL.  Requests other than GET, or non-200
    /// responses, are forwarded to the inner handler and not cached.
    ///
    /// This is used in tests to avoid hammering upstream servers (Tor dist, silvester.org.uk,
    /// the mirror) on every test run while still exercising real response parsing.
    /// </summary>
    internal sealed class CachingHttpClientHandler : DelegatingHandler
    {
        private static readonly object _globalLock = new object();
        private static readonly Dictionary<string, SemaphoreSlim> _pathToSemaphore
            = new Dictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        private readonly ITestOutputHelper _output;
        private readonly string _cacheDirectory;

        public CachingHttpClientHandler(
            ITestOutputHelper output,
            string cacheDirectory,
            HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            _output = output;
            _cacheDirectory = cacheDirectory;
            Directory.CreateDirectory(cacheDirectory);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Only cache plain GET requests
            if (request.Method != HttpMethod.Get)
            {
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            var cachePath = GetCachePath(request.RequestUri!.AbsoluteUri);
            var semaphore = GetSemaphore(cachePath);

            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (File.Exists(cachePath))
                {
                    _output.WriteLine($"[CachingHandler] HIT  {request.RequestUri}");
                    var cachedBytes = File.ReadAllBytes(cachePath);
                    return BuildCachedResponse(request, cachedBytes);
                }

                _output.WriteLine($"[CachingHandler] MISS {request.RequestUri}");
                var liveResponse = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (liveResponse.StatusCode == HttpStatusCode.OK)
                {
                    var bytes = await liveResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    File.WriteAllBytes(cachePath, bytes);
                    // Re-wrap so the content can be read again
                    return BuildCachedResponse(request, bytes);
                }

                return liveResponse;
            }
            finally
            {
                semaphore.Release();
            }
        }

        private string GetCachePath(string url)
        {
            using var sha1 = SHA1.Create();
            var hash = BitConverter
                .ToString(sha1.ComputeHash(Encoding.UTF8.GetBytes(url)))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
            return Path.Combine(_cacheDirectory, hash);
        }

        private static SemaphoreSlim GetSemaphore(string path)
        {
            lock (_globalLock)
            {
                if (!_pathToSemaphore.TryGetValue(path, out var sem))
                {
                    sem = new SemaphoreSlim(1, 1);
                    _pathToSemaphore[path] = sem;
                }
                return sem;
            }
        }

        private static HttpResponseMessage BuildCachedResponse(HttpRequestMessage request, byte[] bytes)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(bytes),
            };
            return response;
        }
    }
}
