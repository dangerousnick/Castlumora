using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class YouTubeNodeRuntimeTests
{
    [Fact]
    public async Task MissingRuntime_DoesNotFallBackToAmbientNode()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "castlumora-node-absent-" + Guid.NewGuid().ToString("N"));
        await Assert.ThrowsAsync<InvalidDataException>(() => YouTubeNodeRuntime.VerifyAsync(root, TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task ActualReviewedBundle_HasExactVersion()
    {
        var root = Environment.GetEnvironmentVariable("CASTLUMORA_TEST_NODE_BUNDLE");
        if (string.IsNullOrWhiteSpace(root))
        {
            Assert.Skip("Provide the reviewed Linux Node bundle to claim native runtime verification.");
            return;
        }

        var executable = await YouTubeNodeRuntime.VerifyAsync(root, TestContext.Current.CancellationToken);
        Assert.Equal("node", Path.GetFileName(executable));
        Assert.Equal(YouTubeNodeRuntime.Size, new FileInfo(executable).Length);
        var start = new ProcessStartInfo(executable);
        start.ArgumentList.Add("--permission");
        start.ArgumentList.Add("-");
        start.Environment.Clear();
        start.Environment["NODE_OPTIONS"] = "--max-old-space-size=256";
        var script = "try { require('node:fs').writeFileSync('must-not-be-created', 'blocked'); process.exit(1); } catch (error) { console.log(error.code); }";
        var result = await ImportProcess.RunAsync(start, script, TimeSpan.FromSeconds(10), 256, ct => Task.Delay(Timeout.InfiniteTimeSpan, ct), TestContext.Current.CancellationToken);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("ERR_ACCESS_DENIED", result.Output.Trim());
    }
}
