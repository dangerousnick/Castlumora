using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class ImportNetworkPolicyTests
{
    [Theory]
    [InlineData("http://example.org/video.mp4")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://user:secret@example.org/video")]
    [InlineData("https://example.org:444/video")]
    [InlineData("https://example.org/video#fragment")]
    [InlineData("https://localhost/video")]
    [InlineData("https://sub.localhost./video")]
    [InlineData("https://server.local/video")]
    [InlineData("https://127.1/video")]
    [InlineData("https://2130706433/video")]
    [InlineData("https://0x7f000001/video")]
    [InlineData("https://[::ffff:127.0.0.1]/video")]
    [InlineData("https://example.org\\@127.0.0.1/video")]
    [InlineData(" https://example.org/video")]
    [InlineData("https://example.org/a\nb")]
    public void UnsafeSourceIsRejected(string url)
        => Assert.Throws<InvalidDataException>(() => ImportNetworkPolicy.ValidateSource(url));

    [Theory]
    [InlineData("0.1.2.3")]
    [InlineData("10.1.2.3")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.254")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.254")]
    [InlineData("192.0.0.9")]
    [InlineData("192.0.2.1")]
    [InlineData("192.88.99.1")]
    [InlineData("192.168.1.1")]
    [InlineData("198.18.0.1")]
    [InlineData("198.19.255.254")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1%1")]
    [InlineData("ff02::1")]
    [InlineData("2001::1")]
    [InlineData("2001:20::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2002:7f00:1::1")]
    [InlineData("3fff::1")]
    public void NonPublicAddressIsRejected(string ip)
        => Assert.False(ImportNetworkPolicy.IsPublic(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("100.63.255.254")]
    [InlineData("100.128.0.1")]
    [InlineData("172.15.255.254")]
    [InlineData("172.32.0.1")]
    [InlineData("::ffff:8.8.8.8")]
    [InlineData("2001:4860:4860::8888")]
    [InlineData("2606:4700:4700::1111")]
    public void PublicAddressIsAccepted(string ip)
        => Assert.True(ImportNetworkPolicy.IsPublic(IPAddress.Parse(ip)));

    [Fact]
    public async Task MixedDnsAnswersFailClosedBeforeConnecting()
    {
        var connected = false;
        var policy = new ImportNetworkPolicy(
            (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Loopback }),
            (_, _) =>
            {
                connected = true;
                return Task.FromResult<Stream>(new MemoryStream());
            });
        await Assert.ThrowsAsync<InvalidDataException>(() => policy.ConnectAsync(new Uri("https://example.org/video"), TestContext.Current.CancellationToken));
        Assert.False(connected);
    }

    [Fact]
    public async Task ConnectionUsesNumericValidatedEndpointAndRevalidatesOnEveryConnection()
    {
        var lookups = 0;
        var connections = 0;
        var policy = new ImportNetworkPolicy(
            (_, _) => Task.FromResult(new[] { ++lookups == 1 ? IPAddress.Parse("8.8.8.8") : IPAddress.Loopback }),
            (endpoint, _) =>
            {
                Assert.Equal(IPAddress.Parse("8.8.8.8"), endpoint.Address);
                Assert.Equal(443, endpoint.Port);
                connections++;
                return Task.FromResult<Stream>(new MemoryStream());
            });
        using var stream = await policy.ConnectAsync(new Uri("https://example.org/video"), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => policy.ConnectAsync(new Uri("https://example.org/redirect"), TestContext.Current.CancellationToken));
        Assert.Equal(2, lookups);
        Assert.Equal(1, connections);
    }

    [Fact]
    public async Task EmptyOrExcessiveDnsAnswerIsRejected()
    {
        foreach (var addresses in new[] { Array.Empty<IPAddress>(), new IPAddress[65] })
        {
            var policy = new ImportNetworkPolicy((_, _) => Task.FromResult(addresses), (_, _) => throw new InvalidOperationException("Must not connect"));
            await Assert.ThrowsAsync<InvalidDataException>(() => policy.ConnectAsync(new Uri("https://example.org/video"), TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task CancellationStopsResolutionWithoutConnecting()
    {
        using var cancellation = new CancellationTokenSource();
        var policy = new ImportNetworkPolicy(
            async (_, token) =>
            {
                await cancellation.CancelAsync();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return [];
            },
            (_, _) => throw new InvalidOperationException("Must not connect"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => policy.ConnectAsync(new Uri("https://example.org/video"), cancellation.Token));
    }

    [Fact]
    public async Task PublicLiteralDoesNotPerformDnsLookup()
    {
        var policy = new ImportNetworkPolicy(
            (_, _) => throw new InvalidOperationException("Must not resolve a literal"),
            (endpoint, _) =>
            {
                Assert.Equal(IPAddress.Parse("1.1.1.1"), endpoint.Address);
                return Task.FromResult<Stream>(new MemoryStream());
            });
        using var stream = await policy.ConnectAsync(new Uri("https://1.1.1.1/video"), TestContext.Current.CancellationToken);
    }
}
