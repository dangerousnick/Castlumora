using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Api.Castlumora;

/// <summary>Internal, bounded source preview; never publishes media or exposes signed stream URLs.</summary>
internal static class YouTubeMetadataPreview
{
    internal static async Task<Metadata> ReadAsync(YtDlpRuntime runtime, string nodeRoot, string dataRoot, string source, CancellationToken token)
    {
        var video = YouTubeVideoSource.Parse(source);
        var node = await YouTubeNodeRuntime.VerifyAsync(nodeRoot, token).ConfigureAwait(false);
        using var lease = await runtime.AcquireAsync(token).ConfigureAwait(false);
        var parent = Path.Combine(dataRoot, "castlumora", "imports", "previews");
        for (var current = parent; current is not null; current = Path.GetDirectoryName(current))
        {
            YtDlpPlatform.RejectLink(current);
        }

        YtDlpPlatform.PrivateDirectory(parent);
        var temporary = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        YtDlpPlatform.PrivateDirectory(temporary);
        try
        {
            await using var tunnel = new ImportTunnel(new ImportNetworkPolicy(), token);
            var start = CreateStart(lease.Executable, node, temporary, tunnel.Proxy);
            var result = await ImportProcess.RunAsync(start, video.CanonicalUrl, TimeSpan.FromSeconds(90), 2097152, ct => Task.Delay(Timeout.InfiniteTimeSpan, ct), token).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                throw ImportFailure.Download(result.ErrorCode == "downloadFailed" && tunnel.HasConnectionFailure ? "sourceNetwork" : result.ErrorCode);
            }

            return Parse(result.Output, video);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    internal static ProcessStartInfo CreateStart(string executable, string node, string temporary, Uri proxy)
    {
        var start = new ProcessStartInfo(executable) { WorkingDirectory = temporary };
        foreach (var arg in new[]
        {
            "--ignore-config", "--no-plugin-dirs", "--no-cache-dir", "--no-playlist", "--playlist-items", "1",
            "--no-remote-components", "--no-js-runtimes", "--js-runtimes", "node:" + node,
            "--use-extractors", "youtube", "--extractor-args", "youtube:player_client=web", "--xff", "never",
            "--skip-download", "--dump-single-json", "--quiet", "--no-warnings", "--socket-timeout", "10", "--retries", "1", "--batch-file", "-"
        })
        {
            start.ArgumentList.Add(arg);
        }

        start.Environment.Clear();
        foreach (var name in new[] { "HOME", "TMPDIR", "TMP", "TEMP" })
        {
            start.Environment[name] = temporary;
        }

        foreach (var name in new[] { "http_proxy", "https_proxy", "all_proxy", "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY" })
        {
            start.Environment[name] = proxy.AbsoluteUri;
        }

        start.Environment["PATH"] = "/usr/bin:/bin";
        start.Environment["LANG"] = "C.UTF-8";
        start.Environment["NODE_OPTIONS"] = "--max-old-space-size=256";
        return start;
    }

    internal static Metadata Parse(string output, YouTubeVideoSource source)
    {
        if (output.Length > 2097152)
        {
            throw new InvalidDataException("YouTube preview metadata exceeded its limit.");
        }

        try
        {
            using var json = JsonDocument.Parse(output, new JsonDocumentOptions { MaxDepth = 32 });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != root.GetPropertyCount()
                || Text(root, "id", 11) != source.VideoId || Text(root, "extractor_key", 40) != "Youtube"
                || (root.TryGetProperty("_type", out var kind) && kind.GetString() is not (null or "video"))
                || (root.TryGetProperty("has_drm", out var drm) && drm.ValueKind != JsonValueKind.False)
                || (root.TryGetProperty("is_live", out var live) && live.ValueKind != JsonValueKind.False))
            {
                throw new InvalidDataException("The source is not a matching unrestricted single video.");
            }

            var title = Text(root, "title", 1000);
            var channel = Text(root, "channel_id", 24);
            if (string.IsNullOrWhiteSpace(title) || title.Any(char.IsControl)
                || channel.Length != 24 || !channel.StartsWith("UC", StringComparison.Ordinal)
                || channel.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-'))
                || !root.TryGetProperty("duration", out var duration) || !duration.TryGetDouble(out var seconds)
                || !double.IsFinite(seconds) || seconds is <= 0 or > 10800)
            {
                throw new InvalidDataException("YouTube preview requires a reliable creator identity and finite supported duration.");
            }

            var formats = new List<Format>();
            if (root.TryGetProperty("formats", out var list) && list.ValueKind == JsonValueKind.Array && list.GetArrayLength() <= 1000)
            {
                foreach (var item in list.EnumerateArray())
                {
                    var id = Text(item, "format_id", 16);
                    var video = Text(item, "vcodec", 80);
                    var audio = Text(item, "acodec", 80);
                    if (id.Length == 0 || !id.All(char.IsAsciiDigit) || Text(item, "protocol", 30) != "https"
                        || Text(item, "ext", 8) != "mp4" || !video.StartsWith("avc1", StringComparison.Ordinal)
                        || !audio.StartsWith("mp4a", StringComparison.Ordinal)
                        || (item.TryGetProperty("has_drm", out var protectedFormat) && protectedFormat.ValueKind != JsonValueKind.False))
                    {
                        continue;
                    }

                    var height = item.TryGetProperty("height", out var h) && h.TryGetInt32(out var value) ? value : 0;
                    if (height is > 0 and <= 2160)
                    {
                        formats.Add(new Format(id, height));
                    }
                }
            }

            return new Metadata(source.VideoId, title, Text(root, "description", 50000), channel, Text(root, "channel", 500), seconds, formats.Distinct().ToArray());
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("YouTube preview metadata is malformed or unsupported.");
        }
    }

    private static string Text(JsonElement json, string key, int maximum)
        => json.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } text && text.Length <= maximum ? text : string.Empty;

    internal sealed record Format(string Id, int Height);

    internal sealed record Metadata(string VideoId, string Title, string Description, string ChannelId, string Creator, double Duration, Format[] Formats);
}
