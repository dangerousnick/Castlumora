using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Api.Castlumora;

/// <summary>
/// Import-only HTTPS egress policy. Each connection resolves again, rejects the complete answer
/// if any address is unsafe, and connects to an approved numeric endpoint without a second DNS lookup.
/// This is not a general server networking policy or an enabled downloader endpoint.
/// </summary>
internal sealed class ImportNetworkPolicy
{
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolve;
    private readonly Func<IPEndPoint, CancellationToken, Task<Stream>> _connect;

    internal ImportNetworkPolicy()
        : this((host, token) => Dns.GetHostAddressesAsync(host, token), ConnectSocketAsync)
    {
    }

    internal ImportNetworkPolicy(
        Func<string, CancellationToken, Task<IPAddress[]>> resolve,
        Func<IPEndPoint, CancellationToken, Task<Stream>> connect)
    {
        _resolve = resolve;
        _connect = connect;
    }

    internal static Uri ValidateSource(string source)
    {
        if (string.IsNullOrEmpty(source) || source.Length > 4096
            || source.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '\\')
            || !Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443
            || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0
            || uri.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6)
            || uri.IdnHost.Contains('%', StringComparison.Ordinal))
        {
            throw new InvalidDataException("Imports require an absolute HTTPS URL on port 443 without credentials or fragments.");
        }

        var host = uri.IdnHost.Trim('[', ']').TrimEnd('.');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out var address) && !IsPublic(address)))
        {
            throw new InvalidDataException("Import sources must use publicly routable addresses.");
        }

        return uri;
    }

    internal async Task<Stream> ConnectAsync(Uri source, CancellationToken cancellationToken)
    {
        // Revalidate even internally constructed URIs; never trust a redirect or a proxy CONNECT target.
        source = ValidateSource(source.AbsoluteUri);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var host = source.IdnHost.Trim('[', ']').TrimEnd('.');
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await _resolve(host, timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);
        if (addresses.Length == 0 || addresses.Length > 64 || addresses.Any(address => !IsPublic(address)))
        {
            throw new InvalidDataException("Import source DNS must resolve exclusively to public addresses.");
        }

        // Fixed port and numeric endpoint prevent DNS rebinding between validation and connection.
        // A failed connection is retried by the job, with a fresh validation; no implicit fallback resolver.
        return await _connect(new IPEndPoint(addresses[0], 443), timeout.Token).ConfigureAwait(false);
    }

    internal static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(bytes[0] is 0 or 10 or 127 or >= 224
                || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && ((bytes[1] == 0 && bytes[2] is 0 or 2)
                    || (bytes[1] == 88 && bytes[2] == 99) || bytes[1] == 168))
                || (bytes[0] == 198 && (bytes[1] is 18 or 19 || (bytes[1] == 51 && bytes[2] == 100)))
                || (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113));
        }

        // Conservatively admit only global unicast, excluding protocol assignments, documentation,
        // 6to4 and newer documentation space. No scoped, mapped-private, NAT64, local or multicast route.
        return address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId == 0
            && (bytes[0] & 0xe0) == 0x20
            && !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] < 2)
            && !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8)
            && !(bytes[0] == 0x20 && bytes[1] == 0x02)
            && !(bytes[0] == 0x3f && (bytes[1] & 0xf0) == 0xf0);
    }

    private static async Task<Stream> ConnectSocketAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
    {
        var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
