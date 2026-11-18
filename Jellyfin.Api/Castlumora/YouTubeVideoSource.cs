using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Jellyfin.Api.Castlumora;

/// <summary>Canonical single-video identity, independent of tracking and playlist context.</summary>
internal sealed record YouTubeVideoSource(string VideoId)
{
    internal string CanonicalUrl => "https://www.youtube.com/watch?v=" + VideoId;

    internal string StableKey => "youtube:" + VideoId;

    internal static YouTubeVideoSource Parse(string source)
    {
        var uri = ImportNetworkPolicy.ValidateSource(source);
        if (source.Contains('%', StringComparison.Ordinal))
        {
            throw new InvalidDataException("Use a plain YouTube video link without encoded path or query parameters.");
        }

        var host = uri.IdnHost.ToLowerInvariant();
        if (host is not ("youtube.com" or "www.youtube.com" or "m.youtube.com" or "youtu.be"))
        {
            throw new InvalidDataException("Use a supported HTTPS YouTube single-video URL.");
        }

        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2 || parts[0] is not ("v" or "t" or "start" or "feature" or "si" or "list" or "index" or "pp")
                || !query.TryAdd(parts[0], parts[1]))
            {
                throw new InvalidDataException("Unsupported or ambiguous YouTube URL parameters; provide a plain single-video link.");
            }
        }

        var path = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? id = null;
        if (host == "youtu.be" && path.Length == 1 && !query.ContainsKey("v"))
        {
            id = path[0];
        }
        else if (host != "youtu.be" && uri.AbsolutePath == "/watch" && query.TryGetValue("v", out var video))
        {
            id = video;
        }
        else if (host != "youtu.be" && path.Length == 2 && path[0] is "shorts" or "live" or "embed" && !query.ContainsKey("v"))
        {
            id = path[1];
        }

        if (id is null || id.Length != 11 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-')))
        {
            throw new InvalidDataException("A valid eleven-character YouTube video ID is required; playlists and channels are not single-video sources.");
        }

        // Only the video identity survives. The caller must still perform rights/preview
        // confirmation and every extraction connection remains subject to egress policy.
        return new YouTubeVideoSource(id);
    }
}
