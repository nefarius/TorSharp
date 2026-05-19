using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nefarius.Utilities.TorProxy.Tests.TestSupport;
using Nefarius.Utilities.TorProxy.Tools;
using Nefarius.Utilities.TorProxy.Tools.Tor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Proxy;
using Proxy.Configurations;
using xRetry;
using Xunit;
using Xunit.Abstractions;

namespace Nefarius.Utilities.TorProxy.Tests
{
    [Collection(HttpCollection.Name)]
    public class EndToEnd
    {
        private readonly HttpFixture _httpFixture;
        private readonly ITestOutputHelper _output;

        public EndToEnd(HttpFixture httpFixture, ITestOutputHelper output)
        {
            _httpFixture = httpFixture;
            _output = output;
        }

        [RetryFact]
        [DisplayTestMethodName]
        public async Task ToolRunner_ConfigurationPathsWithSpaces()
        {
            using (var te = TestEnvironment.Initialize(_output))
            {
                // Arrange
                te.TestDirectory.Path = Path.Combine(te.TestDirectory, "Path With Spaces");
                var settings = te.BuildSettings();

                // Act & Assert
                await ExecuteEndToEndTestAsync(settings);
            }
        }

#if NET6_0_OR_GREATER
        [RetryFact]
        [DisplayTestMethodName]
        public async Task PrivoxyCanBeDisabled()
        {
            using (var te = TestEnvironment.Initialize(_output))
            {
                // Arrange
                var settings = te.BuildSettings();
                settings.PrivoxySettings.Disable = true;

                // Act
                using (var httpClient = new HttpClient())
                using (var proxy = new TorProxy(settings))
                {
                    _output.WriteLine(settings);

                    var fetcher = _httpFixture.GetTorProxyToolFetcher(_output, settings, httpClient);
                    await fetcher.FetchAsync();
                    _output.WriteLine("The tools have been fetched");
                    await proxy.ConfigureAndStartAsync();
                    _output.WriteLine("The proxy has been started");

                    var isTor = await SendProxiedRequestAsync(
                        proxy,
                        "Checking if we're using Tor",
                        async proxiedHttpClient =>
                        {
                            var torCheck = await proxiedHttpClient.GetStringAsync("https://check.torproject.org/api/ip");
                            _output.WriteLine("Tor Check: " + torCheck);
                            var json = JsonConvert.DeserializeObject<JObject>(torCheck);
                            return json!.Value<bool>("IsTor");
                        },
                        () => new SocketsHttpHandler
                        {
                            Proxy = new WebProxy("socks5://localhost:" + settings.TorSettings.SocksPort)
                        });

                    // Assert
                    Assert.True(isTor);
                    Assert.All(Directory.EnumerateFileSystemEntries(settings.ZippedToolsDirectory), e => Assert.DoesNotContain("privoxy", e, StringComparison.OrdinalIgnoreCase));
                    Assert.All(Directory.EnumerateFileSystemEntries(settings.ExtractedToolsDirectory), e => Assert.DoesNotContain("privoxy", e, StringComparison.OrdinalIgnoreCase));

                    // Verify the Privoxy port is free — if Privoxy had been started by this test
                    // it would still be bound here (proxy.Stop() is called by the using-block
                    // exit AFTER these assertions).  Checking by port instead of scanning
                    // Process.GetProcesses() avoids false positives when parallel test assemblies
                    // (net8.0 / net9.0) run simultaneously and one sees the other's Privoxy.
                    using (var probe = new TcpListener(IPAddress.Loopback, settings.PrivoxySettings.Port))
                    {
                        probe.Start();
                        probe.Stop();
                    }
                }
            }
        }
#endif

        [RetryFact]
        [DisplayTestMethodName]
        public async Task ToolRunner_CaptureOutput()
        {
            using (var te = TestEnvironment.Initialize(_output))
            {
                // Arrange
                var settings = te.BuildSettings();
                settings.WriteToConsole = false;

                // Act & Assert
                await ExecuteCapturedOutputTestAsync(settings);
            }
        }

        [RetryFact]
        [DisplayTestMethodName]
        public async Task StrictNodesWithGeoWorks()
        {
            using (var te = TestEnvironment.Initialize(_output))
            {
                // Arrange
                var settings = te.BuildSettings();
                settings.TorSettings.ExitNodes = "{de}";
                settings.TorSettings.StrictNodes = true;

                // Act & Assert
                await ExecuteEndToEndTestAsync(settings);
            }
        }

        [RetryFact]
        [DisplayTestMethodName]
        public async Task ToolRunner_EndToEnd()
        {
            using (var te = TestEnvironment.Initialize(_output))
            {
                // Arrange
                var settings = te.BuildSettings();

                // Act & Assert
                await ExecuteEndToEndTestAsync(settings);
            }
        }

        [RetryFact]
        [DisplayTestMethodName]
        public async Task ToolRunner_EndToEnd_Parallel()
        {
            using (var te1 = TestEnvironment.Initialize(_output))
            using (var te2 = TestEnvironment.Initialize(_output))
            using (var te3 = TestEnvironment.Initialize(_output))
            {
                // Arrange
                var settings1 = te1.BuildSettings();
                var settings2 = te2.BuildSettings();
                var settings3 = te3.BuildSettings();
                var barrier = new Barrier(3);

                // Act & Assert
                var tasks = new[]
                {
                    ExecuteEndToEndTestAsync(settings1, barrier),
                    ExecuteEndToEndTestAsync(settings2, barrier),
                    ExecuteEndToEndTestAsync(settings3, barrier),
                };
                await Task.WhenAny(tasks); // fail fast
                await Task.WhenAll(tasks);
            }
        }

        [RetryFact]
        [DisplayTestMethodName]
        public async Task TrafficReadAndWritten()
        {
            using (var te = TestEnvironment.Initialize(_output))
            {
                // Arrange
                var settings = te.BuildSettings();

                using (var httpClient = new HttpClient())
                using (var proxy = new TorProxy(settings))
                {
                    _output.WriteLine(settings);

                    var fetcher = _httpFixture.GetTorProxyToolFetcher(_output, settings, httpClient);
                    await fetcher.FetchAsync();
                    _output.WriteLine("The tools have been fetched");
                    await proxy.ConfigureAndStartAsync();
                    _output.WriteLine("The proxy has been started");

                    await GetCurrentIpAddressAsync(proxy, settings);
                    _output.WriteLine("The first request has succeeded");

                    // Act
                    using (var controlClient = await proxy.GetControlClientAsync())
                    {
                        var readBefore = await controlClient.GetTrafficReadAsync();
                        _output.WriteLine($"Read before: {readBefore}");
                        var writtenBefore = await controlClient.GetTrafficWrittenAsync();
                        _output.WriteLine($"Written before: {writtenBefore}");

                        // Upload and download some bytes
                        var bytes = 10000;
                        await SendProxiedRequestAsync(
                            proxy,
                            settings,
                            $"uploading and downloading {bytes} bytes",
                            async proxiedHttpClient =>
                            {
                                using (var content = new ByteArrayContent(new byte[bytes]))
                                using (var response = await proxiedHttpClient.PostAsync("https://httpbin.org/anything", content))
                                {
                                }

                                return true;
                            });

                        var readAfter = await controlClient.GetTrafficReadAsync();
                        _output.WriteLine($"Read after: {readBefore}");
                        var writtenAfter = await controlClient.GetTrafficWrittenAsync();
                        _output.WriteLine($"Written after: {writtenBefore}");

                        // Assert
                        Assert.True(readBefore + bytes <= readAfter, $"At least {bytes} more bytes should have been read.");
                        Assert.True(writtenBefore + bytes <= writtenAfter, $"At least {bytes} more bytes should have been written.");
                    }
                }
            }
        }

        [RetryFact]
        [DisplayTestMethodName]
        public async Task DefaultControlPasswordWorks()
        {
            using (var te = TestEnvironment.Initialize(_output, setControlPassword: false))
            {
                // Arrange
                var settings = te.BuildSettings();
                settings.PrivoxySettings.Disable = true;

                using (var httpClient = new HttpClient())
                using (var proxy = new TorProxy(settings))
                {
                    _output.WriteLine(settings);

                    var fetcher = _httpFixture.GetTorProxyToolFetcher(_output, settings, httpClient);
                    await fetcher.FetchAsync();
                    _output.WriteLine("The tools have been fetched");
                    await proxy.ConfigureAndStartAsync();
                    _output.WriteLine("The proxy has been started");

                    // Act & Assert
                    using (var controlClient = new TorControlClient())
                    {
                        await controlClient.ConnectAsync("localhost", settings.TorSettings.ControlPort);
                        await controlClient.AuthenticateAsync(TorProxyTorSettings.DefaultControlPassword);
                        await controlClient.CleanCircuitsAsync();
                        await controlClient.QuitAsync();

                        // No exception is thrown
                    }
                }
            }
        }

        [RetryFact]
        [DisplayTestMethodName]
        public async Task TorHttpsProxy_EndToEnd()
        {
            var proxyUsername = "proxy-username";
            var proxyPassword = "proxy-password";
            using (var proxyPort = ReservedPort.Reserve())
            using (var ptp = new PassThroughProxy(proxyPort.Port, new Configuration(
                new Server(proxyPort.Port, rejectHttpProxy: true),
                new Authentication(true, proxyUsername, proxyPassword),
                new Firewall(false, new Rule[0]))))
            using (var te = TestEnvironment.Initialize(_output))
            {
                // Arrange
                var settings = te.BuildSettings();
                settings.TorSettings.HttpsProxyHost = "localhost";
                settings.TorSettings.HttpsProxyPort = proxyPort.Port;
                settings.TorSettings.HttpsProxyUsername = proxyUsername;
                settings.TorSettings.HttpsProxyPassword = proxyPassword;

                // Act & Assert
                await ExecuteEndToEndTestAsync(settings);
            }
        }

        private async Task ExecuteCapturedOutputTestAsync(TorProxySettings settings)
        {
            using (var httpClient = new HttpClient())
            using (var proxy = new TorProxy(settings))
            {
                var output = new ConcurrentQueue<DataEventArgs>();
                proxy.OutputDataReceived += (_, e) => output.Enqueue(e);
                proxy.ErrorDataReceived += (_, e) => output.Enqueue(e);

                _output.WriteLine(settings);

                // Act
                var fetcher = _httpFixture.GetTorProxyToolFetcher(_output, settings, httpClient);
                await fetcher.FetchAsync();
                _output.WriteLine("The tools have been fetched");
                await proxy.ConfigureAndStartAsync();
                _output.WriteLine("The proxy has been started");

                await GetCurrentIpAddressAsync(proxy, settings);

                // Assert
                Assert.NotEmpty(output);
                Assert.Contains(output, x => Path.GetFileName(x.ExecutablePath).StartsWith("tor", StringComparison.OrdinalIgnoreCase));
                if (settings.OSPlatform != TorProxyOSPlatform.Windows)
                {
                    Assert.Contains(output, x => Path.GetFileName(x.ExecutablePath).StartsWith("privoxy", StringComparison.OrdinalIgnoreCase));
                }
                Assert.Contains(output, x => x.Data != null && x.Data.Contains($"Opening Socks listener on 127.0.0.1:{settings.TorSettings.SocksPort}"));
            }
        }

        private async Task ExecuteEndToEndTestAsync(TorProxySettings settings)
        {
            await ExecuteEndToEndTestAsync(settings, barrier: null);
        }

        private async Task ExecuteEndToEndTestAsync(TorProxySettings settings, Barrier? barrier)
        {
            // Arrange
            using (var httpClient = new HttpClient())
            using (var proxy = new TorProxy(settings))
            {
                _output.WriteLine(settings);

                // Act
                var fetcher = _httpFixture.GetTorProxyToolFetcher(_output, settings, httpClient);
                await fetcher.FetchAsync();
                _output.WriteLine("The tools have been fetched");
                await proxy.ConfigureAndStartAsync();
                _output.WriteLine("The proxy has been started");

                // get the first identity
                var ipA = await GetCurrentIpAddressAsync(proxy, settings);

                if (barrier != null)
                {
                    barrier.SignalAndWait();
                }

                await proxy.GetNewIdentityAsync();
                _output.WriteLine("Get new identity succeeded");
                var ipB = await GetCurrentIpAddressAsync(proxy, settings);

                // Assert
                Assert.Equal(AddressFamily.InterNetwork, ipA.AddressFamily);
                Assert.Equal(AddressFamily.InterNetwork, ipB.AddressFamily);

                var zippedDir = new DirectoryInfo(settings.ZippedToolsDirectory);
                Assert.True(zippedDir.Exists, "The zipped tools directory should exist.");
                Assert.Empty(zippedDir.EnumerateDirectories());
                Assert.Equal(2, zippedDir.EnumerateFiles().Count());

                var extractedDir = new DirectoryInfo(settings.ExtractedToolsDirectory);
                Assert.True(extractedDir.Exists, "The extracted tools directory should exist.");
                Assert.Equal(2, extractedDir.EnumerateDirectories().Count());
                Assert.Empty(extractedDir.EnumerateFiles());
            }
        }

        private async Task<IPAddress> GetCurrentIpAddressAsync(TorProxy proxy, TorProxySettings settings)
        {
            return await SendProxiedRequestAsync(
                proxy,
                settings,
                "fetching an IP",
                async proxiedHttpClient =>
                {
                    var ip = (await proxiedHttpClient.GetStringAsync("https://api.ipify.org")).Trim();
                    _output.WriteLine($"Get IP succeeded: {ip}");
                    return IPAddress.Parse(ip);
                });
        }

        private async Task<T> SendProxiedRequestAsync<T>(
            TorProxy proxy,
            TorProxySettings settings,
            string operation,
            Func<HttpClient, Task<T>> executeAsync)
        {
            return await SendProxiedRequestAsync(
                proxy,
                operation,
                executeAsync,
                () => new HttpClientHandler
                {
                    Proxy = new WebProxy(new Uri("http://localhost:" + settings.PrivoxySettings.Port))
                });
        }

        private async Task<T> SendProxiedRequestAsync<T>(
            TorProxy proxy,
            string operation,
            Func<HttpClient, Task<T>> executeAsync,
            Func<HttpMessageHandler> getHttpMessageHandler)
        {
            const int maxAttempts = 3;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using (var handler = getHttpMessageHandler())
                    using (var httpClient = new HttpClient(handler))
                    {
                        httpClient.Timeout = TimeSpan.FromSeconds(60);

                        return await executeAsync(httpClient);
                    }
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    _output.WriteLine($"[Attempt {attempt}] An exception was thrown while {operation}. Retrying." + Environment.NewLine + ex);
                    await proxy.GetNewIdentityAsync();
                }
            }

            throw new NotImplementedException();
        }
    }
}
