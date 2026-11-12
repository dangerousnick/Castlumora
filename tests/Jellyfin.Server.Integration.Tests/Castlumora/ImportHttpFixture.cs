using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.IO;
using Emby.Server.Implementations.Library;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Tests.Castlumora;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Providers.Manager;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Jellyfin.Server.Integration.Tests.Castlumora;

/// <summary>Owned TLS source and privileged test-only DI; no production URL/TLS policy switch.</summary>
internal sealed class ImportHttpFixture : IAsyncDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "castlumora-http-import-" + Guid.NewGuid().ToString("N"));
    private readonly CancellationTokenSource _stop = new();
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly X509Certificate2 _certificate = ImportTestCertificate.Create("fixture.invalid");
    private Task _server = Task.CompletedTask;

    internal ConcurrentQueue<string> Diagnostics { get; } = new();

    internal string Source => "https://fixture.invalid/owned.mp4?token=signed-secret";

    internal string Library => Path.Combine(_directory, "library");

    internal string Server => Path.Combine(_directory, "server");

    internal string Ca => Path.Combine(_directory, "owned-ca.pem");

    internal byte[] Payload { get; } = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Test Data", "import-owned.mp4"));

    internal static async Task<ImportHttpFixture> CreateAsync()
    {
        var fixture = new ImportHttpFixture();
        Directory.CreateDirectory(fixture.Library);
        await File.WriteAllTextAsync(fixture.Ca, fixture._certificate.ExportCertificatePem(), fixture._stop.Token);
        fixture._listener.Start();
        fixture._server = fixture.ServeAsync();
        return fixture;
    }

    internal ImportApplicationFactory Host() => new(this);

    internal ImportNetworkPolicy Policy() => new(
        (host, _) => host == "fixture.invalid" ? Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }) : throw new InvalidDataException("Unexpected owned-fixture source host"),
        async (_, token) =>
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(_listener.LocalEndpoint, token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        });

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        await _server;
        _certificate.Dispose();
        _stop.Dispose();
        Directory.Delete(_directory, true);
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
                        if (header.Length > 16384)
                        {
                            throw new InvalidDataException("Owned fixture header bound");
                        }
                    }

                    if (header.ToString().Contains("Proxy-Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("Proxy credentials must not reach source");
                    }

                    var denied = header.ToString().Contains("case=denied", StringComparison.Ordinal);
                    var status = denied ? "403 Forbidden" : "200 OK";
                    await tls.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: video/mp4\r\nContent-Length: {Payload.Length}\r\nConnection: close\r\n\r\n"), _stop.Token);
                    if (!header.ToString().StartsWith("HEAD ", StringComparison.Ordinal))
                    {
                        await tls.WriteAsync(Payload, _stop.Token);
                    }

                    await tls.FlushAsync(_stop.Token);
                }
                catch (Exception exception) when (exception is IOException or System.Security.Authentication.AuthenticationException)
                {
                    // Cancellation/denied sources intentionally close owned sockets.
                }
            }
        }
        catch (Exception exception) when (_stop.IsCancellationRequested && exception is OperationCanceledException or SocketException or InvalidOperationException or IOException)
        {
            // Owned fixture shutdown only.
        }
    }

    private sealed class DiagnosticLogger<T>(ImportHttpFixture source) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (source.Diagnostics.Count < 200)
            {
                source.Diagnostics.Enqueue(typeof(T).Name + ": " + formatter(state, exception) + " " + exception);
            }
        }
    }

    internal sealed class ImportApplicationFactory(ImportHttpFixture source) : JellyfinApplicationFactory(source.Server)
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ILogger<LibraryManager>>(new DiagnosticLogger<LibraryManager>(source));
                services.AddSingleton<ILogger<LibraryMonitor>>(new DiagnosticLogger<LibraryMonitor>(source));
                services.AddSingleton<ILogger<ProviderManager>>(new DiagnosticLogger<ProviderManager>(source));
                services.RemoveAll<YtDlpRuntime>();
                services.AddSingleton(provider =>
                {
                    var paths = provider.GetRequiredService<IApplicationPaths>();
                    var toolPaths = new Mock<IApplicationPaths>();
                    toolPaths.SetupGet(paths => paths.DataPath).Returns(paths.DataPath);
                    toolPaths.SetupGet(paths => paths.ProgramSystemPath).Returns(Environment.GetEnvironmentVariable("CASTLUMORA_TEST_YTDLP_BUNDLE") ?? throw new InvalidOperationException("Real pinned downloader fixture required"));
                    return new YtDlpRuntime(toolPaths.Object);
                });
                services.RemoveAll<ImportService>();
                services.AddSingleton(provider =>
                {
                    var paths = provider.GetRequiredService<IApplicationPaths>();
                    var runtime = provider.GetRequiredService<YtDlpRuntime>();
                    var libraries = provider.GetRequiredService<ILibraryManager>();
                    var users = provider.GetRequiredService<IUserManager>();
                    var monitor = provider.GetRequiredService<ILibraryMonitor>();
                    provider.GetRequiredService<IServerConfigurationManager>().Configuration.LibraryMonitorDelay = 1;
                    var media = new Mock<IMediaEncoder>();
                    var probe = Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFPROBE") ?? throw new InvalidOperationException("Real native probe required");
                    var encoder = Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFMPEG") ?? throw new InvalidOperationException("Real native decoder required");
                    media.SetupGet(item => item.ProbePath).Returns(probe);
                    media.SetupGet(item => item.EncoderPath).Returns(encoder);
                    var runner = new YtDlpImportRunner(runtime, paths.DataPath, NullLogger.Instance, source.Policy(), new YtDlpImportRunner.Limits(), source.Ca);
                    return new ImportService(runtime, provider.GetRequiredService<IServerApplicationHost>(), paths, libraries, users, monitor, media.Object, provider.GetRequiredService<ILogger<ImportService>>(), () => new ImportPublication(
                        paths.DataPath,
                        [paths.ProgramDataPath, paths.ProgramSystemPath, paths.ConfigurationDirectoryPath],
                        job => ImportPublication.Resolve(job, libraries, users),
                        (job, progress, token) => runner.RunAsync(job.Source, token, progress),
                        path => ImportPublication.RequestScan(path, libraries, monitor),
                        probe,
                        encoder,
                        NullLogger.Instance));
                });
            });
        }
    }
}
