using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

[Collection(nameof(ImportExecutableCollection))]
public sealed class YtDlpImportRunnerTests
{
    [Fact]
    public async Task RealPinnedTool_StagesExactBytesWithProxyEnvironmentAndStdinSource_ThenDisposes()
    {
        await using var fixture = await Fixture.CreateAsync("ok");
        using var staged = await fixture.Runner.RunAsync(fixture.Source, TestContext.Current.CancellationToken);
        Assert.Equal(fixture.Payload, await File.ReadAllBytesAsync(staged.MediaPath, TestContext.Current.CancellationToken));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(fixture.Payload)), staged.Sha256);
        Assert.Equal(fixture.Payload.Length, staged.Bytes);
        Assert.True(File.Exists(staged.MetadataPath));
        Assert.True(fixture.Connections > 0);
        Assert.Equal(0, (await fixture.Runtime.StatusAsync(TestContext.Current.CancellationToken))["activeExecutions"]!.GetValue<int>());
        staged.Dispose();
        staged.Dispose();
        fixture.AssertNoStaging();
    }

    [Theory]
    [InlineData("untrusted")]
    [InlineData("denied")]
    [InlineData("declaredLarge")]
    [InlineData("streamLarge")]
    [InlineData("metadataLarge")]
    [InlineData("unexpectedFile")]
    public async Task Failure_ReleasesLeaseAndCleansPrivatePartialFiles(string mode)
    {
        await using var fixture = await Fixture.CreateAsync(mode);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.RunAsync(fixture.Source, TestContext.Current.CancellationToken));
        Assert.DoesNotContain("fixture-token", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Proxy-Authorization", error.ToString(), StringComparison.OrdinalIgnoreCase);
        if (mode == "untrusted")
        {
            Assert.Contains("certificate", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        if (mode == "denied")
        {
            Assert.Contains("denied", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        if (mode is "declaredLarge" or "streamLarge" or "metadataLarge")
        {
            Assert.Contains("size", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        if (mode == "unexpectedFile")
        {
            Assert.Contains("Unexpected", error.Message, StringComparison.Ordinal);
        }

        fixture.AssertNoStaging();
        Assert.Equal(0, (await fixture.Runtime.StatusAsync(TestContext.Current.CancellationToken))["activeExecutions"]!.GetValue<int>());
    }

    [Fact]
    public async Task CancellationAfterSourceConnect_StopsOwnedExecutionAndCleansAttempt()
    {
        await using var fixture = await Fixture.CreateAsync("stall");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var download = fixture.Runner.RunAsync(fixture.Source, cancellation.Token);
        try
        {
            await fixture.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => download);
        }
        finally
        {
            await cancellation.CancelAsync();
            try
            {
                await download;
            }
            catch (OperationCanceledException)
            {
                // Observe expected cancellation even when an earlier fixture assertion fails.
            }
        }

        fixture.AssertNoStaging();
        Assert.Equal(0, (await fixture.Runtime.StatusAsync(TestContext.Current.CancellationToken))["activeExecutions"]!.GetValue<int>());
    }

    [Fact]
    public async Task RealPinnedProcess_RejectsOutputOverflowAndEnforcesDeadline()
    {
        await using var fixture = await Fixture.CreateAsync("stall");
        using var lease = await fixture.Runtime.AcquireAsync(TestContext.Current.CancellationToken);
        var info = fixture.Runner.CreateStart(lease.Executable, fixture.Root, fixture.Root, new Uri("http://castlumora:fixture-secret@127.0.0.1:1"));
        info.ArgumentList.Clear();
        info.ArgumentList.Add("--ignore-config");
        info.ArgumentList.Add("--no-plugin-dirs");
        info.ArgumentList.Add("--help");
        await Assert.ThrowsAsync<InvalidDataException>(() => ImportProcess.RunAsync(
            info,
            string.Empty,
            TimeSpan.FromSeconds(30),
            64,
            token => Task.Delay(Timeout.InfiniteTimeSpan, token),
            TestContext.Current.CancellationToken));
        info = fixture.Runner.CreateStart(lease.Executable, fixture.Root, fixture.Root, new Uri("http://castlumora:fixture-secret@127.0.0.1:1"));
        // A tiny absolute deadline includes startup; no network connection is needed to prove it.
        await Assert.ThrowsAsync<TimeoutException>(() => ImportProcess.RunAsync(
            info,
            fixture.Source,
            TimeSpan.FromMilliseconds(1),
            16384,
            token => Task.Delay(Timeout.InfiniteTimeSpan, token),
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("http://fixture.invalid/video.mp4", "absolute HTTPS")]
    [InlineData("https://127.0.0.1/video.mp4", "publicly routable")]
    [InlineData("file:///etc/passwd", "absolute HTTPS")]
    [InlineData("https://fixture.invalid/channel", "direct HTTPS MP4")]
    public async Task UnsafeOrUnsupportedSourceFailsBeforeRuntimeExecution(string source, string expected)
    {
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(p => p.DataPath).Returns(Path.GetTempPath());
        paths.SetupGet(p => p.ProgramSystemPath).Returns(Path.GetTempPath());
        using var runtime = new YtDlpRuntime(paths.Object);
        var runner = new YtDlpImportRunner(runtime, paths.Object, NullLogger.Instance);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => runner.RunAsync(source, TestContext.Current.CancellationToken));
        Assert.Contains(expected, failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessContract_DoesNotExposeProxyCredentialsOrSourceInArguments()
    {
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(p => p.DataPath).Returns(Path.GetTempPath());
        paths.SetupGet(p => p.ProgramSystemPath).Returns(Path.GetTempPath());
        using var runtime = new YtDlpRuntime(paths.Object);
        var runner = new YtDlpImportRunner(runtime, paths.Object, NullLogger.Instance);
        var proxy = new Uri("http://castlumora:private-proxy-secret@127.0.0.1:1234");
        var info = runner.CreateStart("reviewed-managed-tool", Path.GetTempPath(), Path.GetTempPath(), proxy);
        Assert.DoesNotContain("private-proxy-secret", string.Join(' ', info.ArgumentList), StringComparison.Ordinal);
        Assert.DoesNotContain("--no-check-certificates", info.ArgumentList);
        Assert.Equal(proxy.AbsoluteUri, info.Environment["HTTPS_PROXY"]);
        Assert.False(info.Environment.ContainsKey("PYTHONPATH"));
        Assert.Contains("--batch-file", info.ArgumentList);
        Assert.Contains("--no-js-runtimes", info.ArgumentList);
        Assert.Contains("--no-remote-components", info.ArgumentList);
        Assert.Contains("--use-extractors", info.ArgumentList);
        Assert.Contains("--xff", info.ArgumentList);
        Assert.Contains("never", info.ArgumentList);
    }

    [Fact]
    public async Task RealPinnedTool_DurableQueueCompletesAndDeduplicates_PrivateStagingOnly()
    {
        await using var fixture = await Fixture.CreateAsync("ok");
        var directory = Path.Combine(fixture.Root, "queue");
        var calls = 0;
        Guid id;
        var destination = Guid.NewGuid();
        var admin = Guid.NewGuid();
        await using (var queue = new ImportQueue(
            directory,
            async (job, progress, token) =>
        {
            Interlocked.Increment(ref calls);
            using var staged = await fixture.Runner.RunAsync(job.Source, token, progress);
            progress(staged.Bytes);
            // Internal queue/runner integration only: bytes are NOT a validated playable library item.
            var retained = Path.Combine(directory, job.Id.ToString("N") + ".owned-fixture");
            File.Copy(staged.MediaPath, retained);
            return new ImportQueue.Receipt(staged.Sha256, staged.Bytes);
        },
            NullLogger.Instance))
        {
            var job = await queue.SubmitAsync(fixture.Source, destination, "Owned fixture", "Fixture created for integration testing", admin, TestContext.Current.CancellationToken);
            id = job.Id;
            queue.Start();
            await WaitForQueueStateAsync(queue, ImportQueue.State.Completed);
            Assert.Equal(id, (await queue.SubmitAsync(fixture.Source, destination, "Owned fixture", "Fixture created for integration testing", admin, TestContext.Current.CancellationToken)).Id);
            Assert.Equal(fixture.Payload, await File.ReadAllBytesAsync(Path.Combine(directory, id.ToString("N") + ".owned-fixture"), TestContext.Current.CancellationToken));
        }

        await using var reloaded = new ImportQueue(directory, (_, _, _) => throw new InvalidOperationException("Completed job must not execute again"), NullLogger.Instance);
        reloaded.Start();
        var restored = Assert.Single(await reloaded.ListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(id, restored.Id);
        Assert.Equal(ImportQueue.State.Completed, restored.State);
        Assert.Equal(1, calls);
        fixture.AssertNoStaging();
    }

    [Fact]
    public async Task RealPinnedTool_QueueCancellationTerminatesProcessAndCleansStaging()
    {
        await using var fixture = await Fixture.CreateAsync("stall");
        await using var queue = new ImportQueue(
            Path.Combine(fixture.Root, "queue"),
            async (job, _, token) =>
        {
            using var staged = await fixture.Runner.RunAsync(job.Source, token);
            return new ImportQueue.Receipt(staged.Sha256, staged.Bytes);
        },
            NullLogger.Instance);
        var submitted = await queue.SubmitAsync(fixture.Source, Guid.NewGuid(), "Owned fixture", "Owned test fixture", Guid.NewGuid(), TestContext.Current.CancellationToken);
        queue.Start();
        await fixture.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await queue.CancelAsync(submitted.Id, TestContext.Current.CancellationToken);
        await WaitForQueueStateAsync(queue, ImportQueue.State.Cancelled);
        fixture.AssertNoStaging();
        Assert.Equal(0, (await fixture.Runtime.StatusAsync(TestContext.Current.CancellationToken))["activeExecutions"]!.GetValue<int>());
    }

    [Fact]
    public async Task RealPinnedTool_InterruptedQueueRestartsSameJobAndCleansBothAttempts()
    {
        var journal = Path.Combine(Path.GetTempPath(), "castlumora-native-recovery-" + Guid.NewGuid().ToString("N"));
        Guid id;
        try
        {
            await using (var interrupted = await Fixture.CreateAsync("stall"))
            {
                await using (var queue = new ImportQueue(
                    journal,
                    async (job, progress, token) =>
                    {
                        using var staged = await interrupted.Runner.RunAsync(job.Source, token, progress);
                        return new ImportQueue.Receipt(staged.Sha256, staged.Bytes);
                    },
                    NullLogger.Instance))
                {
                    id = (await queue.SubmitAsync(interrupted.Source, Guid.NewGuid(), "Owned fixture", "Owned test fixture", Guid.NewGuid(), TestContext.Current.CancellationToken)).Id;
                    queue.Start();
                    await interrupted.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
                }

                interrupted.AssertNoStaging();
                Assert.Equal(0, (await interrupted.Runtime.StatusAsync(TestContext.Current.CancellationToken))["activeExecutions"]!.GetValue<int>());
            }

            await using var resumed = await Fixture.CreateAsync("ok");
            await using var recovered = new ImportQueue(
                journal,
                async (job, progress, token) =>
                {
                    using var staged = await resumed.Runner.RunAsync(job.Source, token, progress);
                    return new ImportQueue.Receipt(staged.Sha256, staged.Bytes);
                },
                NullLogger.Instance);
            recovered.Start();
            await WaitForQueueStateAsync(recovered, ImportQueue.State.Completed);
            var completed = Assert.Single(await recovered.ListAsync(TestContext.Current.CancellationToken));
            Assert.Equal(id, completed.Id);
            Assert.Equal(2, completed.Attempts);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(resumed.Payload)), completed.Sha256);
            resumed.AssertNoStaging();
        }
        finally
        {
            if (Directory.Exists(journal))
            {
                Directory.Delete(journal, true);
            }
        }
    }

    [Theory]
    [InlineData("media", true)]
    [InlineData("ok", false)]
    public async Task RealQueueAndDownloader_RequireInspectedMediaBeforeCompletion(string mode, bool valid)
    {
        await using var fixture = await Fixture.CreateAsync(mode);
        var probe = Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFPROBE");
        Assert.True(!string.IsNullOrWhiteSpace(probe) && File.Exists(probe), "A real native FFprobe fixture is required.");
        var encoder = Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFMPEG");
        Assert.True(!string.IsNullOrWhiteSpace(encoder) && File.Exists(encoder), "A real native FFmpeg fixture is required.");
        await using var queue = new ImportQueue(
            Path.Combine(fixture.Root, "queue"),
            async (job, progress, token) =>
            {
                using var staged = await fixture.Runner.RunAsync(job.Source, token, progress);
                var seconds = await ImportMediaInspection.InspectAsync(staged.MediaPath, probe!, encoder!, token);
                Assert.InRange(seconds, 0.9, 1.2);
                // Validation is real, but library publication remains a separate, disabled boundary.
                return new ImportQueue.Receipt(staged.Sha256, staged.Bytes);
            },
            NullLogger.Instance);
        var job = await queue.SubmitAsync(fixture.Source, Guid.NewGuid(), "Owned generated clip", "Generated for Castlumora tests", Guid.NewGuid(), TestContext.Current.CancellationToken);
        queue.Start();
        await WaitForQueueStateAsync(queue, valid ? ImportQueue.State.Completed : ImportQueue.State.Failed);
        var result = Assert.Single(await queue.ListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(job.Id, result.Id);
        Assert.Equal(valid ? Convert.ToHexStringLower(SHA256.HashData(fixture.Payload)) : null, result.Sha256);
        fixture.AssertNoStaging();
    }

    [Fact]
    public async Task RealHttpsQueue_InspectsPublishesAndReconcilesWithoutSecondDownload()
    {
        await using var fixture = await Fixture.CreateAsync("media");
        var library = Path.Combine(Path.GetTempPath(), "castlumora-library-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(library);
        try
        {
            var scans = 0;
            var downloads = 0;
            var publisher = new ImportPublication(
                fixture.Root,
                [fixture.Root],
                _ => library,
                async (job, progress, token) =>
                {
                    downloads++;
                    return await fixture.Runner.RunAsync(job.Source, token, progress);
                },
                _ => scans++,
                Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFPROBE")!,
                Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFMPEG")!,
                NullLogger.Instance);
            ImportQueue.Job submitted;
            await using (var queue = new ImportQueue(Path.Combine(fixture.Root, "queue"), publisher.ExecuteAsync, NullLogger.Instance))
            {
                submitted = await queue.SubmitAsync(fixture.Source, Guid.NewGuid(), "Owned TLS generated video", "Generated test clip", Guid.NewGuid(), TestContext.Current.CancellationToken);
                queue.Start();
                await WaitForQueueStateAsync(queue, ImportQueue.State.Completed);
            }

            var media = Assert.Single(Directory.GetFiles(library, "*.mp4", SearchOption.AllDirectories));
            Assert.Equal(fixture.Payload, await File.ReadAllBytesAsync(media, TestContext.Current.CancellationToken));
            await publisher.ExecuteAsync(submitted, _ => { }, TestContext.Current.CancellationToken);
            Assert.Equal(1, downloads);
            Assert.Equal(2, scans);
            fixture.AssertNoStaging();
        }
        finally
        {
            Directory.Delete(library, true);
        }
    }

    private static async Task WaitForQueueStateAsync(ImportQueue queue, ImportQueue.State expected)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        while (Assert.Single(await queue.ListAsync(deadline.Token)).State != expected)
        {
            await Task.Delay(20, deadline.Token);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly X509Certificate2 _certificate = ImportTestCertificate.Create("fixture.invalid");
        private readonly string _mode;
        private Task _server = Task.CompletedTask;
        private int _connections;

        private Fixture(string mode)
        {
            _mode = mode;
            Root = Path.Combine(Path.GetTempPath(), "castlumora-runner-test-" + Guid.NewGuid().ToString("N"));
            YtDlpPlatform.PrivateDirectory(Root);
            Payload = mode == "media" ? File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "import-owned.mp4"))
                : mode == "streamLarge" ? new byte[2097152] : Encoding.ASCII.GetBytes("Owned transport bytes; playable-container validation belongs before library commit.");
            _stop.CancelAfter(TimeSpan.FromSeconds(90));
        }

        internal string Root { get; }

        internal string Source => "https://fixture.invalid/owned.mp4?token=fixture-token";

        internal byte[] Payload { get; }

        internal YtDlpRuntime Runtime { get; private set; } = null!;

        internal YtDlpImportRunner Runner { get; private set; } = null!;

        internal int Connections => Volatile.Read(ref _connections);

        internal TaskCompletionSource RequestReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal static async Task<Fixture> CreateAsync(string mode)
        {
            var bundle = Environment.GetEnvironmentVariable("CASTLUMORA_TEST_YTDLP_BUNDLE");
            if (string.IsNullOrEmpty(bundle))
            {
                Assert.Skip("Provide the verified platform executable fixture; no real-asset claim without it.");
            }

            var fixture = new Fixture(mode);
            try
            {
                var paths = new Mock<IApplicationPaths>();
                paths.SetupGet(p => p.DataPath).Returns(fixture.Root);
                paths.SetupGet(p => p.ProgramSystemPath).Returns(bundle!);
                fixture.Runtime = new YtDlpRuntime(paths.Object);
                await fixture.Runtime.InstallAsync(fixture._stop.Token);
                var ca = Path.Combine(fixture.Root, "owned-ca.pem");
                await File.WriteAllTextAsync(ca, fixture._certificate.ExportCertificatePem(), fixture._stop.Token);
                fixture._listener.Start();
                fixture._server = fixture.ServeAsync();
                var policy = new ImportNetworkPolicy(
                    (host, _) =>
                    {
                        Assert.Equal("fixture.invalid", host);
                        return Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") });
                    },
                    async (_, token) =>
                    {
                        Interlocked.Increment(ref fixture._connections);
                        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        try
                        {
                            await socket.ConnectAsync(fixture._listener.LocalEndpoint, token);
                            return new NetworkStream(socket, ownsSocket: true);
                        }
                        catch
                        {
                            socket.Dispose();
                            throw;
                        }
                    });
                var size = mode == "declaredLarge" ? 16 : mode == "streamLarge" ? 32768 : 33554432;
                fixture.Runner = new YtDlpImportRunner(fixture.Runtime, fixture.Root, NullLogger.Instance, policy, new YtDlpImportRunner.Limits(size), mode == "untrusted" ? null : ca);
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        internal void AssertNoStaging()
        {
            var path = Path.Combine(Root, "castlumora", "imports", "staging");
            Assert.True(!Directory.Exists(path) || Directory.GetDirectories(path).Length == 0);
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Stop();
            await _server;
            Runtime?.Dispose();
            _certificate.Dispose();
            _stop.Dispose();
            Directory.Delete(Root, true);
        }

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    try
                    {
                        await using var tls = new SslStream(client.GetStream());
                        await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = _certificate }, _stop.Token);
                        var header = new StringBuilder();
                        var bytes = new byte[1];
                        while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                        {
                            await tls.ReadExactlyAsync(bytes, _stop.Token);
                            header.Append((char)bytes[0]);
                            Assert.True(header.Length < 16384);
                        }

                        Assert.DoesNotContain("Proxy-Authorization", header.ToString(), StringComparison.OrdinalIgnoreCase);
                        RequestReceived.TrySetResult();
                        if (_mode is "metadataLarge" or "unexpectedFile")
                        {
                            // Model adversarial producer output while the real owned process is active.
                            var attempt = Assert.Single(Directory.GetDirectories(Path.Combine(Root, "castlumora", "imports", "staging")));
                            var name = _mode == "metadataLarge" ? "media.info.json" : "untrusted-extra.txt";
                            var payload = _mode == "metadataLarge" ? new byte[2097153] : new byte[1];
                            await File.WriteAllBytesAsync(Path.Combine(attempt, "files", name), payload, _stop.Token);
                            await Task.Delay(Timeout.InfiniteTimeSpan, _stop.Token);
                        }

                        if (_mode == "stall")
                        {
                            await Task.Delay(Timeout.InfiniteTimeSpan, _stop.Token);
                        }

                        var status = _mode == "denied" ? "403 Forbidden" : "200 OK";
                        var length = _mode == "streamLarge" ? string.Empty : $"Content-Length: {Payload.Length}\r\n";
                        await tls.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: video/mp4\r\n{length}Connection: close\r\n\r\n"), _stop.Token);
                        if (!header.ToString().StartsWith("HEAD ", StringComparison.Ordinal))
                        {
                            await tls.WriteAsync(Payload, _stop.Token);
                        }

                        await tls.FlushAsync(_stop.Token);
                    }
                    catch (Exception exception) when (exception is System.Security.Authentication.AuthenticationException or IOException)
                    {
                        // Negative certificate/cancellation/size tests intentionally close the connection.
                    }
                }
            }
            catch (Exception exception) when (_stop.IsCancellationRequested && exception is OperationCanceledException or SocketException or InvalidOperationException or IOException)
            {
                // Owned fixture lifetime only.
            }
        }
    }
}
