using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Locking;
using Jellyfin.Database.Providers.Sqlite;
using Jellyfin.Server.Implementations.FullSystemBackup;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.SystemBackupService;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using BaseItemKind = Jellyfin.Data.Enums.BaseItemKind;

namespace Jellyfin.Server.Implementations.Tests.FullSystemBackup;

/// <summary>
/// Tests for <see cref="BackupService"/>, in particular that a single row of corrupt
/// <see cref="KeyframeData"/> (e.g. malformed <c>KeyframeTicks</c> JSON) does not abort
/// an otherwise healthy backup. See https://github.com/jellyfin/jellyfin/issues/17216.
/// </summary>
public sealed class BackupServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<JellyfinDbContext> _dbOptions;
    private readonly string _testRoot;
    private readonly string _backupPath;
    private readonly string _configurationDirectoryPath;

    public BackupServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<JellyfinDbContext>()
            .UseSqlite(_connection)
            .Options;

        using (var ctx = CreateDbContext())
        {
            ctx.Database.EnsureCreated();
        }

        // Each instance owns a directory and an isolated reserve policy. Production settings are untouched.
        _testRoot = Path.Combine(AppContext.BaseDirectory, "jellyfin-backup-service-tests-" + Guid.NewGuid().ToString("N"));
        _backupPath = Path.Combine(_testRoot, "Backup");
        _configurationDirectoryPath = Path.Combine(_testRoot, "Config");
        Directory.CreateDirectory(_backupPath);
        Directory.CreateDirectory(_configurationDirectoryPath);
    }

    public void Dispose()
    {
        _connection.Dispose();

        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, true);
        }
    }

    [Fact]
    public async Task CreateBackupAsync_WithCorruptKeyframeDataRow_SkipsRowAndCompletesBackup()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var validItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var corruptItemId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        await using (var ctx = CreateDbContext())
        {
            // A healthy item + keyframe row, written the normal way.
            ctx.BaseItems.Add(CreateMovieEntity(validItemId, "Good Movie"));
            ctx.BaseItems.Add(CreateMovieEntity(corruptItemId, "Corrupt Movie"));
            await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(true);

            ctx.KeyframeData.Add(new KeyframeData
            {
                ItemId = validItemId,
                TotalDuration = 60_000,
                KeyframeTicks = [0, 1000, 2000]
            });
            await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(true);

            // Simulate a corrupted database row: truncated JSON array for KeyframeTicks,
            // written directly via SQL to bypass EF's normal (well-formed) write path.
            await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO KeyframeData (ItemId, TotalDuration, KeyframeTicks) VALUES ({corruptItemId.ToString()}, {5000L}, {"[1,2,3"})",
                cancellationToken).ConfigureAwait(true);
        }

        var backupService = CreateBackupService();

        var manifest = await backupService.CreateBackupAsync(new BackupOptionsDto()).ConfigureAwait(true);

        Assert.True(File.Exists(manifest.Path));

        using var archive = await ZipFile.OpenReadAsync(manifest.Path, cancellationToken).ConfigureAwait(true);
        var keyframeEntry = archive.GetEntry("Database/KeyframeData.json");
        Assert.NotNull(keyframeEntry);

        await using var entryStream = await keyframeEntry!.OpenAsync(cancellationToken).ConfigureAwait(true);
        using var document = await JsonDocument.ParseAsync(entryStream, cancellationToken: cancellationToken).ConfigureAwait(true);

        var rows = document.RootElement.EnumerateArray().ToList();

        // The corrupt row must be skipped, but the valid row must still make it into the backup.
        var singleRow = Assert.Single(rows);
        Assert.Equal(validItemId, singleRow.GetProperty("ItemId").GetGuid());
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(5_368_709_114L)]
    public async Task CreateBackupAsync_BelowProductionReserve_RejectsBeforeCreatingArchive(long freeBytes)
    {
        string? checkedPath = null;
        var policy = new BackupStoragePolicy(path =>
        {
            checkedPath = path;
            return freeBytes;
        });
        var service = CreateBackupService(policy);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateBackupAsync(new BackupOptionsDto())).ConfigureAwait(true);

        Assert.Contains("Cannot create backup", exception.Message, StringComparison.Ordinal);
        Assert.Equal(_backupPath, checkedPath);
        Assert.Empty(Directory.GetFiles(_backupPath));
    }

    [Fact]
    public void ProductionPolicy_AtOriginalReserve_AllowsBackup()
    {
        Assert.Equal(5_368_709_115L, BackupStoragePolicy.ProductionMinimumFreeBytes);
        var policy = new BackupStoragePolicy(_ => 5_368_709_115L);
        policy.Validate(_backupPath);
    }

    [Fact]
    public async Task CreateBackupAsync_BelowIsolatedReserve_StillRejects()
    {
        var service = CreateBackupService(new BackupStoragePolicy(_ => 1023, 1024));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateBackupAsync(new BackupOptionsDto())).ConfigureAwait(true);
        Assert.Empty(Directory.GetFiles(_backupPath));
    }

    [Fact]
    public async Task CreateBackupAsync_AtIsolatedReserve_WritesRealDatabaseArchive()
    {
        var id = Guid.NewGuid();
        await using (var context = CreateDbContext())
        {
            context.BaseItems.Add(CreateMovieEntity(id, "Small fixture"));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        }

        // Capacity is controlled, but database export, serialization and ZIP creation are real.
        var service = CreateBackupService(new BackupStoragePolicy(_ => 1024 * 1024, 1024 * 1024));
        var manifest = await service.CreateBackupAsync(new BackupOptionsDto()).ConfigureAwait(true);
        using var archive = await ZipFile.OpenReadAsync(manifest.Path, TestContext.Current.CancellationToken).ConfigureAwait(true);
        Assert.NotNull(archive.GetEntry("manifest.json"));
        var entry = Assert.Single(archive.Entries, item => item.FullName == "Database/BaseItems.json");
        await using var stream = await entry.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        // EnsureCreated seeds a detached-user-data placeholder in addition to our movie.
        Assert.Equal(2, json.RootElement.GetArrayLength());
        var item = Assert.Single(json.RootElement.EnumerateArray(), row => row.GetProperty("Id").GetGuid().Equals(id));
        Assert.Equal(id, item.GetProperty("Id").GetGuid());
        Assert.Equal("Small fixture", item.GetProperty("Name").GetString());
    }

    [Fact]
    public void IsolatedReserve_DoesNotChangeProductionRequirement()
    {
        var isolated = new BackupStoragePolicy(_ => 1024 * 1024, 1024 * 1024);
        var production = new BackupStoragePolicy(_ => 1024 * 1024);
        isolated.Validate(_backupPath);
        Assert.Throws<InvalidOperationException>(() => production.Validate(_backupPath));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Policy_NonpositiveRequirement_IsRejected(long minimum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BackupStoragePolicy(minimumFreeBytes: minimum));
    }

    private BackupService CreateBackupService(BackupStoragePolicy? storagePolicy = null)
    {
        var factory = new Mock<IDbContextFactory<JellyfinDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(CreateDbContext);
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateDbContext);

        var applicationHost = new Mock<IServerApplicationHost>();
        applicationHost.Setup(a => a.ApplicationVersion).Returns(new Version(10, 11, 0));

        var applicationPaths = new Mock<IServerApplicationPaths>();
        applicationPaths.Setup(a => a.BackupPath).Returns(_backupPath);
        applicationPaths.Setup(a => a.ConfigurationDirectoryPath).Returns(_configurationDirectoryPath);
        applicationPaths.Setup(a => a.DataPath).Returns(Path.Combine(_testRoot, "Data"));
        applicationPaths.Setup(a => a.RootFolderPath).Returns(Path.Combine(_testRoot, "Root"));
        applicationPaths.Setup(a => a.InternalMetadataPath).Returns(Path.Combine(_testRoot, "Metadata"));
        applicationPaths.Setup(a => a.DefaultInternalMetadataPath).Returns(Path.Combine(_testRoot, "MetadataDefault"));

        var jellyfinDatabaseProvider = new Mock<IJellyfinDatabaseProvider>();
        jellyfinDatabaseProvider.Setup(p => p.RunScheduledOptimisation(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        jellyfinDatabaseProvider.Setup(p => p.PurgeDatabase(It.IsAny<JellyfinDbContext>(), It.IsAny<System.Collections.Generic.IEnumerable<string>>())).Returns(Task.CompletedTask);

        var applicationLifetime = new Mock<IHostApplicationLifetime>();

        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(l => l.IsScanRunning).Returns(false);

        return new BackupService(
            NullLogger<BackupService>.Instance,
            factory.Object,
            applicationHost.Object,
            applicationPaths.Object,
            jellyfinDatabaseProvider.Object,
            applicationLifetime.Object,
            libraryManager.Object,
            storagePolicy ?? new BackupStoragePolicy(minimumFreeBytes: 1024 * 1024));
    }

    private static BaseItemEntity CreateMovieEntity(Guid id, string name)
    {
        return new BaseItemEntity
        {
            Id = id,
            Type = "Movie",
            Name = name,
            PresentationUniqueKey = id.ToString("N"),
            MediaType = "Video",
            IsMovie = true,
            IsFolder = false,
            IsVirtualItem = false
        };
    }

    private JellyfinDbContext CreateDbContext()
    {
        return new JellyfinDbContext(
            _dbOptions,
            NullLogger<JellyfinDbContext>.Instance,
            new SqliteDatabaseProvider(null!, NullLogger<SqliteDatabaseProvider>.Instance),
            new NoLockBehavior(NullLogger<NoLockBehavior>.Instance));
    }
}
