using System;
using System.IO;
using Jellyfin.LiveTv.Recordings;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Moq;
using Xunit;

namespace Jellyfin.LiveTv.Tests.Recordings;

public sealed class RecordingRefreshTargetTests
{
    [Fact]
    public void FirstRecordingInUnindexedRoot_RefreshesAggregateRoot()
    {
        var library = new Mock<ILibraryManager>();
        var root = new AggregateFolder { Id = Guid.NewGuid(), Name = "Synthetic library root" };
        library.SetupGet(value => value.RootFolder).Returns(root);
        var recordingFolder = Path.Combine(Path.GetTempPath(), "fresh-recordings", "First recording");

        Assert.Same(root, RecordingsManager.GetAffectedBaseItem(library.Object, recordingFolder));
    }

    [Fact]
    public void ExistingIndexedRecordingParent_IsPreferredToAggregateRefresh()
    {
        var library = new Mock<ILibraryManager>();
        var path = Path.Combine(Path.GetTempPath(), "indexed-recordings", "Existing recording");
        var folder = new Folder { Id = Guid.NewGuid(), Path = path };
        library.Setup(value => value.FindByPath(path, null)).Returns(folder);

        Assert.Same(folder, RecordingsManager.GetAffectedBaseItem(library.Object, path));
        library.VerifyGet(value => value.RootFolder, Times.Never);
    }

    [Fact]
    public void MissingRecordingDirectory_DoesNotQueueGlobalRefresh()
    {
        var library = new Mock<ILibraryManager>(MockBehavior.Strict);
        Assert.Null(RecordingsManager.GetAffectedBaseItem(library.Object, null));
        library.VerifyNoOtherCalls();
    }
}
