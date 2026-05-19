using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Knapcode.TorSharp.Tests.TestSupport;
using Knapcode.TorSharp.Tools;
using xRetry;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.TorSharp.Tests
{
    [Collection(HttpCollection.Name)]
    public class TorSharpFetcherTests
    {
        private readonly HttpFixture _httpFixture;
        private readonly ITestOutputHelper _output;

        public TorSharpFetcherTests(HttpFixture httpFixture, ITestOutputHelper output)
        {
            _httpFixture = httpFixture;
            _output = output;
        }

        /// <summary>
        /// Verifies that the mirror manifest is reachable and that resolved URLs actually
        /// come from the mirror host (not from upstream scrapers). The SHA256 presence
        /// further confirms the mirror path was taken — upstream entries never have one.
        /// </summary>
        [RetryFact]
        [DisplayTestMethodName]
        public async Task MirrorOnly_FetchSucceeds()
        {
            using (var te = TestEnvironment.Initialize(_output))
            {
                var settings = te.BuildSettings();
                settings.UseMirror = true;

                using var handler = new HttpClientHandler();
                using var cachingClient = _httpFixture.CreateCachingHttpClient(_output, handler);

                var fetcher = _httpFixture.GetTorSharpToolFetcher(_output, settings, cachingClient);

                // Act
                var updates = await fetcher.CheckForUpdatesAsync();

                // Assert — URLs must point at the mirror, not the upstream scrapers.
                Assert.NotNull(updates);
                Assert.NotNull(updates.Tor);

                var torUrl = updates.Tor!.LatestDownload.Url;
                _output.WriteLine("Mirror Tor URL: " + torUrl.AbsoluteUri);
                Assert.Equal("github.com", torUrl.Host);
                Assert.Contains("/nefarius/TorSharp.Mirror/", torUrl.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
                Assert.NotNull(updates.Tor.LatestDownload.Sha256);

                if (!settings.PrivoxySettings.Disable)
                {
                    Assert.NotNull(updates.Privoxy);
                    var privoxyUrl = updates.Privoxy!.LatestDownload.Url;
                    _output.WriteLine("Mirror Privoxy URL: " + privoxyUrl.AbsoluteUri);
                    Assert.Equal("github.com", privoxyUrl.Host);
                    Assert.Contains("/nefarius/TorSharp.Mirror/", privoxyUrl.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
                    Assert.NotNull(updates.Privoxy.LatestDownload.Sha256);
                }
            }
        }

        /// <summary>
        /// Verifies that the upstream scrapers (silvester.org.uk for Privoxy,
        /// dist.torproject.org for Tor) still return parseable results. Gated by the
        /// <c>TORSHARP_RUN_UPSTREAM_TESTS</c> environment variable so it only runs in
        /// the scheduled CI cron job and not on every commit.
        /// </summary>
        [RetryTheory]
        [MemberData(nameof(Platforms))]
        [DisplayTestMethodName]
        public async Task UpstreamOnly_AllPlatforms_FetchSucceeds(TorSharpOSPlatform osPlatform, TorSharpArchitecture architecture)
        {
            if (!IsUpstreamTestEnabled())
            {
                return;
            }

            using (var te = TestEnvironment.Initialize(_output))
            {
                var settings = te.BuildSettings();
                settings.OSPlatform = osPlatform;
                settings.Architecture = architecture;
                settings.UseMirror = false;   // <-- bypass mirror entirely

                using (var httpClientHandler = new HttpClientHandler())
                using (var loggingHandler = new LoggingHandler(_output) { InnerHandler = httpClientHandler })
                using (var httpClient = new HttpClient(loggingHandler))
                {
                    _output.WriteLine(settings);
                    var fetcher = _httpFixture.GetTorSharpToolFetcher(_output, settings, httpClient);

                    // Act
                    var updates = await fetcher.CheckForUpdatesAsync();

                    // Assert
                    Assert.NotNull(updates);
                    _output.WriteLine("Upstream Tor URL: " + updates.Tor!.LatestDownload.Url.AbsoluteUri);
                    if (updates.Privoxy != null)
                    {
                        _output.WriteLine("Upstream Privoxy URL: " + updates.Privoxy.LatestDownload.Url.AbsoluteUri);
                    }
                }
            }
        }

        /// <summary>
        /// Validates that the upstream silvester.org.uk source is reachable for the
        /// current runtime platform (no mirror bypass needed for this check).
        /// Previously <c>TorSharpToolFetch_AllResultsAreWorking</c> with
        /// <c>skipOnExceptions: typeof(TorSharpException)</c> which silently hid
        /// regressions. The silent skip is removed so genuine failures are surfaced.
        /// </summary>
        [RetryTheory]
        [MemberData(nameof(Platforms))]
        [DisplayTestMethodName]
        public async Task TorSharpToolFetch_AllResultsAreWorking(TorSharpOSPlatform osPlatform, TorSharpArchitecture architecture)
        {
            if (!IsUpstreamTestEnabled())
            {
                return;
            }

            using (var te = TestEnvironment.Initialize(_output))
            {
                // Arrange
                var settings = te.BuildSettings();
                settings.OSPlatform = osPlatform;
                settings.Architecture = architecture;
                settings.UseMirror = false;

                using (var httpClientHandler = new HttpClientHandler())
                using (var loggingHandler = new LoggingHandler(_output) { InnerHandler = httpClientHandler })
                using (var httpClient = new HttpClient(loggingHandler))
                using (var proxy = new TorSharpProxy(settings))
                {
                    _output.WriteLine(settings);
                    var fetcher = _httpFixture.GetTorSharpToolFetcher(_output, settings, httpClient);

                    // Act
                    var updates = await fetcher.CheckForUpdatesAsync();

                    // Assert
                    Assert.NotNull(updates);
                    if (updates.Privoxy != null)
                    {
                        _output.WriteLine("Privoxy URL: " + updates.Privoxy.LatestDownload.Url.AbsoluteUri);
                    }
                    _output.WriteLine("Tor URL: " + updates.Tor!.LatestDownload.Url.AbsoluteUri);
                }
            }
        }

        [RetryTheory(skipOnExceptions: typeof(TorSharpException))]
        [InlineData(ToolDownloadStrategy.First)]
        [InlineData(ToolDownloadStrategy.Latest)]
        [DisplayTestMethodName]
        public async Task TorSharpToolFetcher_CheckForUpdates(ToolDownloadStrategy strategy)
        {
            using (var te = TestEnvironment.Initialize(_output))
            {
                // Arrange
                var settings = te.BuildSettings();
                settings.ToolDownloadStrategy = strategy;

                using (var httpClientHandler = new HttpClientHandler())
                using (var loggingHandler = new LoggingHandler(_output) { InnerHandler = httpClientHandler })
                using (var httpClient = new HttpClient(loggingHandler))
                using (var proxy = new TorSharpProxy(settings))
                {
                    _output.WriteLine(settings);
                    var fetcher = _httpFixture.GetTorSharpToolFetcher(_output, settings, httpClient);
                    var initial = await fetcher.CheckForUpdatesAsync();
                    await fetcher.FetchAsync(initial);

                    var prefix = ToolUtility.GetPrivoxyToolSettings(settings).Prefix;
                    var extension = Path.GetExtension(initial.Privoxy!.DestinationPath);
                    var fakeOldPrivoxy = Path.Combine(settings.ZippedToolsDirectory, $"{prefix}0.0.1{extension}");
                    File.Move(initial.Privoxy.DestinationPath, fakeOldPrivoxy);

                    // Act
                    var newerVersion = await fetcher.CheckForUpdatesAsync();
                    await fetcher.FetchAsync(newerVersion);
                    var upToDate = await fetcher.CheckForUpdatesAsync();

                    // Assert
                    Assert.True(initial.HasUpdate);
                    Assert.Equal(ToolUpdateStatus.NoLocalVersion, initial.Privoxy.Status);
                    Assert.Equal(ToolUpdateStatus.NoLocalVersion, initial.Tor!.Status);

                    Assert.True(newerVersion.HasUpdate);
                    Assert.Equal(ToolUpdateStatus.NewerVersionAvailable, newerVersion.Privoxy!.Status);
                    Assert.Equal(ToolUpdateStatus.NoUpdateAvailable, newerVersion.Tor!.Status);

                    Assert.False(upToDate.HasUpdate);
                    Assert.Equal(ToolUpdateStatus.NoUpdateAvailable, upToDate.Privoxy!.Status);
                    Assert.Equal(ToolUpdateStatus.NoUpdateAvailable, upToDate.Tor!.Status);
                }
            }
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task TorSharpToolFetcher_UseExistingTools()
        {
            using (var te = TestEnvironment.Initialize(_output))
            {
                // Arrange
                var settings = te.BuildSettings();
                settings.ReloadTools = true;
                settings.UseExistingTools = true;

                using (var httpClientHandler = new HttpClientHandler())
                using (var requestCountHandler = new RequestCountHandler { InnerHandler = httpClientHandler })
                using (var loggingHandler = new LoggingHandler(_output) { InnerHandler = requestCountHandler })
                using (var httpClient = new HttpClient(requestCountHandler))
                using (var proxy = new TorSharpProxy(settings))
                {
                    _output.WriteLine(settings);
                    var fetcherA = _httpFixture.GetTorSharpToolFetcher(_output, settings, httpClient);
                    await fetcherA.FetchAsync();
                    var requestCount = requestCountHandler.RequestCount;

                    // Act
                    var fetcherB = _httpFixture.GetTorSharpToolFetcher(_output, settings, httpClient);
                    await fetcherB.FetchAsync();

                    // Assert
                    Assert.True(requestCount > 0, "The should be at least one request.");
                    Assert.Equal(requestCount, requestCountHandler.RequestCount);
                    Assert.NotNull(ToolUtility.GetLatestToolOrNull(settings, ToolUtility.GetPrivoxyToolSettings(settings)));
                    Assert.NotNull(ToolUtility.GetLatestToolOrNull(settings, ToolUtility.GetTorToolSettings(settings)));
                }
            }
        }

        /// <summary>
        /// Downloads <c>manifest.schema.json</c> and <c>manifest.sample.json</c> from the
        /// latest mirror release and validates that the sample deserializes correctly into
        /// the library's internal model. Gated by <c>TORSHARP_RUN_UPSTREAM_TESTS</c>.
        /// </summary>
        [Fact]
        [DisplayTestMethodName]
        public async Task ManifestSchema_SampleDeserializesCorrectly()
        {
            if (!IsUpstreamTestEnabled())
            {
                return;
            }

            const string sampleUrl =
                "https://github.com/nefarius/TorSharp.Mirror/releases/latest/download/manifest.sample.json";

            using var httpClient = new HttpClient();
            var json = await httpClient.GetStringAsync(sampleUrl);

            // Must not throw
            var manifest = JsonSerializer.Deserialize<MirrorManifest>(
                json, MirrorJsonContext.Default.MirrorManifest);

            Assert.NotNull(manifest);
            Assert.Equal(1, manifest!.SchemaVersion);
            Assert.NotNull(manifest.Tor);
            Assert.NotNull(manifest.Privoxy);
            Assert.True(manifest.Tor!.ContainsKey("windows-x86_64"), "Manifest must have windows-x86_64 Tor entry");
            Assert.True(manifest.Privoxy!.ContainsKey("linux-amd64"), "Manifest must have linux-amd64 Privoxy entry");
        }

        public static IEnumerable<object[]> Platforms
        {
            get
            {
                foreach (var osPlatform in Enum.GetValues(typeof(TorSharpOSPlatform)).Cast<TorSharpOSPlatform>())
                {
                    if (osPlatform == TorSharpOSPlatform.Unknown)
                    {
                        continue;
                    }

                    foreach (var architecture in Enum.GetValues(typeof(TorSharpArchitecture)).Cast<TorSharpArchitecture>())
                    {
                        if (architecture == TorSharpArchitecture.Unknown)
                        {
                            continue;
                        }

                        yield return new object[] { osPlatform, architecture };
                    }
                }
            }
        }

        private static bool IsUpstreamTestEnabled() =>
            string.Equals(
                Environment.GetEnvironmentVariable("TORSHARP_RUN_UPSTREAM_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase);
    }
}
