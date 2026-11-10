using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using MediaBrowser.Common.Configuration;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class YtDlpRuntimeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "castlumora-tool-test-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] _bytes = Encoding.UTF8.GetBytes("owned synthetic tool asset");
    private readonly YtDlpPlatform _asset;

    public YtDlpRuntimeTests()
    {
        Directory.CreateDirectory(_root);
        _asset = new YtDlpPlatform("linux-x64", "2026.10.26", "yt-dlp_linux", Convert.ToHexStringLower(SHA256.HashData(_bytes)), _bytes.Length);
    }

    [Fact]
    public async Task InstallAndReopen_VerifiesPinnedBytesAndPersistsWithoutNetwork()
    {
        var calls = 0;
        await Bundle();
        async Task Probe(string path, string directory, string version, CancellationToken token)
        {
            Assert.Equal(_bytes, await File.ReadAllBytesAsync(path, token));
            Assert.Equal(_asset.Version, version);
            calls++;
        }

        using var runtime = Runtime(Probe);
        Assert.Equal("notInstalled", (await runtime.StatusAsync(TestContext.Current.CancellationToken))["state"]!.GetValue<string>());
        Assert.True((await runtime.InstallAsync(TestContext.Current.CancellationToken))["ready"]!.GetValue<bool>());
        using var reopened = Runtime(Probe);
        var state = await reopened.StatusAsync(TestContext.Current.CancellationToken);
        Assert.Equal("ready", state["state"]!.GetValue<string>());
        Assert.False(state["downloadJobsAvailable"]!.GetValue<bool>());
        Assert.Equal(_asset.Sha256, state["expectedSha256"]!.GetValue<string>());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task MissingOrCorruptBundle_IsNeverExecuted()
    {
        var calls = 0;
        using var runtime = Runtime((_, _, _, _) =>
        {
            calls++;
            return Task.CompletedTask;
        });
        await Assert.ThrowsAsync<InvalidDataException>(() => runtime.InstallAsync(TestContext.Current.CancellationToken));
        var path = await Bundle();
        await File.WriteAllBytesAsync(path, new byte[_bytes.Length], TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => runtime.InstallAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, calls);
        Assert.False(File.Exists(Managed()));
    }

    [Fact]
    public async Task InstalledTampering_IsDetectedBeforeExecutionAndCanBeRepaired()
    {
        await Bundle();
        var calls = 0;
        using var runtime = Runtime((_, _, _, _) =>
        {
            calls++;
            return Task.CompletedTask;
        });
        await runtime.InstallAsync(TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Managed(), new byte[_bytes.Length], TestContext.Current.CancellationToken);
        Assert.Equal("verificationFailed", (await runtime.StatusAsync(TestContext.Current.CancellationToken))["state"]!.GetValue<string>());
        Assert.Equal(1, calls);
        await runtime.InstallAsync(TestContext.Current.CancellationToken);
        Assert.Equal(_bytes, await File.ReadAllBytesAsync(Managed(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FailedOrCanceledCandidate_DoesNotReplaceInstalledFileOrLeaveTemporaryExecutable()
    {
        await Bundle();
        using (var good = Runtime((_, _, _, _) => Task.CompletedTask))
        {
            await good.InstallAsync(TestContext.Current.CancellationToken);
        }

        using var bad = Runtime((_, _, _, _) => throw new InvalidDataException("Unexpected version"));
        await Assert.ThrowsAsync<InvalidDataException>(() => bad.InstallAsync(TestContext.Current.CancellationToken));
        Assert.Equal(_bytes, await File.ReadAllBytesAsync(Managed(), TestContext.Current.CancellationToken));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var canceled = Runtime(async (_, _, _, token) =>
        {
            await cancellation.CancelAsync();
            token.ThrowIfCancellationRequested();
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled.InstallAsync(cancellation.Token));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(Managed())!, "*.tmp"));
        Assert.Equal(_bytes, await File.ReadAllBytesAsync(Managed(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReparsePaths_AreRejectedWithoutWritingOutsideManagedDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Symbolic-link creation requires Windows developer mode/privileges; other Windows lifecycle tests still run.");
            return;
        }

        await Bundle();
        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        var data = Path.Combine(_root, "data");
        Directory.CreateDirectory(data);
        Directory.CreateSymbolicLink(Path.Combine(data, "castlumora"), outside);
        using var runtime = Runtime((_, _, _, _) => Task.CompletedTask);
        await Assert.ThrowsAsync<InvalidDataException>(() => runtime.InstallAsync(TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFileSystemEntries(outside));
    }

    [Fact]
    public async Task PlatformSelection_IsExplicitAndUnsupportedPlatformsDoNotInstall()
    {
        Assert.Equal("yt-dlp.exe", YtDlpPlatform.Detect("win-x64")!.FileName);
        Assert.Equal("yt-dlp_linux", YtDlpPlatform.Detect("linux-x64")!.FileName);
        Assert.Null(YtDlpPlatform.Detect("linux-arm64"));
        using var runtime = new YtDlpRuntime(Path.Combine(_root, "data"), _root, null, (_, _, _, _) => throw new InvalidOperationException("Must not launch"));
        Assert.Equal("unsupportedPlatform", (await runtime.StatusAsync(TestContext.Current.CancellationToken))["state"]!.GetValue<string>());
        await Assert.ThrowsAsync<NotSupportedException>(() => runtime.InstallAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RealProbe_UsesFixedArgumentsAndCleanEnvironment_AndRejectsWrongVersion()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Owned POSIX process fixture; pinned Windows executable is checked by the platform CI job.");
            return;
        }

        var executable = Path.Combine(_root, "probe");
        await File.WriteAllTextAsync(executable, "#!/bin/sh\n[ \"$1\" = --ignore-config ] && [ \"$2\" = --no-plugin-dirs ] && [ \"$3\" = --version ] || exit 2\n[ -z \"$PYTHONPATH\" ] || exit 3\nprintf '2026.10.26\\n'\n", TestContext.Current.CancellationToken);
        YtDlpPlatform.Executable(executable);
        await YtDlpRuntime.ProbeAsync(executable, _root, "2026.10.26", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => YtDlpRuntime.ProbeAsync(executable, _root, "wrong", TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetDirectories(_root, "health-*"));
    }

    [Fact]
    public async Task RealProbe_CancellationStopsOwnedProcessAndCleansTemporaryDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Owned POSIX cancellation fixture; Windows runtime health has a separate pinned-binary CI job.");
            return;
        }

        var executable = Path.Combine(_root, "sleeping-probe");
        await File.WriteAllTextAsync(executable, "#!/bin/sh\nsleep 60\n", TestContext.Current.CancellationToken);
        YtDlpPlatform.Executable(executable);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(250));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => YtDlpRuntime.ProbeAsync(executable, _root, "2026.10.26", cancellation.Token));
        Assert.Empty(Directory.GetDirectories(_root, "health-*"));
    }

    [Fact]
    public async Task RealProbe_RejectsExcessiveOutputAndCleansOwnedProcess()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Owned POSIX output fixture; real Windows asset is checked separately.");
            return;
        }

        var executable = Path.Combine(_root, "verbose-probe");
        await File.WriteAllTextAsync(executable, "#!/bin/sh\nhead -c 6000 /dev/zero | tr '\\000' x\n", TestContext.Current.CancellationToken);
        YtDlpPlatform.Executable(executable);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<InvalidDataException>(() => YtDlpRuntime.ProbeAsync(executable, _root, "2026.10.26", cancellation.Token));
        Assert.Empty(Directory.GetDirectories(_root, "health-*"));
    }

    [Fact]
    public async Task ActualPinnedBundle_InstallsAndChecksAcrossRestart_WhenProvided()
    {
        var system = Environment.GetEnvironmentVariable("CASTLUMORA_TEST_YTDLP_BUNDLE");
        if (string.IsNullOrWhiteSpace(system))
        {
            Assert.Skip("Set the verified platform bundle directory; executed by managed-runtime CI and local real-asset verification.");
            return;
        }

        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(p => p.DataPath).Returns(Path.Combine(_root, "real-data"));
        paths.SetupGet(p => p.ProgramSystemPath).Returns(system);
        using (var runtime = new YtDlpRuntime(paths.Object))
        {
            Assert.True((await runtime.InstallAsync(TestContext.Current.CancellationToken))["ready"]!.GetValue<bool>());
        }

        using var reopened = new YtDlpRuntime(paths.Object);
        Assert.True((await reopened.StatusAsync(TestContext.Current.CancellationToken))["ready"]!.GetValue<bool>());
    }

    public void Dispose() => Directory.Delete(_root, true);

    private string Managed() => Path.Combine(_root, "data", "castlumora", "tools", "yt-dlp", _asset.Version, _asset.Id, _asset.FileName);

    private YtDlpRuntime Runtime(Func<string, string, string, CancellationToken, Task> probe)
        => new(Path.Combine(_root, "data"), Path.Combine(_root, "system"), _asset, probe);

    private async Task<string> Bundle()
    {
        var path = Path.Combine(_root, "system", "tools", "yt-dlp", _asset.Version, _asset.Id, _asset.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, _bytes, TestContext.Current.CancellationToken);
        return path;
    }
}
