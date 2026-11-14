using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jellyfin.Api.Castlumora;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class YouTubePreviewStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "castlumora-confirmation-" + Guid.NewGuid().ToString("N"));
    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _library = Guid.NewGuid();

    [Fact]
    public void Receipt_SurvivesRestart_BindsIdentityAndResourcePolicy()
    {
        var options = YouTubeImportOptions.Select(YouTubeImportOptionsTests.Metadata(new YouTubeMetadataPreview.Format("18", 360)), 720, 128);
        var preview = new YouTubePreviewStore(_root).Create(_admin, _library, "https://www.youtube.com/watch?v=4Xsn6oe-AEg", "Reviewed", options);
        Assert.Equal(preview, new YouTubePreviewStore(_root).Read(preview.Id, _admin, _library));
        Assert.Throws<InvalidDataException>(() => new YouTubePreviewStore(_root).Read(preview.Id, Guid.NewGuid(), _library));
        Assert.Throws<InvalidDataException>(() => new YouTubePreviewStore(_root).Read(preview.Id, _admin, Guid.NewGuid()));
        var path = Directory.GetFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(path, JsonSerializer.Serialize(preview with { Expires = DateTimeOffset.UtcNow.AddSeconds(-1) }));
        Assert.Throws<InvalidDataException>(() => new YouTubePreviewStore(_root).Read(preview.Id, _admin, _library));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
