using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Api.Castlumora;

/// <summary>
/// Per-job authenticated loopback CONNECT transport. TLS remains end-to-end and must be
/// validated by the downloader. Not registered or exposed until queue supervision is implemented.
/// </summary>
internal sealed class ImportTunnel : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop;
    private readonly SemaphoreSlim _slots = new(4);
    private readonly List<Task> _clients = [];
    private readonly ImportNetworkPolicy _policy;
    private readonly byte[] _authorization;
    private readonly Task _accept;
    private long _remaining;

    internal ImportTunnel(ImportNetworkPolicy policy, CancellationToken cancellationToken, long byteLimit = 67108864)
    {
        if (byteLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLimit));
        }

        _policy = policy;
        _remaining = byteLimit;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _stop.CancelAfter(TimeSpan.FromMinutes(10));
        var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        _authorization = Encoding.ASCII.GetBytes("Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("castlumora:" + secret)));
        _listener.Start(8);
        Proxy = new Uri($"http://castlumora:{secret}@127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}");
        _accept = AcceptAsync();
    }

    // Contains a per-job secret: never log this value or persist it in job history.
    internal Uri Proxy { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        await _accept.ConfigureAwait(false);
        await Task.WhenAll(_clients).ConfigureAwait(false);
        _slots.Dispose();
        _stop.Dispose();
    }

    private async Task AcceptAsync()
    {
        try
        {
            // Bound total accepted connections as well as simultaneous tunnels and pending tasks.
            for (var count = 0; count < 32; count++)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
                if (!_slots.Wait(0))
                {
                    client.Dispose();
                    continue;
                }

                _clients.Add(ServeAsync(client));
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // Only the owned listener is stopped on cancellation/disposal.
        }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                using var headerTimeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                headerTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                var downstream = client.GetStream();
                var header = await ReadHeaderAsync(downstream, headerTimeout.Token).ConfigureAwait(false);
                var target = ParseHeader(header);
                await using var upstream = await _policy.ConnectAsync(target, _stop.Token).ConfigureAwait(false);
                await downstream.WriteAsync("HTTP/1.1 200 Connection Established\r\n\r\n"u8.ToArray(), headerTimeout.Token).ConfigureAwait(false);
                using var tunnel = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                var send = PumpAsync(downstream, upstream, tunnel.Token);
                var receive = PumpAsync(upstream, downstream, tunnel.Token);
                try
                {
                    await Task.WhenAny(send, receive).ConfigureAwait(false);
                }
                finally
                {
                    await tunnel.CancelAsync().ConfigureAwait(false);
                    await Task.WhenAll(send, receive).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or OperationCanceledException or SocketException or UnauthorizedAccessException or ObjectDisposedException)
            {
                // Fail closed. Do not reflect credentials, source URLs, DNS details or remote errors.
                // Caller observes a closed proxy connection; the future runner supplies a safe job error.
            }
            finally
            {
                _slots.Release();
            }
        }
    }

    private Uri ParseHeader(string header)
    {
        var lines = header.Split("\r\n", StringSplitOptions.None);
        var request = lines[0].Split(' ', StringSplitOptions.None);
        if (request.Length != 3 || request[0] != "CONNECT" || request[2] != "HTTP/1.1"
            || !request[1].EndsWith(":443", StringComparison.Ordinal)
            || request[1].Contains('/', StringComparison.Ordinal)
            || request[1].Contains('?', StringComparison.Ordinal))
        {
            throw new InvalidDataException("Only HTTPS tunnels are permitted.");
        }

        byte[]? authorization = null;
        var hostSeen = false;
        if (lines.Length > 19)
        {
            throw new InvalidDataException("Too many proxy headers.");
        }

        for (var i = 1; i < lines.Length - 2; i++)
        {
            var colon = lines[i].IndexOf(':', StringComparison.Ordinal);
            if (colon < 1)
            {
                throw new InvalidDataException("Invalid proxy header.");
            }

            var name = lines[i][..colon];
            var value = lines[i][(colon + 1)..].Trim(' ');
            if (name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) && authorization is null)
            {
                authorization = Encoding.ASCII.GetBytes(value);
            }
            else if (name.Equals("Host", StringComparison.OrdinalIgnoreCase) && !hostSeen && value == request[1])
            {
                hostSeen = true;
            }
            else if (!name.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)
                && !name.Equals("Proxy-Connection", StringComparison.OrdinalIgnoreCase))
            {
                // Reject duplicate auth/Host, request bodies, folding and transfer-encoding smuggling.
                throw new InvalidDataException("Unsupported proxy header.");
            }
        }

        if (authorization is null || !CryptographicOperations.FixedTimeEquals(authorization, _authorization))
        {
            throw new UnauthorizedAccessException("Proxy authentication required.");
        }

        return ImportNetworkPolicy.ValidateSource("https://" + request[1] + "/");
    }

    private static async Task<string> ReadHeaderAsync(Stream stream, CancellationToken token)
    {
        var bytes = new byte[4096];
        for (var count = 0; count < bytes.Length; count++)
        {
            if (await stream.ReadAsync(bytes.AsMemory(count, 1), token).ConfigureAwait(false) == 0
                || bytes[count] > 126 || (bytes[count] < 32 && bytes[count] is not (13 or 10)))
            {
                throw new InvalidDataException("Invalid proxy request.");
            }

            if (count >= 3 && bytes[count - 3] == 13 && bytes[count - 2] == 10 && bytes[count - 1] == 13 && bytes[count] == 10)
            {
                return Encoding.ASCII.GetString(bytes, 0, count + 1);
            }
        }

        throw new InvalidDataException("Proxy header limit exceeded.");
    }

    private async Task PumpAsync(Stream source, Stream destination, CancellationToken token)
    {
        var bytes = new byte[16384];
        while (true)
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(token);
            idle.CancelAfter(TimeSpan.FromSeconds(30));
            var count = await source.ReadAsync(bytes, idle.Token).ConfigureAwait(false);
            if (count == 0)
            {
                return;
            }

            if (Interlocked.Add(ref _remaining, -count) < 0)
            {
                throw new InvalidDataException("Import tunnel traffic limit exceeded.");
            }

            await destination.WriteAsync(bytes.AsMemory(0, count), idle.Token).ConfigureAwait(false);
        }
    }
}
