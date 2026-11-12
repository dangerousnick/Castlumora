using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Jellyfin.Api.Castlumora;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

[Collection(nameof(ImportExecutableCollection))]
public sealed class ImportPublicationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "castlumora-publication-" + Guid.NewGuid().ToString("N"));
    private readonly Guid _library = Guid.NewGuid();
    private readonly Guid _admin = Guid.NewGuid();
    private readonly CancellationToken _token = TestContext.Current.CancellationToken;
    private readonly List<string> _scans = [];
    private int _downloads;

    public ImportPublicationTests()
    {
        if (OperatingSystem.IsMacOS())
        {
            // Compare physical paths, not Apple's verified /var -> /private/var
            // system alias. Ordinary symlinks remain rejected by the publication tests.
            _root = ImportQueue.MacDataDirectory(_root, path => new DirectoryInfo(path).ResolveLinkTarget(returnFinalTarget: true)?.FullName);
        }

        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Library);
    }

    private string Data => Path.Combine(_root, "server");

    private string Library => Path.Combine(_root, "library");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewUnindexedFolder_QueuesScanWithoutCancellingExistingScan(bool indexed)
    {
        var libraries = new Mock<ILibraryManager>();
        var monitor = new Mock<ILibraryMonitor>();
        var folder = Path.Combine(Library, "Castlumora Imports");
        var media = Path.Combine(folder, "owned.mp4");
        libraries.Setup(item => item.FindByPath(folder, true)).Returns(indexed ? new Folder() : null);
        ImportPublication.RequestScan(media, libraries.Object, monitor.Object);
        monitor.Verify(item => item.ReportFileSystemChanged(media), Times.Once);
        monitor.Verify(item => item.ReportFileSystemChanged(folder), Times.Once);
        libraries.Verify(item => item.QueueLibraryScan(), indexed ? Times.Never() : Times.Once());
        libraries.Verify(item => item.FindByPath(folder, true), Times.Once);
        libraries.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MediaPathsInitializedAfterConstruction_AreResolvedAtExecutionTime()
    {
        var ready = false;
        var reads = 0;
        var publisher = Publisher(mediaTools: () =>
        {
            reads++;
            Assert.True(ready);
            return (Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFPROBE")!, Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFMPEG")!);
        });
        Assert.Equal(0, reads);
        ready = true;
        var receipt = await publisher.ExecuteAsync(Job(), _ => { }, _token);
        Assert.Equal(1, reads);
        Assert.Equal(Clip().Length, receipt.Bytes);
        Assert.Single(_scans);
    }

    [Fact]
    public async Task ActualMedia_PublishesAtomicallyWithSafeMetadata_PrivateProvenanceAndScan()
    {
        var job = Job();
        var publisher = Publisher();
        var receipt = await publisher.ExecuteAsync(job, _ => { }, _token);
        var file = Assert.Single(Directory.GetFiles(Library, "*.mp4", SearchOption.AllDirectories));
        Assert.Equal(Clip(), await File.ReadAllBytesAsync(file, _token));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Clip())), receipt.Sha256);
        Assert.Equal(file, Assert.Single(_scans));
        var nfo = await File.ReadAllTextAsync(Path.ChangeExtension(file, ".nfo"), _token);
        Assert.Equal(job.Title, XDocument.Parse(nfo).Root!.Element("title")!.Value);
        Assert.DoesNotContain("signed-secret", nfo, StringComparison.Ordinal);
        Assert.DoesNotContain("Rights record", nfo, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(Library, "*.part", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetDirectories(Path.Combine(Data, "staging")));
        var record = Assert.Single(Directory.GetFiles(Path.Combine(Data, "castlumora", "imports", "publications")));
        var provenance = await File.ReadAllTextAsync(record, _token);
        Assert.Contains("signed-secret", provenance, StringComparison.Ordinal);
        Assert.Contains("Rights record", provenance, StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(record));
        }
    }

    [Fact]
    public async Task FreshPublisherReconcilesCommittedReceiptWithoutDownloadingOrOverwritingManualMetadata()
    {
        var job = Job();
        var first = await Publisher().ExecuteAsync(job, _ => { }, _token);
        var file = Assert.Single(Directory.GetFiles(Library, "*.mp4", SearchOption.AllDirectories));
        await File.WriteAllTextAsync(Path.ChangeExtension(file, ".nfo"), "<movie><title>Manually corrected</title></movie>", _token);
        var second = await Publisher().ExecuteAsync(job, _ => { }, _token);
        Assert.Equal(first, second);
        Assert.Equal(1, _downloads);
        Assert.Contains("Manually corrected", await File.ReadAllTextAsync(Path.ChangeExtension(file, ".nfo"), _token), StringComparison.Ordinal);
        Assert.Equal(2, _scans.Count);
    }

    [Fact]
    public async Task CrashAfterCommitBeforeScan_ReconcilesSameFileAndDoesNotRedownload()
    {
        var job = Job();
        await Assert.ThrowsAsync<IOException>(() => Publisher(scan: _ => throw new IOException("simulated scan-request interruption")).ExecuteAsync(job, _ => { }, _token));
        Assert.Single(Directory.GetFiles(Library, "*.mp4", SearchOption.AllDirectories));
        var recovered = await Publisher().ExecuteAsync(job, _ => { }, _token);
        Assert.Equal(1, _downloads);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Clip())), recovered.Sha256);
        Assert.Single(_scans);
    }

    [Fact]
    public async Task DifferentSourcesWithIdenticalContent_DeduplicateAndPreserveFirstTitle()
    {
        var first = Job();
        var receipt = await Publisher().ExecuteAsync(first, _ => { }, _token);
        var other = Job("Other title", "https://example.com/other.mp4");
        var duplicate = await Publisher().ExecuteAsync(other, _ => { }, _token);
        Assert.Equal(receipt, duplicate);
        var media = Assert.Single(Directory.GetFiles(Library, "*.mp4", SearchOption.AllDirectories));
        Assert.Equal(first.Title, XDocument.Parse(await File.ReadAllTextAsync(Path.ChangeExtension(media, ".nfo"), _token)).Root!.Element("title")!.Value);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(Data, "castlumora", "imports", "publications")).Length);
    }

    [Fact]
    public async Task CancelBeforeCommit_CleansStagingAndNeverScansOrPublishes()
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(_token);
        var calls = 0;
        var publisher = Publisher(destination: _ =>
        {
            if (++calls == 3)
            {
                stop.Cancel();
            }

            return Library;
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publisher.ExecuteAsync(Job(), _ => { }, stop.Token));
        Assert.Empty(Directory.GetFiles(Library, "*.mp4", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(Library, "*.nfo", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(Library, "*.part", SearchOption.AllDirectories));
        Assert.Empty(_scans);
    }

    [Fact]
    public async Task CancellationAfterCommit_PreservesMediaAndReturnsSuccessfulReceipt()
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(_token);
        var receipt = await Publisher(scan: _ => stop.Cancel()).ExecuteAsync(Job(), _ => { }, stop.Token);
        Assert.True(stop.IsCancellationRequested);
        Assert.Equal(Clip().Length, receipt.Bytes);
        Assert.Single(Directory.GetFiles(Library, "*.mp4", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RevokedAuthorizationOrChangedDestination_CannotPublish()
    {
        var calls = 0;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Publisher(destination: _ => ++calls == 1 ? Library : throw new UnauthorizedAccessException()).ExecuteAsync(Job(), _ => { }, _token));
        Assert.Empty(Directory.GetFiles(Library, "*.mp4", SearchOption.AllDirectories));
        var changed = Path.Combine(_root, "other");
        Directory.CreateDirectory(changed);
        calls = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => Publisher(destination: _ => ++calls == 1 ? Library : changed).ExecuteAsync(Job(), _ => { }, _token));
        Assert.Empty(Directory.GetFiles(changed, "*.mp4", SearchOption.AllDirectories));
        Assert.Empty(_scans);
    }

    [Fact]
    public async Task ServerStateDestinationRejectedBeforeDownload()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Publisher(destination: _ => Data).ExecuteAsync(Job(), _ => { }, _token));
        Assert.Equal(0, _downloads);
        Assert.Empty(_scans);
    }

    [Fact]
    public async Task MediaCollisionAndUnownedDirectory_ArePreserved()
    {
        var folder = Path.Combine(Library, "Castlumora Imports");
        Directory.CreateDirectory(folder);
        var existing = Path.Combine(folder, "preserve.mp4");
        await File.WriteAllTextAsync(existing, "preserve", _token);
        await Assert.ThrowsAsync<InvalidDataException>(() => Publisher().ExecuteAsync(Job(), _ => { }, _token));
        Assert.Equal("preserve", await File.ReadAllTextAsync(existing, _token));
        Assert.Equal(0, _downloads);
        File.Delete(existing);
        var job = Job();
        await Publisher().ExecuteAsync(job, _ => { }, _token);
        var file = Assert.Single(Directory.GetFiles(Library, "*.mp4", SearchOption.AllDirectories));
        await File.WriteAllTextAsync(file, "changed by operator", _token);
        await Assert.ThrowsAsync<InvalidDataException>(() => Publisher().ExecuteAsync(job, _ => { }, _token));
        Assert.Equal("changed by operator", await File.ReadAllTextAsync(file, _token));
    }

    [Fact]
    public async Task LinkedDestinationCannotRedirectWrites()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("POSIX link fixture; shared reparse policy retained.");
        }

        var linked = Path.Combine(_root, "linked");
        Directory.CreateSymbolicLink(linked, Library);
        await Assert.ThrowsAsync<InvalidDataException>(() => Publisher(destination: _ => linked).ExecuteAsync(Job(), _ => { }, _token));
        Assert.Empty(Directory.GetFileSystemEntries(Library));
        Assert.Equal(0, _downloads);
    }

    [Fact]
    public async Task SharedWritableDestinationIsRejectedWithoutChangingPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("POSIX mode fixture; Windows ACL certification remains deferred.");
            return;
        }

        var mode = File.GetUnixFileMode(Library) | UnixFileMode.GroupWrite;
        File.SetUnixFileMode(Library, mode);
        await Assert.ThrowsAsync<InvalidDataException>(() => Publisher().ExecuteAsync(Job(), _ => { }, _token));
        Assert.Equal(mode, File.GetUnixFileMode(Library));
        Assert.Equal(0, _downloads);
    }

    [Fact]
    public void ProductionResolver_RequiresEnabledAdministratorAndRealGenericLibrary()
    {
        var users = new Mock<IUserManager>();
        var libraries = new Mock<ILibraryManager>();
        var job = Job();
        Assert.Throws<UnauthorizedAccessException>(() => ImportPublication.Resolve(job, libraries.Object, users.Object));
        var user = new User("Owned", "test", "test") { Id = _admin };
        users.Setup(manager => manager.GetUserById(_admin)).Returns(user);
        Assert.Throws<UnauthorizedAccessException>(() => ImportPublication.Resolve(job, libraries.Object, users.Object));
        user.SetPermission(PermissionKind.IsAdministrator, true);
        libraries.Setup(manager => manager.GetVirtualFolders()).Returns([new VirtualFolderInfo { ItemId = _library.ToString("N"), CollectionType = CollectionTypeOptions.movies, Locations = [Library] }]);
        Assert.Throws<InvalidDataException>(() => ImportPublication.Resolve(job, libraries.Object, users.Object));
        libraries.Setup(manager => manager.GetVirtualFolders()).Returns([new VirtualFolderInfo { ItemId = _library.ToString("N"), CollectionType = CollectionTypeOptions.homevideos, Locations = [Library] }]);
        Assert.Equal(Library, ImportPublication.Resolve(job, libraries.Object, users.Object));
        user.SetPermission(PermissionKind.IsDisabled, true);
        Assert.Throws<UnauthorizedAccessException>(() => ImportPublication.Resolve(job, libraries.Object, users.Object));
    }

    [Fact]
    public async Task InterruptedBeforeCommit_ReconcilesOwnedPartialNamesAndRetainsProvenance()
    {
        var job = Job();
        var calls = 0;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Publisher(destination: _ => ++calls == 3 ? throw new UnauthorizedAccessException() : Library).ExecuteAsync(job, _ => { }, _token));
        var folder = Path.Combine(Library, "Castlumora Imports");
        var partial = Path.Combine(folder, ".castlumora-" + job.Id.ToString("N") + ".part");
        await File.WriteAllTextAsync(partial, "interrupted copy", _token);
        await File.WriteAllTextAsync(partial + ".metadata", "interrupted metadata", _token);
        var receipt = await Publisher().ExecuteAsync(job, _ => { }, _token);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Clip())), receipt.Sha256);
        Assert.False(File.Exists(partial));
        Assert.False(File.Exists(partial + ".metadata"));
        Assert.Single(_scans);
    }

    [Fact]
    public async Task ExistingUnownedNfoAndLinkedNfo_AreNeverOverwritten()
    {
        var folder = Path.Combine(Library, "Castlumora Imports");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, ".castlumora-library"), _library.ToString("N"), _token);
        var nfo = Path.Combine(folder, Convert.ToHexStringLower(SHA256.HashData(Clip())) + ".nfo");
        await File.WriteAllTextAsync(nfo, "manual metadata", _token);
        await Assert.ThrowsAsync<InvalidDataException>(() => Publisher().ExecuteAsync(Job(), _ => { }, _token));
        Assert.Equal("manual metadata", await File.ReadAllTextAsync(nfo, _token));
        Assert.Empty(_scans);
        if (!OperatingSystem.IsWindows())
        {
            File.Delete(nfo);
            var target = Path.Combine(_root, "preserve.nfo");
            await File.WriteAllTextAsync(target, "preserve outside", _token);
            File.CreateSymbolicLink(nfo, target);
            await Assert.ThrowsAsync<InvalidDataException>(() => Publisher().ExecuteAsync(Job(), _ => { }, _token));
            Assert.Equal("preserve outside", await File.ReadAllTextAsync(target, _token));
        }
    }

    [Fact]
    public async Task WritableNonStickyAncestorCannotRedirectPublication()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("POSIX ancestor permission fixture.");
            return;
        }

        var shared = Path.Combine(_root, "shared");
        var target = Path.Combine(shared, "library");
        Directory.CreateDirectory(target);
        File.SetUnixFileMode(shared, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        await Assert.ThrowsAsync<InvalidDataException>(() => Publisher(destination: _ => target).ExecuteAsync(Job(), _ => { }, _token));
        Assert.Empty(Directory.GetFileSystemEntries(target));
        Assert.Equal(0, _downloads);
    }

    [Fact]
    public async Task InvalidXmlCharactersCannotCrashWorker_AndUnicodeTitleIsPreserved()
    {
        var job = Job("Owned 🎬 & <clip>\uffff");
        await Publisher().ExecuteAsync(job, _ => { }, _token);
        var file = Assert.Single(Directory.GetFiles(Library, "*.nfo", SearchOption.AllDirectories));
        Assert.Equal("Owned 🎬 & <clip>", XDocument.Parse(await File.ReadAllTextAsync(file, _token)).Root!.Element("title")!.Value);
        var provenance = Assert.Single(Directory.GetFiles(Path.Combine(Data, "castlumora", "imports", "publications")));
        using var record = JsonDocument.Parse(await File.ReadAllTextAsync(provenance, _token));
        Assert.Equal(job.Title, record.RootElement.GetProperty("Job").GetProperty("Title").GetString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static byte[] Clip() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "import-owned.mp4"));

    private ImportQueue.Job Job(string title = "Owned <script> & emoji 🎬", string source = "https://example.com/owned.mp4?signed-secret") => new(Guid.NewGuid(), source, Guid.NewGuid().ToString("N"), _library, title, "Rights record retained privately", _admin, DateTimeOffset.UtcNow);

    private ImportPublication Publisher(Func<ImportQueue.Job, string>? destination = null, Action<string>? scan = null, Func<(string Probe, string Encoder)>? mediaTools = null) => new(
        Data,
        [Data],
        destination ?? (_ => Library),
        StageAsync,
        scan ?? (file => _scans.Add(file)),
        Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFPROBE") ?? throw new InvalidOperationException("Real FFprobe fixture required"),
        Environment.GetEnvironmentVariable("CASTLUMORA_TEST_FFMPEG") ?? throw new InvalidOperationException("Real FFmpeg fixture required"),
        NullLogger.Instance,
        mediaTools);

    private async Task<YtDlpImportRunner.StagedDownload> StageAsync(ImportQueue.Job job, Action<long> progress, CancellationToken token)
    {
        _downloads++;
        var attempt = Path.Combine(Data, "staging", Guid.NewGuid().ToString("N"));
        var files = Path.Combine(attempt, "files");
        Directory.CreateDirectory(files);
        var staged = new YtDlpImportRunner.StagedDownload(attempt, files, NullLogger.Instance);
        var bytes = Clip();
        await File.WriteAllBytesAsync(staged.MediaPath, bytes, token);
        await File.WriteAllTextAsync(staged.MetadataPath, "{\"untrusted\":\"never-publish-raw\"}", token);
        staged.Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        staged.Bytes = bytes.Length;
        progress(bytes.Length);
        return staged;
    }
}
