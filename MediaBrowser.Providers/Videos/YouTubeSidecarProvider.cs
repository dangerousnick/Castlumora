using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Videos;

/// <summary>
/// Reads reliably identified, retained YouTube metadata without downloading media or contacting a source.
/// Applies only to explicitly generic videos, never movie/episode identification.
/// </summary>
public sealed class YouTubeSidecarProvider : ILocalMetadataProvider<Video>, IHasOrder, IHasItemChangeMonitor
{
    private const int MaximumBytes = 2 * 1024 * 1024;
    private readonly ILibraryManager _library;
    private readonly ILogger<YouTubeSidecarProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="YouTubeSidecarProvider"/> class.
    /// </summary>
    /// <param name="library">Existing library options, including disabled local readers.</param>
    /// <param name="logger">Diagnostic logger.</param>
    public YouTubeSidecarProvider(ILibraryManager library, ILogger<YouTubeSidecarProvider> logger)
    {
        _library = library;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Castlumora YouTube sidecar";

    /// <inheritdoc />
    // Explicit local NFO corrections have priority by default.
    public int Order => 1;

    /// <inheritdoc />
    public async Task<MetadataResult<Video>> GetMetadata(ItemInfo info, IDirectoryService directoryService, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Video>();
        if (info.ItemType != typeof(Video) || info.VideoType != VideoType.VideoFile || string.IsNullOrWhiteSpace(info.Path) || !Path.IsPathFullyQualified(info.Path))
        {
            return result;
        }

        var item = new Video { Path = info.Path, ParentId = info.ParentId };
        if (_library.GetLibraryOptions(item).DisabledLocalMetadataReaders?.Contains(Name, StringComparer.OrdinalIgnoreCase) == true)
        {
            return result;
        }

        // JSON cannot choose another file, thumbnail URL, destination or executable.
        var path = Path.ChangeExtension(info.Path, ".info.json");
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.LinkTarget is not null || file.Length > MaximumBytes)
            {
                return result;
            }

            // Bound actual reads as well as initial size, even if the file grows concurrently.
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
            var bytes = new byte[MaximumBytes + 1];
            var length = 0;
            while (length < bytes.Length)
            {
                var count = await stream.ReadAsync(bytes.AsMemory(length), cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }

                length += count;
            }

            if (length > MaximumBytes)
            {
                return result;
            }

            using var document = JsonDocument.Parse(bytes.AsMemory(0, length), new JsonDocumentOptions { MaxDepth = 32 });
            var json = document.RootElement;
            if (json.ValueKind != JsonValueKind.Object || json.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != json.GetPropertyCount())
            {
                return result;
            }

            var id = Text(json, "id", 11);
            if (!ValidIdentifier(id, 11)
                || !(string.Equals(Text(json, "extractor_key", 40), "Youtube", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Text(json, "extractor", 40), "youtube", StringComparison.OrdinalIgnoreCase)))
            {
                return result;
            }

            var url = Text(json, "webpage_url", 2048);
            if (json.TryGetProperty("webpage_url", out _) && !MatchesSourceUrl(url, id))
            {
                return result;
            }

            // A recognizable filename ID must agree with the sidecar, not merely its title.
            var filename = Path.GetFileNameWithoutExtension(info.Path);
            for (var index = 0; index + 12 < filename.Length; index++)
            {
                if (filename[index] == '[' && filename[index + 12] == ']')
                {
                    var embedded = filename.Substring(index + 1, 11);
                    if (ValidIdentifier(embedded, 11) && !string.Equals(embedded, id, StringComparison.Ordinal))
                    {
                        return result;
                    }
                }
            }

            var title = Text(json, "title", 1000);
            if (string.IsNullOrWhiteSpace(title))
            {
                return result;
            }

            item.Name = title;
            item.OriginalTitle = title;
            item.Overview = WebUtility.HtmlEncode(Text(json, "description", 65536));
            item.ProviderIds["YouTube"] = id;
            item.Tags = ["CreatorVideo", "YouTube"];
            var channelId = Text(json, "channel_id", 24);
            if (channelId.StartsWith("UC", StringComparison.Ordinal) && ValidIdentifier(channelId, 24))
            {
                item.ProviderIds["YouTubeChannel"] = channelId;
                var channel = Text(json, "channel", 256);
                if (!string.IsNullOrWhiteSpace(channel))
                {
                    item.Studios = [channel];
                }
            }

            if (DateTime.TryParseExact(Text(json, "upload_date", 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
            {
                item.PremiereDate = date;
                item.ProductionYear = date.Year;
            }

            // Runtime, streams, parental policy and manual locks remain server-authoritative.
            result.Item = item;
            result.Provider = Name;
            result.HasMetadata = true;
            result.QueriedById = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogDebug(exception, "Could not read retained YouTube sidecar metadata");
        }

        return result;
    }

    /// <inheritdoc />
    public bool HasChanged(BaseItem item, IDirectoryService directoryService)
    {
        if (item.GetType() != typeof(Video) || string.IsNullOrWhiteSpace(item.Path) || !Path.IsPathFullyQualified(item.Path))
        {
            return false;
        }

        var file = new FileInfo(Path.ChangeExtension(item.Path, ".info.json"));
        return file.Exists && file.LinkTarget is null && file.Length <= MaximumBytes && file.LastWriteTimeUtc > item.DateLastSaved;
    }

    private static string Text(JsonElement json, string name, int maximum)
        => json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } text && text.Length <= maximum ? text : string.Empty;

    private static bool ValidIdentifier(string value, int length)
        => value.Length == length && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    private static bool MatchesSourceUrl(string value, string id)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length > 0)
        {
            return false;
        }

        // Canonical retained source URLs only; never issue an HTTP request or follow redirects.
        return (uri.Host is "youtube.com" or "www.youtube.com" or "m.youtube.com" && uri.AbsolutePath == "/watch" && uri.Query == "?v=" + id)
            || (uri.Host == "youtu.be" && uri.AbsolutePath == "/" + id && uri.Query.Length == 0);
    }
}
