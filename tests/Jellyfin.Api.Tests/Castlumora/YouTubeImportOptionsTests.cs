using System;
using System.IO;
using Jellyfin.Api.Castlumora;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class YouTubeImportOptionsTests
{
    internal static YouTubeMetadataPreview.Metadata Metadata(params YouTubeMetadataPreview.Format[] formats)
        => new("4Xsn6oe-AEg", "Creator original title", "Retained description", "UCabcdefghijklmnopqrstuv", "Creator", 60, formats);

    [Fact]
    public void Select_ChoosesBoundedVideoAndAudio_WithoutStreamUrls()
    {
        var options = YouTubeImportOptions.Select(Metadata(new YouTubeMetadataPreview.Format("137", 1080, VideoOnly: true), new YouTubeMetadataPreview.Format("136", 720, VideoOnly: true), new YouTubeMetadataPreview.Format("140", 0, AudioOnly: true)), 720, 128);
        Assert.Equal("136", options.VideoFormat);
        Assert.Equal("140", options.AudioFormat);
        Assert.Equal(134217728, options.MaximumBytes);
        options.Validate("https://www.youtube.com/watch?v=4Xsn6oe-AEg");
    }

    [Fact]
    public void Select_PrefersMuxedAtEqualQuality()
    {
        var options = YouTubeImportOptions.Select(Metadata(new YouTubeMetadataPreview.Format("136", 720, VideoOnly: true), new YouTubeMetadataPreview.Format("22", 720), new YouTubeMetadataPreview.Format("140", 0, AudioOnly: true)), 720, 256);
        Assert.Equal("22", options.VideoFormat);
        Assert.Null(options.AudioFormat);
    }

    [Theory]
    [InlineData(2160, 128)]
    [InlineData(720, 10000)]
    [InlineData(720, -1)]
    public void Select_RejectsUnboundedPolicies(int height, int size)
        => Assert.Throws<InvalidDataException>(() => YouTubeImportOptions.Select(Metadata(new YouTubeMetadataPreview.Format("18", 360)), height, size));

    [Fact]
    public void Select_RejectsMissingAudioAndUnreliableIdentity()
    {
        Assert.Throws<InvalidDataException>(() => YouTubeImportOptions.Select(Metadata(new YouTubeMetadataPreview.Format("136", 720, VideoOnly: true)), 720, 128));
        var options = YouTubeImportOptions.Select(Metadata(new YouTubeMetadataPreview.Format("18", 360)), 720, 128);
        Assert.Throws<InvalidDataException>(() => (options with { VideoFormat = "18;exec" }).Validate("https://www.youtube.com/watch?v=4Xsn6oe-AEg"));
        Assert.Throws<InvalidDataException>(() => options.Validate("https://www.youtube.com/watch?v=abcdefghijk"));
        Assert.Throws<InvalidDataException>(() => (options with { MaximumBytes = long.MaxValue }).Validate("https://www.youtube.com/watch?v=4Xsn6oe-AEg"));
    }
}
