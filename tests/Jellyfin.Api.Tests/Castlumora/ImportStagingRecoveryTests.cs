using System;
using System.IO;
using Jellyfin.Api.Castlumora;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class ImportStagingRecoveryTests
{
    [Fact]
    public void Recovery_RemovesOnlyMarkedAttempts_AndPreservesUnknownFilesAndLinks()
    {
        var data = Path.Combine(Path.GetTempPath(), "castlumora-staging-recovery-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(data, "castlumora", "imports", "staging");
        Directory.CreateDirectory(root);
        try
        {
            var owned = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(owned, "files"));
            ImportStagingRecovery.Mark(owned, Guid.NewGuid());
            File.WriteAllText(Path.Combine(owned, "files", "video.mp4.part"), "regenerable partial bytes");
            var unknown = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(unknown);
            File.WriteAllText(Path.Combine(unknown, "preserved"), "unknown unmarked data");
            var linked = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(linked);
            ImportStagingRecovery.Mark(linked, Guid.NewGuid());
            if (!OperatingSystem.IsWindows())
            {
                Directory.CreateSymbolicLink(Path.Combine(linked, "external"), unknown);
            }
            else
            {
                File.WriteAllText(Path.Combine(linked, ".castlumora-staging"), "invalid marker");
            }

            ImportStagingRecovery.Recover(data, NullLogger.Instance);
            Assert.False(Directory.Exists(owned));
            Assert.Equal("unknown unmarked data", File.ReadAllText(Path.Combine(unknown, "preserved")));
            Assert.True(Directory.Exists(linked));
        }
        finally
        {
            Directory.Delete(data, true);
        }
    }
}
