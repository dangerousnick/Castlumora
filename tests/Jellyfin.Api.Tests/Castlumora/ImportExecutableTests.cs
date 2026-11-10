using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using MediaBrowser.Common.Configuration;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class ImportExecutableTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PinnedExecutableDownloadsOwnedHttpsFixtureThroughAuthenticatedTunnel(bool trustFixture)
    {
        var bundle = Environment.GetEnvironmentVariable("CASTLUMORA_TEST_YTDLP_BUNDLE");
        if (string.IsNullOrEmpty(bundle))
        {
            Assert.Skip("Provide the verified platform fixture; Linux/Windows CI prepares this bundle.");
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "castlumora-import-executable-" + Guid.NewGuid().ToString("N"));
        YtDlpPlatform.PrivateDirectory(root);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            var paths = new Mock<IApplicationPaths>();
            paths.SetupGet(p => p.DataPath).Returns(root);
            paths.SetupGet(p => p.ProgramSystemPath).Returns(bundle);
            using var runtime = new YtDlpRuntime(paths.Object);
            await runtime.InstallAsync(timeout.Token);
            using var lease = await runtime.AcquireAsync(timeout.Token);
            using var certificate = ImportTestCertificate.Create();
            var ca = Path.Combine(root, "fixture-ca.pem");
            await File.WriteAllTextAsync(ca, certificate.ExportCertificatePem(), timeout.Token);
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var payload = Encoding.ASCII.GetBytes("Owned authorized transport fixture, not a playable media certification.");
            using var serving = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            var server = ServeAsync(listener, certificate, payload, trustFixture, serving.Token);
            var connections = 0;
            var policy = new ImportNetworkPolicy(
                (host, _) =>
                {
                    Assert.Equal("example.org", host);
                    return Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") });
                },
                async (endpoint, token) =>
                {
                    Assert.Equal(IPAddress.Parse("8.8.8.8"), endpoint.Address);
                    Interlocked.Increment(ref connections);
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    await socket.ConnectAsync(listener.LocalEndpoint, token);
                    return new NetworkStream(socket, ownsSocket: true);
                });
            await using var proxy = new ImportTunnel(policy, timeout.Token);
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo(lease.Executable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = root
            };
            foreach (var arg in new[]
            {
                "--ignore-config", "--no-plugin-dirs", "--no-cache-dir", "--no-playlist",
                "--force-generic-extractor", "--downloader", "native", "--fixup", "never",
                "--format", "best[protocol=https]", "--proxy", proxy.Proxy.AbsoluteUri,
                "--compat-options", "no-certifi", "--output", Path.Combine(root, "media.mp4"), "--write-info-json",
                "--socket-timeout", "10", "--retries", "0", "--", "https://example.org/owned.mp4"
            })
            {
                process.StartInfo.ArgumentList.Add(arg);
            }

            process.StartInfo.Environment.Clear();
            process.StartInfo.Environment["HOME"] = root;
            if (trustFixture)
            {
                process.StartInfo.Environment["SSL_CERT_FILE"] = ca;
            }

            process.StartInfo.Environment["TMPDIR"] = root;
            process.StartInfo.Environment["TMP"] = root;
            process.StartInfo.Environment["TEMP"] = root;
            if (OperatingSystem.IsWindows())
            {
                process.StartInfo.Environment["SystemRoot"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            }
            else
            {
                process.StartInfo.Environment["PATH"] = "/usr/bin:/bin";
            }

            process.Start();
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                await Task.WhenAll(output, error);
                var diagnostic = (await error).Replace(proxy.Proxy.AbsoluteUri, "[proxy]", StringComparison.Ordinal)
                    .Replace(proxy.Proxy.UserInfo, "[credentials]", StringComparison.Ordinal)
                    .Replace(proxy.Proxy.UserInfo.Split(':')[1], "[secret]", StringComparison.Ordinal)
                    .Replace(Convert.ToBase64String(Encoding.ASCII.GetBytes(proxy.Proxy.UserInfo)), "[authorization]", StringComparison.Ordinal);
                if (trustFixture)
                {
                    Assert.True(process.ExitCode == 0, diagnostic.Length > 2048 ? diagnostic[..2048] : diagnostic);
                    Assert.Equal(payload, await File.ReadAllBytesAsync(Path.Combine(root, "media.mp4"), timeout.Token));
                    Assert.True(File.Exists(Path.Combine(root, "media.info.json")));
                }
                else
                {
                    Assert.NotEqual(0, process.ExitCode);
                    Assert.Contains("CERTIFICATE_VERIFY_FAILED", diagnostic, StringComparison.OrdinalIgnoreCase);
                    Assert.False(File.Exists(Path.Combine(root, "media.mp4")));
                }

                Assert.True(connections > 0);
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }

                await serving.CancelAsync();
                listener.Stop();
                await server;
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static async Task ServeAsync(TcpListener listener, X509Certificate2 certificate, byte[] payload, bool trustFixture, CancellationToken token)
    {
        try
        {
            while (true)
            {
                using var client = await listener.AcceptTcpClientAsync(token);
                try
                {
                    await using var tls = new SslStream(client.GetStream());
                    await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, token);
                    var header = new StringBuilder();
                    var bytes = new byte[1];
                    while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                    {
                        await tls.ReadExactlyAsync(bytes, token);
                        header.Append((char)bytes[0]);
                        Assert.True(header.Length < 16384);
                    }

                    await tls.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: video/mp4\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n"), token);
                    if (!header.ToString().StartsWith("HEAD ", StringComparison.Ordinal))
                    {
                        await tls.WriteAsync(payload, token);
                    }

                    await tls.FlushAsync(token);
                }
                catch (Exception exception) when (!trustFixture && exception is AuthenticationException or IOException)
                {
                    // An untrusted fixture certificate must terminate the tool's TLS request.
                }
            }
        }
        catch (Exception exception) when (token.IsCancellationRequested && exception is OperationCanceledException or SocketException or InvalidOperationException or IOException)
        {
            // Only the owned fixture is stopped.
        }
    }
}
