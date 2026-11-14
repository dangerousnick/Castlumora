using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.LiveTv.IO;
using MediaBrowser.Common;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.LiveTv.Tests.IO;

public sealed class EncodedRecorderTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RequestedStopOrDurationLimit_IsCancellationAfterOwnedProcessExits(bool manual)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Uses an owned POSIX process fixture.");
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "castlumora-recorder-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "encoder");
            await File.WriteAllTextAsync(path, "#!/bin/sh\nread command\nexit 23\n", TestContext.Current.CancellationToken);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            using var recorder = Create(root, path);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var task = recorder.Record(null!, new MediaSourceInfo { Path = "owned-fixture" }, Path.Combine(root, "recording.ts"), manual ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(2), () => started.SetResult(), cancellation.Token);
            if (manual)
            {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                await cancellation.CancelAsync();
            }

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task UnrequestedNonzeroExit_RemainsARealRecordingFailure()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Uses an owned POSIX process fixture.");
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "castlumora-recorder-failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "encoder");
            await File.WriteAllTextAsync(path, "#!/bin/sh\nexit 23\n", TestContext.Current.CancellationToken);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            using var recorder = Create(root, path);
            await Assert.ThrowsAsync<FfmpegException>(() => recorder.Record(null!, new MediaSourceInfo { Path = "owned-fixture" }, Path.Combine(root, "recording.ts"), TimeSpan.FromSeconds(30), () => { }, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static EncodedRecorder Create(string root, string encoderPath)
    {
        var encoder = new Mock<IMediaEncoder>();
        encoder.SetupGet(e => e.EncoderPath).Returns(encoderPath);
        var paths = new Mock<IServerApplicationPaths>();
        paths.SetupGet(p => p.LogDirectoryPath).Returns(root);
        var config = new Mock<IServerConfigurationManager>();
        config.Setup(c => c.GetConfiguration("encoding")).Returns(new EncodingOptions());
        return new EncodedRecorder(NullLogger.Instance, encoder.Object, paths.Object, config.Object);
    }
}
