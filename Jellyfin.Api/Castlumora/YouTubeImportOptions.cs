using System;
using System.IO;
using System.Linq;

namespace Jellyfin.Api.Castlumora;

/// <summary>Validated, durable source identity and administrator-selected resource snapshot.</summary>
internal sealed record YouTubeImportOptions(string VideoId, string ChannelId, string Creator, string Description, string OriginalTitle, double Duration, string VideoFormat, string? AudioFormat, int Height, long MaximumBytes)
{
    internal const long AbsoluteMaximumBytes = 536870912;

    internal static YouTubeImportOptions Select(YouTubeMetadataPreview.Metadata preview, int maximumHeight, int maximumMiB)
    {
        if (maximumHeight is not (360 or 480 or 720 or 1080) || maximumMiB is not (32 or 128 or 256 or 512))
        {
            throw new InvalidDataException("Choose a supported quality and size budget.");
        }

        var video = preview.Formats.Where(format => !format.AudioOnly && format.Height <= maximumHeight)
            .OrderByDescending(format => format.Height).ThenBy(format => format.VideoOnly).FirstOrDefault()
            ?? throw new InvalidDataException("The source offers no supported H264 video within the selected quality.");
        var audio = video.VideoOnly ? preview.Formats.FirstOrDefault(format => format.AudioOnly)?.Id
            ?? throw new InvalidDataException("The source offers no supported AAC audio for this video.") : null;
        var options = new YouTubeImportOptions(preview.VideoId, preview.ChannelId, Clean(preview.Creator, 300), Clean(preview.Description, 4000), Clean(preview.Title, 1000), preview.Duration, video.Id, audio, video.Height, maximumMiB * 1048576L);
        options.Validate(new YouTubeVideoSource(preview.VideoId).CanonicalUrl);
        return options;
    }

    internal void Validate(string source)
    {
        var parsed = YouTubeVideoSource.Parse(source);
        if (parsed.VideoId != VideoId || parsed.CanonicalUrl != source || ChannelId is not { Length: 24 } || !ChannelId.StartsWith("UC", StringComparison.Ordinal)
            || ChannelId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-'))
            || Creator is null || Creator.Length > 300 || Description is null || Description.Length > 4000
            || OriginalTitle is null || OriginalTitle.Length > 1000 || OriginalTitle.Any(char.IsControl)
            || Creator.Any(char.IsControl) || Description.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))
            || !double.IsFinite(Duration) || Duration is <= 0 or > 10800
            || !FormatId(VideoFormat) || (AudioFormat is not null && (!FormatId(AudioFormat) || AudioFormat == VideoFormat))
            || Height is <= 0 or > 1080 || MaximumBytes is not (33554432 or 134217728 or 268435456 or AbsoluteMaximumBytes))
        {
            throw new InvalidDataException("Invalid YouTube job identity, format or resource policy.");
        }
    }

    private static bool FormatId(string? value) => value is { Length: > 0 and <= 16 } && value.All(char.IsAsciiDigit);

    private static string Clean(string value, int maximum) => new(value.Where(c => !char.IsControl(c) || c is '\n' or '\r' or '\t').Take(maximum).ToArray());
}
