using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class ImportTunnelTests
{
    [Theory]
    [InlineData("GET https://example.org/ HTTP/1.1\r\n")]
    [InlineData("CONNECT example.org:80 HTTP/1.1\r\n")]
    [InlineData("CONNECT 127.0.0.1:443 HTTP/1.1\r\n")]
    [InlineData("CONNECT example.org:443 HTTP/1.1\r\nContent-Length: 2\r\n")]
    [InlineData("CONNECT example.org:443 HTTP/1.1\r\nTransfer-Encoding: chunked\r\n")]
    [InlineData("CONNECT example.org:443 HTTP/1.1\r\n Proxy-Authorization: bad\r\n")]
    public async Task UnsafeRequestNeverConnects(string request)
    {
        var calls = 0;
        await using var proxy = new ImportTunnel(
            Policy(() =>
            {
                calls++;
                return new MemoryStream();
            }),
            TestContext.Current.CancellationToken);
        using var client = await Client(proxy);
        await Write(client.GetStream(), request + Auth(proxy) + "\r\n");
        Assert.Equal(0, await Closed(client.GetStream()));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task MissingOrDuplicateCredentialsNeverConnect()
    {
        var calls = 0;
        await using var proxy = new ImportTunnel(
            Policy(() =>
            {
                calls++;
                return new MemoryStream();
            }),
            TestContext.Current.CancellationToken);
        foreach (var credentials in new[] { string.Empty, "Proxy-Authorization: Basic wrong\r\n", Auth(proxy) + Auth(proxy) })
        {
            using var client = await Client(proxy);
            await Write(client.GetStream(), "CONNECT example.org:443 HTTP/1.1\r\n" + credentials + "\r\n");
            Assert.Equal(0, await Closed(client.GetStream()));
        }

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task OversizedHeadersFailClosed()
    {
        await using var proxy = new ImportTunnel(Policy(() => throw new InvalidOperationException("Must not connect")), TestContext.Current.CancellationToken);
        using var client = await Client(proxy);
        await Write(client.GetStream(), "CONNECT example.org:443 HTTP/1.1\r\nUser-Agent: " + new string('x', 4100) + "\r\n" + Auth(proxy) + "\r\n");
        Assert.Equal(0, await Closed(client.GetStream()));
    }

    [Fact]
    public async Task EveryNewTunnelRechecksDnsIncludingRedirectTargets()
    {
        var resolved = 0;
        var connected = 0;
        var policy = new ImportNetworkPolicy(
            (_, _) => Task.FromResult(new[] { ++resolved == 1 ? IPAddress.Parse("8.8.8.8") : IPAddress.Loopback }),
            (_, _) =>
            {
                connected++;
                return Task.FromResult<Stream>(new MemoryStream());
            });
        await using var proxy = new ImportTunnel(policy, TestContext.Current.CancellationToken);
        using (var first = await Client(proxy))
        {
            await Write(first.GetStream(), "CONNECT example.org:443 HTTP/1.1\r\n" + Auth(proxy) + "\r\n");
            Assert.Contains("200 Connection Established", await Header(first.GetStream()), StringComparison.Ordinal);
        }

        using var second = await Client(proxy);
        await Write(second.GetStream(), "CONNECT redirect.example.org:443 HTTP/1.1\r\n" + Auth(proxy) + "\r\n");
        Assert.Equal(0, await Closed(second.GetStream()));
        Assert.Equal(2, resolved);
        Assert.Equal(1, connected);
    }

    [Fact]
    public async Task TrafficBudgetRejectsExcessResponseBeforeForwarding()
    {
        await using var proxy = new ImportTunnel(Policy(() => new MemoryStream(new byte[65])), TestContext.Current.CancellationToken, byteLimit: 64);
        using var client = await Client(proxy);
        await Write(client.GetStream(), "CONNECT example.org:443 HTTP/1.1\r\n" + Auth(proxy) + "\r\n");
        Assert.Contains("200 Connection Established", await Header(client.GetStream()), StringComparison.Ordinal);
        Assert.Equal(0, await Closed(client.GetStream()));
    }

    [Fact]
    public async Task CancellationClosesOwnedPendingConnection()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var proxy = new ImportTunnel(Policy(() => new MemoryStream()), cancellation.Token);
        using var client = await Client(proxy);
        await Write(client.GetStream(), "CONNECT example.org:443 HTTP/1.1\r\n");
        await cancellation.CancelAsync();
        Assert.Equal(0, await Closed(client.GetStream()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TunnelPreservesTlsHostnameValidationAndEncryptedPayload(bool wrongHost)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=example.org", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("example.org");
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = Task.Run(
            async () =>
            {
                try
                {
                    using var incoming = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
                    await using var tls = new SslStream(incoming.GetStream());
                    await tls.AuthenticateAsServerAsync(certificate);
                    var bytes = new byte[4];
                    await tls.ReadExactlyAsync(bytes, TestContext.Current.CancellationToken);
                    Assert.Equal("ping", Encoding.ASCII.GetString(bytes));
                    await tls.WriteAsync("pong"u8.ToArray(), TestContext.Current.CancellationToken);
                    await tls.FlushAsync(TestContext.Current.CancellationToken);
                }
                catch (Exception exception) when (wrongHost && exception is AuthenticationException or IOException)
                {
                    // Client rejects the certificate hostname and closes TLS.
                }
            },
            TestContext.Current.CancellationToken);
        var policy = new ImportNetworkPolicy((_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }), async (endpoint, token) =>
        {
            Assert.Equal(IPAddress.Parse("8.8.8.8"), endpoint.Address);
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(listener.LocalEndpoint, token);
            return new NetworkStream(socket, ownsSocket: true);
        });
        await using var proxy = new ImportTunnel(policy, TestContext.Current.CancellationToken);
        using var client = await Client(proxy);
        await Write(client.GetStream(), "CONNECT example.org:443 HTTP/1.1\r\n" + Auth(proxy) + "\r\n");
        Assert.Contains("200 Connection Established", await Header(client.GetStream()), StringComparison.Ordinal);
        await using var secure = new SslStream(client.GetStream());
        var chain = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck };
        chain.CustomTrustStore.Add(certificate);
        var options = new SslClientAuthenticationOptions { TargetHost = wrongHost ? "wrong.example.org" : "example.org", CertificateChainPolicy = chain };
        if (wrongHost)
        {
            await Assert.ThrowsAsync<AuthenticationException>(() => secure.AuthenticateAsClientAsync(options, TestContext.Current.CancellationToken));
            await secure.DisposeAsync();
            await server;
            return;
        }

        await secure.AuthenticateAsClientAsync(options, TestContext.Current.CancellationToken);
        await secure.WriteAsync("ping"u8.ToArray(), TestContext.Current.CancellationToken);
        var result = new byte[4];
        await secure.ReadExactlyAsync(result, TestContext.Current.CancellationToken);
        Assert.Equal("pong", Encoding.ASCII.GetString(result));
        await server;
    }

    [Fact]
    public async Task FifthConcurrentTunnelIsRejectedWithoutConnecting()
    {
        using var upstreamListener = new TcpListener(IPAddress.Loopback, 0);
        upstreamListener.Start();
        var connections = 0;
        var policy = new ImportNetworkPolicy(
            (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }),
            async (_, token) =>
            {
                Interlocked.Increment(ref connections);
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(upstreamListener.LocalEndpoint, token);
                return new NetworkStream(socket, ownsSocket: true);
            });
        await using var proxy = new ImportTunnel(policy, TestContext.Current.CancellationToken);
        var clients = new System.Collections.Generic.List<TcpClient>();
        try
        {
            for (var i = 0; i < 4; i++)
            {
                var client = await Client(proxy);
                clients.Add(client);
                await Write(client.GetStream(), "CONNECT example.org:443 HTTP/1.1\r\n" + Auth(proxy) + "\r\n");
                Assert.Contains("200 Connection Established", await Header(client.GetStream()), StringComparison.Ordinal);
            }

            using var excess = await Client(proxy);
            Assert.Equal(0, await Closed(excess.GetStream()));
            Assert.Equal(4, connections);
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }
    }

    private static ImportNetworkPolicy Policy(Func<Stream> connect)
        => new((_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }), (_, _) => Task.FromResult(connect()));

    private static string Auth(ImportTunnel proxy)
        => "Proxy-Authorization: Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(proxy.Proxy.UserInfo)) + "\r\n";

    private static async Task<TcpClient> Client(ImportTunnel proxy)
    {
        var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxy.Proxy.Port, TestContext.Current.CancellationToken);
        return client;
    }

    private static async Task Write(Stream stream, string value)
        => await stream.WriteAsync(Encoding.ASCII.GetBytes(value), TestContext.Current.CancellationToken);

    private static async Task<string> Header(Stream stream)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var result = new StringBuilder();
        var bytes = new byte[1];
        while (!result.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            Assert.Equal(1, await stream.ReadAsync(bytes, timeout.Token));
            result.Append((char)bytes[0]);
            Assert.True(result.Length < 4096);
        }

        return result.ToString();
    }

    private static async Task<int> Closed(Stream stream)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            return await stream.ReadAsync(new byte[1], timeout.Token);
        }
        catch (IOException)
        {
            return 0; // Reset is also a fail-closed connection.
        }
    }
}
