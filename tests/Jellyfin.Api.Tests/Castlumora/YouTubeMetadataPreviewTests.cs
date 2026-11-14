using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class YouTubeMetadataPreviewTests
{
    private const string VideoId = "4Xsn6oe-AEg";

    [Fact]
    public void Parse_ReturnsIdentityAndSafeFormatsWithoutSignedUrls()
    {
        var input = Valid();
        input["url"] = "https://example.com/private?secret=credential";
        var result = YouTubeMetadataPreview.Parse(input.ToJsonString(), new YouTubeVideoSource(VideoId));
        Assert.Equal(VideoId, result.VideoId);
        Assert.Equal("UCabcdefghijklmnopqrstuv", result.ChannelId);
        Assert.Single(result.Formats);
        Assert.Equal("18", result.Formats[0].Id);
        Assert.DoesNotContain("credential", System.Text.Json.JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("id", "differentID")]
    [InlineData("extractor_key", "Generic")]
    [InlineData("channel_id", "Unreliable creator")]
    [InlineData("title", "unsafe\nname")]
    [InlineData("_type", "playlist")]
    public void Parse_RejectsUnreliableIdentity(string field, string value)
    {
        var input = Valid();
        input[field] = value;
        Assert.Throws<InvalidDataException>(() => YouTubeMetadataPreview.Parse(input.ToJsonString(), new YouTubeVideoSource(VideoId)));
    }

    [Theory]
    [InlineData("has_drm")]
    [InlineData("is_live")]
    public void Parse_RejectsProtectedOrLiveSources(string field)
    {
        var input = Valid();
        input[field] = true;
        Assert.Throws<InvalidDataException>(() => YouTubeMetadataPreview.Parse(input.ToJsonString(), new YouTubeVideoSource(VideoId)));
    }

    [Fact]
    public void Parse_RejectsDuplicateIdentityKeys()
    {
        var input = Valid().ToJsonString().Insert(1, "\"id\":\"conflicting\",");
        Assert.Throws<InvalidDataException>(() => YouTubeMetadataPreview.Parse(input, new YouTubeVideoSource(VideoId)));
    }

    [Fact]
    public void Command_IsIsolatedAndNeverDownloadsRemoteComponents()
    {
        var start = YouTubeMetadataPreview.CreateStart("/reviewed/yt-dlp", "/reviewed/node", "/private/preview", new Uri("http://nonce:secret@127.0.0.1:4000"));
        Assert.Contains("--no-remote-components", start.ArgumentList);
        Assert.Contains("--no-plugin-dirs", start.ArgumentList);
        Assert.Contains("--no-playlist", start.ArgumentList);
        Assert.Contains("node:/reviewed/node", start.ArgumentList);
        Assert.Contains("youtube:player_client=web", start.ArgumentList);
        Assert.Equal("--max-old-space-size=256", start.Environment["NODE_OPTIONS"]);
        Assert.DoesNotContain("--cookies", start.ArgumentList);
        Assert.DoesNotContain("--no-check-certificates", start.ArgumentList);
    }

    [Fact]
    public async Task AuthorizedExternalSource_UsesManagedRuntimeAndNormalNetworkPolicy()
    {
        var source = Environment.GetEnvironmentVariable("CASTLUMORA_TEST_AUTHORIZED_YOUTUBE_URL");
        if (string.IsNullOrEmpty(source))
        {
            Assert.Skip("Requires an explicitly authorized public video and normal public network access.");
            return;
        }

        var system = Environment.GetEnvironmentVariable("CASTLUMORA_TEST_YTDLP_BUNDLE") ?? throw new InvalidOperationException("Managed yt-dlp fixture required.");
        var node = Environment.GetEnvironmentVariable("CASTLUMORA_TEST_NODE_BUNDLE") ?? throw new InvalidOperationException("Managed Node fixture required.");
        var data = Path.Combine(Path.GetTempPath(), "castlumora-youtube-preview-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var runtime = new YtDlpRuntime(data, system, YtDlpPlatform.Detect(YtDlpPlatform.CurrentId()), YtDlpRuntime.ProbeAsync);
            await runtime.InstallAsync(TestContext.Current.CancellationToken);
            var result = await YouTubeMetadataPreview.ReadAsync(runtime, node, data, source, TestContext.Current.CancellationToken);
            Assert.Equal(YouTubeVideoSource.Parse(source).VideoId, result.VideoId);
            Assert.NotEmpty(result.Title);
        }
        finally
        {
            if (Directory.Exists(data))
            {
                Directory.Delete(data, recursive: true);
            }
        }
    }

    private static JsonObject Valid() => new()
    {
        ["id"] = VideoId,
        ["extractor_key"] = "Youtube",
        ["title"] = "Authorized creator video",
        ["channel_id"] = "UCabcdefghijklmnopqrstuv",
        ["channel"] = "Creator",
        ["duration"] = 60,
        ["has_drm"] = false,
        ["is_live"] = false,
        ["formats"] = new JsonArray(new JsonObject
        {
            ["format_id"] = "18",
            ["protocol"] = "https",
            ["ext"] = "mp4",
            ["vcodec"] = "avc1.42001E",
            ["acodec"] = "mp4a.40.2",
            ["height"] = 360
        })
    };
}
