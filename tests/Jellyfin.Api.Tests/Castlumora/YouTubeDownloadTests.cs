using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

[Collection(nameof(ImportExecutableCollection))]
public sealed class YouTubeDownloadTests
{
    [Fact]
    public async Task RealMediaAboveLegacyLimit_RequiresExplicitBoundedJobPolicy()
    {
        var root = Path.Combine(Path.GetTempPath(), "castlumora-large-owned-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "import-owned.mp4"), TestContext.Current.CancellationToken);
            var position = 0;
            while (System.Text.Encoding.ASCII.GetString(bytes, position + 4, 4) != "mdat")
            {
                position += BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(position, 4));
            }

            Assert.Equal(bytes.Length, position + BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(position, 4)));
            const int length = 36 * 1048576;
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(position, 4), length - position);
            var path = Path.Combine(root, "owned.mp4");
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
            using (var file = File.OpenWrite(path))
            {
                file.SetLength(length);
            }

            Assert.Throws<InvalidDataException>(() => ImportMediaInspection.CheckContainer(path));
            var seconds = await ImportMediaInspection.InspectAsync(
                path,
                Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFPROBE")!,
                Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFMPEG")!,
                TestContext.Current.CancellationToken,
                134217728,
                720);
            Assert.True(seconds > 0);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("18;exec", "video.mp4", 33554432)]
    [InlineData("18", "../outside.mp4", 33554432)]
    [InlineData("18", "video.mp4", long.MaxValue)]
    public void Command_RejectsInjectedFormatsPathsAndUnboundedResources(string format, string output, long bytes)
        => Assert.Throws<InvalidDataException>(() => YouTubeDownload.CreateStart("/tool", "/node", "/private/files", "/private/tmp", new Uri("http://user:secret@127.0.0.1:4000"), format, output, bytes));

    [Fact]
    public void Command_UsesSingleNumericFormat_NoAutomaticMuxOrRemoteComponents()
    {
        var start = YouTubeDownload.CreateStart("/tool", "/node", "/private/files", "/private/tmp", new Uri("http://user:secret@127.0.0.1:4000"), "137", "video.mp4", 134217728);
        Assert.Contains("137", start.ArgumentList);
        Assert.Contains("--no-remote-components", start.ArgumentList);
        Assert.Contains("never", start.ArgumentList);
        Assert.DoesNotContain("--skip-download", start.ArgumentList);
        Assert.DoesNotContain("--dump-single-json", start.ArgumentList);
        Assert.DoesNotContain("--exec", start.ArgumentList);
        Assert.Equal("/private/files", start.WorkingDirectory);
        Assert.Equal("--max-old-space-size=256", start.Environment["NODE_OPTIONS"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealOwnedStreams_MuxLocally_AndPassFinalNativeInspection(bool fragmented)
    {
        var encoder = Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFMPEG") ?? throw new InvalidOperationException("Native encoder fixture required.");
        var probe = Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFPROBE") ?? throw new InvalidOperationException("Native probe fixture required.");
        var root = Path.Combine(Path.GetTempPath(), "castlumora-youtube-mux-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var clip = Path.Combine(AppContext.BaseDirectory, "Fixtures", "import-owned.mp4");
            var video = clip;
            string? audio = null;
            var token = TestContext.Current.CancellationToken;
            if (fragmented)
            {
                video = Path.Combine(root, "video.mp4");
                audio = Path.Combine(root, "audio.m4a");
                foreach (var pair in new[] { (Path: video, Map: "0:v:0"), (Path: audio, Map: "0:a:0") })
                {
                    var command = new ProcessStartInfo(encoder);
                    foreach (var arg in new[] { "-hide_banner", "-v", "error", "-nostdin", "-i", clip, "-map", pair.Map, "-c", "copy", "-movflags", "frag_keyframe+empty_moov", pair.Path })
                    {
                        command.ArgumentList.Add(arg);
                    }

                    var result = await ImportProcess.RunAsync(command, string.Empty, TimeSpan.FromSeconds(20), 16384, ct => Task.Delay(Timeout.InfiniteTimeSpan, ct), token);
                    Assert.Equal(0, result.ExitCode);
                }

                Assert.Throws<InvalidDataException>(() => ImportMediaInspection.CheckContainer(video));
            }

            var output = Path.Combine(root, "media.mp4");
            await YouTubeDownload.MuxAsync(video, audio, output, encoder, 33554432, token);
            var seconds = await ImportMediaInspection.InspectAsync(output, probe, encoder, token, 33554432, 720);
            Assert.True(seconds > 0);
            Assert.True(File.Exists(clip));
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => YouTubeDownload.MuxAsync(video, audio, Path.Combine(root, "cancelled.mp4"), encoder, 33554432, cancelled.Token));
            Assert.False(File.Exists(Path.Combine(root, "cancelled.mp4")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
