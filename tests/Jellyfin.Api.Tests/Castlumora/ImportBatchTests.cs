using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class ImportBatchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "castlumora-batch-" + Guid.NewGuid().ToString("N"));
    private readonly Guid _library = Guid.NewGuid();
    private readonly Guid _admin = Guid.NewGuid();
    private readonly CancellationToken _token = TestContext.Current.CancellationToken;

    [Fact]
    public async Task TwentyFiveEntries_SurviveReload_ExecuteThroughTheExistingWorker()
    {
        ImportQueue.Job[] saved;
        await using (var queue = Open())
        {
            saved = await SubmitAsync(queue, Entries(25), automatic: true);
            Assert.Equal(25, saved.Select(job => job.Id).Distinct().Count());
        }

        await using var reopened = Open();
        Assert.Equal(saved, await reopened.ListAsync(_token));
        Assert.All(saved, job =>
        {
            Assert.True(job.AutomaticRetries);
            Assert.Equal(_admin, job.RequestedBy);
            Assert.Equal("Owned fixture", job.Rights);
        });
        reopened.Start();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while ((await reopened.ListAsync(timeout.Token)).Any(job => job.State != ImportQueue.State.Completed))
        {
            await Task.Delay(10, timeout.Token);
        }

        Assert.Equal(25, Directory.GetFiles(_root, "*.fixture").Length);
    }

    [Theory]
    [InlineData("https://127.0.0.1/private.mp4")]
    [InlineData("http://example.com/insecure.mp4")]
    [InlineData("https://example.com/playlist")]
    public async Task InvalidLastEntry_LeavesMemoryAndJournalUnchanged(string source)
    {
        await using var queue = Open();
        var before = await File.ReadAllBytesAsync(Path.Combine(_root, "queue.json"), _token);
        var entries = Entries(2);
        entries[1] = (source, "Rejected");
        await Assert.ThrowsAsync<InvalidDataException>(() => SubmitAsync(queue, entries));
        Assert.Empty(await queue.ListAsync(_token));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(_root, "queue.json"), _token));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(26)]
    public async Task EmptyOrOversizedBatch_IsRejected(int count)
    {
        await using var queue = Open();
        await Assert.ThrowsAsync<InvalidDataException>(() => SubmitAsync(queue, Entries(count)));
        Assert.Empty(await queue.ListAsync(_token));
    }

    [Fact]
    public async Task ConcurrentBatches_CoalesceDuplicatesWithoutReplacingRightsOrRetryPolicy()
    {
        await using var queue = Open();
        var first = (await SubmitAsync(queue, Entries(1), automatic: true))[0];
        var duplicate = (first.Source, "Do not replace title");
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => queue.SubmitManyAsync([duplicate, duplicate], _library, "Do not replace rights", _admin, _token)));
        Assert.All(results.SelectMany(batch => batch), job => Assert.Equal(first, job));
        Assert.Single(await queue.ListAsync(_token));
    }

    [Fact]
    public async Task CrossingHistoryCapacity_DoesNotPartiallyAcceptBatch()
    {
        await using var queue = Open();
        for (var offset = 0; offset < 999; offset += 25)
        {
            await SubmitAsync(queue, Entries(Math.Min(25, 999 - offset), offset));
        }

        var before = await File.ReadAllBytesAsync(Path.Combine(_root, "queue.json"), _token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(queue, Entries(2, 999)));
        Assert.Equal(999, (await queue.ListAsync(_token)).Length);
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(_root, "queue.json"), _token));
    }

    [Fact]
    public async Task ActualSaveFailure_RollsBackAllNewInMemoryEntries()
    {
        await using var queue = Open();
        var original = (await SubmitAsync(queue, Entries(1)))[0];
        var journal = Path.Combine(_root, "queue.json");
        var backup = Path.Combine(_root, "retained.json");
        File.Move(journal, backup);
        Directory.CreateDirectory(journal);
        try
        {
            await Assert.ThrowsAnyAsync<IOException>(() => SubmitAsync(queue, Entries(2, 1)));
            Assert.Equal(original, Assert.Single(await queue.ListAsync(_token)));
        }
        finally
        {
            Directory.Delete(journal);
            File.Move(backup, journal);
        }

        Assert.Equal(2, (await SubmitAsync(queue, Entries(2, 1))).Length);
        Assert.Equal(3, (await queue.ListAsync(_token)).Length);
    }

    [Fact]
    public async Task CancelledSubmission_DoesNotPersistAnyEntry()
    {
        await using var queue = Open();
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.SubmitManyAsync(Entries(25), _library, "Owned", _admin, stop.Token));
        Assert.Empty(await queue.ListAsync(_token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private ImportQueue Open() => new(
        _root,
        async (job, progress, token) =>
    {
        var data = new byte[128];
        await File.WriteAllBytesAsync(Path.Combine(_root, job.Id.ToString("N") + ".fixture"), data, token);
        progress(data.Length);
        return new ImportQueue.Receipt(Convert.ToHexStringLower(SHA256.HashData(data)), data.Length);
    },
        NullLogger.Instance);

    private Task<ImportQueue.Job[]> SubmitAsync(ImportQueue queue, (string Source, string Title)[] entries, bool automatic = false)
        => queue.SubmitManyAsync(entries, _library, "Owned fixture", _admin, _token, automatic);

    private static (string Source, string Title)[] Entries(int count, int offset = 0)
        => Enumerable.Range(offset, count).Select(index => ($"https://example.com/{index}.mp4?private-query", "Owned " + index)).ToArray();
}
