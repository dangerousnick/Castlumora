using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

[Collection(nameof(ImportExecutableCollection))]
public sealed class ImportMediaInspectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "castlumora-inspection-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ActualGeneratedH264AacFixture_PassesStructureAndNativeBoundedProbe()
    {
        var file = Fixture();
        ImportMediaInspection.CheckContainer(file);
        var duration = await ImportMediaInspection.InspectAsync(file, Probe(), Encoder(), TestContext.Current.CancellationToken);
        Assert.InRange(duration, 0.9, 1.2);
        Assert.Single(Directory.GetFiles(_root));
    }

    [Theory]
    [InlineData("externalReference")]
    [InlineData("wrongReferenceIndex")]
    [InlineData("protectedVideo")]
    [InlineData("fragmented")]
    [InlineData("oversizedBox")]
    [InlineData("unknownTopBox")]
    [InlineData("truncated")]
    [InlineData("multipleDescriptions")]
    [InlineData("protectedSampleChild")]
    public void MutatedMediaCannotPassContainerBoundary(string mode)
    {
        var file = Fixture();
        var bytes = File.ReadAllBytes(file);
        var reference = Find(bytes, "dref");
        var sample = Find(bytes, "avc1", reference);
        switch (mode)
        {
            case "externalReference":
                // url box flags: 1=self-contained. No external URL/URN is accepted even before probing.
                BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(reference + 20), 0);
                break;
            case "wrongReferenceIndex":
                BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(sample + 10), 2);
                break;
            case "protectedVideo":
                Encoding.ASCII.GetBytes("encv").CopyTo(bytes, sample);
                break;
            case "fragmented":
                Encoding.ASCII.GetBytes("moof").CopyTo(bytes, 4);
                break;
            case "oversizedBox":
                BinaryPrimitives.WriteUInt32BigEndian(bytes, uint.MaxValue);
                break;
            case "unknownTopBox":
                Encoding.ASCII.GetBytes("M3U8").CopyTo(bytes, 4);
                break;
            case "truncated":
                bytes = bytes[..^16];
                break;
            case "multipleDescriptions":
                BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(Find(bytes, "stsd") + 8), 2);
                break;
            case "protectedSampleChild":
                Encoding.ASCII.GetBytes("sinf").CopyTo(bytes, Find(bytes, "avcC", sample));
                break;
        }

        File.WriteAllBytes(file, bytes);
        Assert.Throws<InvalidDataException>(() => ImportMediaInspection.CheckContainer(file));
    }

    [Theory]
    [InlineData("#EXTM3U\nhttps://localhost/private")]
    [InlineData("<!DOCTYPE media SYSTEM 'file:///etc/passwd'><media/>")]
    [InlineData("Owned transport bytes are not a playable MP4")]
    public async Task NonMediaIsRejectedBeforeAnyExecutableStarts(string content)
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "untrusted.mp4");
        await File.WriteAllTextAsync(file, content, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ImportMediaInspection.InspectAsync(file, "/nonexistent/probe-must-not-start", "/nonexistent/encoder-must-not-start", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ProbeContract_HasNoNetworkProtocols_ExternalReferencesOrInheritedSecrets()
    {
        var info = ImportMediaInspection.Start(Fixture(), Probe());
        var arguments = info.ArgumentList.ToArray();
        Assert.Equal("file", arguments[Array.IndexOf(arguments, "-protocol_whitelist") + 1]);
        Assert.Equal("mov", arguments[Array.IndexOf(arguments, "-format_whitelist") + 1]);
        Assert.Equal("0", arguments[Array.IndexOf(arguments, "-enable_drefs") + 1]);
        Assert.Equal("0", arguments[Array.IndexOf(arguments, "-use_absolute_path") + 1]);
        Assert.DoesNotContain("http", string.Join(' ', arguments), StringComparison.OrdinalIgnoreCase);
        Assert.False(info.Environment.ContainsKey("HTTPS_PROXY"));
        Assert.False(info.Environment.ContainsKey("AWS_SECRET_ACCESS_KEY"));
        Assert.Contains("-max_alloc", arguments);
    }

    [Fact]
    public void LinkedInputIsRejected()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("POSIX link fixture; shared reparse policy retained on Windows.");
        }

        var target = Fixture();
        var linked = Path.Combine(_root, "linked.mp4");
        File.CreateSymbolicLink(linked, target);
        Assert.Throws<InvalidDataException>(() => ImportMediaInspection.CheckContainer(linked));
    }

    [Theory]
    [InlineData("not-json-with-secret")]
    [InlineData("{}")]
    [InlineData("{\"format\":{\"duration\":\"NaN\"},\"streams\":[]}")]
    [InlineData("{\"format\":{\"duration\":\"10801\"},\"streams\":[]}")]
    [InlineData("{\"format\":{\"duration\":\"1\"},\"streams\":[{}]}")]
    [InlineData("{\"format\":{\"duration\":\"1\"},\"streams\":[{\"codec_name\":\"aac\",\"codec_type\":\"audio\"}]}")]
    public void InvalidProbePropertiesFailClosedWithoutRawOutput(string output)
    {
        var error = Assert.Throws<InvalidDataException>(() => ImportMediaInspection.Properties(output));
        Assert.DoesNotContain("secret", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidHeaderButDestroyedVideoPackets_CannotPassFirstFrameDecode()
    {
        var file = Fixture();
        var bytes = await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken);
        var media = Find(bytes, "mdat") + 4;
        bytes.AsSpan(media).Clear();
        await File.WriteAllBytesAsync(file, bytes, TestContext.Current.CancellationToken);
        ImportMediaInspection.CheckContainer(file);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => ImportMediaInspection.InspectAsync(file, Probe(), Encoder(), TestContext.Current.CancellationToken));
        Assert.Contains("frame", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static int Find(byte[] bytes, string text, int start = 0)
    {
        var index = bytes.AsSpan(start).IndexOf(Encoding.ASCII.GetBytes(text));
        Assert.True(index >= 0, "Generated fixture must contain the targeted MP4 box.");
        return start + index;
    }

    private static string Encoder()
    {
        var encoder = Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFMPEG");
        Assert.True(!string.IsNullOrWhiteSpace(encoder) && Path.IsPathFullyQualified(encoder) && File.Exists(encoder), "Supply real native CASTLUMORA_TEST_FFMPEG; inspection must not silently skip.");
        return encoder!;
    }

    private static string Probe()
    {
        var probe = Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFPROBE");
        Assert.True(!string.IsNullOrWhiteSpace(probe) && Path.IsPathFullyQualified(probe) && File.Exists(probe), "Supply the real native CASTLUMORA_TEST_FFPROBE fixture; inspection tests must not silently skip.");
        return probe!;
    }

    private string Fixture()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "owned.mp4");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "import-owned.mp4"), file);
        return file;
    }
}
