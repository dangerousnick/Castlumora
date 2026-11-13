using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class ImportQueueTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "castlumora-queue-" + Guid.NewGuid().ToString("N"));
    private readonly Guid _library = Guid.NewGuid();
    private readonly Guid _admin = Guid.NewGuid();
    private readonly CancellationToken _token = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("certificateRejected")]
    [InlineData("sourceDenied")]
    [InlineData("sourceNetwork")]
    [InlineData("sourceRateLimited")]
    [InlineData("sizeLimit")]
    public async Task KnownFailureCode_IsPersistedAcrossReloadWithoutRawDiagnostics(string code)
    {
        Guid id;
        await using (var queue = Open((_, _, _) => throw ImportFailure.Download(code)))
        {
            id = (await SubmitAsync(queue)).Id;
            queue.Start();
            Assert.Equal(code, (await WaitAsync(queue, id, ImportQueue.State.Failed)).Error);
        }

        await using var reopened = Open(CompleteAsync);
        var restored = Assert.Single(await reopened.ListAsync(_token));
        Assert.Equal(id, restored.Id);
        Assert.Equal(ImportQueue.State.Failed, restored.State);
        Assert.Equal(code, restored.Error);
        Assert.Equal(1, restored.Attempts);
    }

    [Fact]
    public async Task PendingJobsAndPrivateRightsSurviveReload_ThenExecuteOnceAndDeduplicate()
    {
        ImportQueue.Job submitted;
        await using (var queue = Open(CompleteAsync))
        {
            submitted = await SubmitAsync(queue);
            Assert.Equal(submitted.Id, (await SubmitAsync(queue)).Id);
            Assert.Single(await queue.ListAsync(_token));
        }

        var executions = 0;
        await using (var queue = Open(async (job, progress, token) =>
        {
            Interlocked.Increment(ref executions);
            return await CompleteAsync(job, progress, token);
        }))
        {
            var restored = Assert.Single(await queue.ListAsync(_token));
            Assert.Equal(submitted, restored);
            queue.Start();
            var completed = await WaitAsync(queue, submitted.Id, ImportQueue.State.Completed);
            Assert.Equal(1, executions);
            Assert.Equal(1, completed.Attempts);
            Assert.Equal(128, completed.Bytes);
            Assert.Equal(submitted.Id, (await SubmitAsync(queue)).Id);
            Assert.True(File.Exists(Path.Combine(_root, submitted.Id.ToString("N") + ".fixture")));
        }

        await using var final = Open(CompleteAsync);
        Assert.Equal(ImportQueue.State.Completed, Assert.Single(await final.ListAsync(_token)).State);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(_root, "queue.json")));
        }
    }

    [Fact]
    public async Task OneWorker_ReportsProgress_CancelsActiveAndQueuedJobs_ThenRunsNext()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var peak = 0;
        await using var queue = Open(async (job, progress, token) =>
        {
            peak = Math.Max(peak, Interlocked.Increment(ref active));
            try
            {
                if (job.Title == "Owned video")
                {
                    progress(42);
                    started.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }

                return await CompleteAsync(job, progress, token);
            }
            finally
            {
                Interlocked.Decrement(ref active);
                cleaned.TrySetResult();
            }
        });
        var first = await SubmitAsync(queue);
        var second = await SubmitAsync(queue, "queued");
        var third = await SubmitAsync(queue, "next");
        queue.Start();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), _token);
        Assert.Equal(42, (await queue.ListAsync(_token)).Single(job => job.Id.Equals(first.Id)).Bytes);
        Assert.Equal(ImportQueue.State.Cancelled, (await queue.CancelAsync(second.Id, _token)).State);
        await queue.CancelAsync(first.Id, _token);
        await cleaned.Task.WaitAsync(TimeSpan.FromSeconds(10), _token);
        await WaitAsync(queue, first.Id, ImportQueue.State.Cancelled);
        await WaitAsync(queue, third.Id, ImportQueue.State.Completed);
        Assert.Equal(1, peak);
        Assert.Equal(0, (await queue.ListAsync(_token)).Single(job => job.Id.Equals(second.Id)).Attempts);
    }

    [Fact]
    public async Task ShutdownPersistsInterruptedJob_ReloadRetriesSameId_WithBoundedAttempts()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ImportQueue.Job submitted;
        await using (var queue = Open(async (_, _, token) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Unreachable");
        }))
        {
            submitted = await SubmitAsync(queue);
            queue.Start();
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10), _token);
        }

        await using var recovered = Open(CompleteAsync);
        var pending = Assert.Single(await recovered.ListAsync(_token));
        Assert.Equal(submitted.Id, pending.Id);
        Assert.Equal(ImportQueue.State.Queued, pending.State);
        Assert.Equal("interrupted", pending.Error);
        recovered.Start();
        var completed = await WaitAsync(recovered, pending.Id, ImportQueue.State.Completed);
        Assert.Equal(2, completed.Attempts);
    }

    [Fact]
    public async Task FailedExecution_HasRedactedError_RetriesUpToThree_AndRetainsHistory()
    {
        await using var queue = Open((_, _, _) => throw new IOException("raw-exception-only-secret"));
        var job = await SubmitAsync(queue);
        queue.Start();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var failed = await WaitAsync(queue, job.Id, ImportQueue.State.Failed);
            Assert.Equal(attempt, failed.Attempts);
            Assert.Equal("executionFailed", failed.Error);
            Assert.DoesNotContain("raw-exception-only-secret", await File.ReadAllTextAsync(Path.Combine(_root, "queue.json"), _token), StringComparison.Ordinal);
            if (attempt < 3)
            {
                await queue.RetryAsync(job.Id, _token);
            }
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.RetryAsync(job.Id, _token));
        Assert.Single(await queue.ListAsync(_token));
    }

    [Fact]
    public async Task CancelledJobCannotRetryWhileReplacementIsQueued()
    {
        await using var queue = Open(CompleteAsync);
        var first = await SubmitAsync(queue);
        await queue.CancelAsync(first.Id, _token);
        var replacement = await SubmitAsync(queue);
        Assert.NotEqual(first.Id, replacement.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.RetryAsync(first.Id, _token));
    }

    [Fact]
    public async Task PublicationReceiptWinsCancellationRace_AndReloadDoesNotExecuteAgain()
    {
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Guid id;
        await using (var queue = Open(async (job, progress, token) =>
        {
            var receipt = await CompleteAsync(job, progress, token);
            published.TrySetResult();
            await release.Task.WaitAsync(_token);
            return receipt;
        }))
        {
            id = (await SubmitAsync(queue)).Id;
            queue.Start();
            await published.Task.WaitAsync(TimeSpan.FromSeconds(10), _token);
            await queue.CancelAsync(id, _token);
            release.TrySetResult();
            await WaitAsync(queue, id, ImportQueue.State.Completed);
        }

        await using var reload = Open((_, _, _) => throw new InvalidOperationException("Must not run"));
        reload.Start();
        Assert.Equal(ImportQueue.State.Completed, Assert.Single(await reload.ListAsync(_token)).State);
    }

    [Fact]
    public async Task InvalidPublicationReceiptFailsWithoutClaimingCompletion()
    {
        await using var queue = Open((_, _, _) => Task.FromResult(new ImportQueue.Receipt("invalid", 0)));
        var job = await SubmitAsync(queue);
        queue.Start();
        await WaitAsync(queue, job.Id, ImportQueue.State.Failed);
    }

    [Fact]
    public async Task ExclusiveOwnership_AndCorruptJournalFailClosedWithoutOverwriting()
    {
        await using (var queue = Open(CompleteAsync))
        {
            await SubmitAsync(queue);
            Assert.Throws<IOException>(() => Open(CompleteAsync));
        }

        var path = Path.Combine(_root, "queue.json");
        await File.WriteAllTextAsync(path, "{broken journal", _token);
        Assert.Throws<System.Text.Json.JsonException>(() => Open(CompleteAsync));
        Assert.Equal("{broken journal", await File.ReadAllTextAsync(path, _token));
    }

    [Theory]
    [InlineData("http://example.com/a.mp4")]
    [InlineData("https://127.0.0.1/a.mp4")]
    [InlineData("https://example.com/channel")]
    [InlineData("https://example.com/a.mp4\n--exec")]
    [InlineData("https://user:password@example.com/a.mp4")]
    public async Task UnsafeSourceRejectedWithoutPersistingJob(string source)
    {
        await using var queue = Open(CompleteAsync);
        await Assert.ThrowsAsync<InvalidDataException>(() => queue.SubmitAsync(source, _library, "Title", "Owned", _admin, _token));
        Assert.Empty(await queue.ListAsync(_token));
    }

    [Fact]
    public async Task DestinationAndRightsRequired_NoCallerPathInJournal()
    {
        await using var queue = Open(CompleteAsync);
        await Assert.ThrowsAsync<InvalidDataException>(() => queue.SubmitAsync("https://example.com/a.mp4", Guid.Empty, "Title", "Owned", _admin, _token));
        await Assert.ThrowsAsync<InvalidDataException>(() => queue.SubmitAsync("https://example.com/a.mp4", _library, "Title", string.Empty, _admin, _token));
        await Assert.ThrowsAsync<InvalidDataException>(() => queue.SubmitAsync("https://example.com/a.mp4", _library, "Title", "Owned", Guid.Empty, _token));
        Assert.Empty(await queue.ListAsync(_token));
    }

    [Fact]
    public async Task SymlinkedJournalRejectedWithoutTouchingTarget()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("POSIX symbolic link fixture; Windows reparse handling remains in shared path policy.");
        }

        Directory.CreateDirectory(_root);
        var target = Path.Combine(_root, "preserve.txt");
        await File.WriteAllTextAsync(target, "preserve", _token);
        File.CreateSymbolicLink(Path.Combine(_root, "queue.json"), target);
        Assert.Throws<InvalidDataException>(() => Open(CompleteAsync));
        Assert.Equal("preserve", await File.ReadAllTextAsync(target, _token));
    }

    [Theory]
    [InlineData("Running", 3, "Failed")]
    [InlineData("Cancelling", 1, "Cancelled")]
    public async Task CrashJournal_RespectsAttemptCapAndDurableCancellation(string state, int attempts, string expected)
    {
        await using (var queue = Open(CompleteAsync))
        {
            await SubmitAsync(queue);
        }

        var path = Path.Combine(_root, "queue.json");
        var journal = JsonNode.Parse(await File.ReadAllTextAsync(path, _token))!;
        journal["Jobs"]![0]!["State"] = (int)Enum.Parse<ImportQueue.State>(state);
        journal["Jobs"]![0]!["Attempts"] = attempts;
        await File.WriteAllTextAsync(path, journal.ToJsonString(), _token);
        await using var recovered = Open(CompleteAsync);
        Assert.Equal(Enum.Parse<ImportQueue.State>(expected), Assert.Single(await recovered.ListAsync(_token)).State);
    }

    [Theory]
    [InlineData("/var/folders/owned/queue", "/private/var", "/private/var/folders/owned/queue")]
    [InlineData("/tmp/owned/queue", "/private/tmp", "/private/tmp/owned/queue")]
    [InlineData("/var/folders/owned/queue", "/untrusted", "/var/folders/owned/queue")]
    [InlineData("/variable/queue", "/private/var", "/variable/queue")]
    [InlineData("/private/var/folders/owned/queue", "/private/var", "/private/var/folders/owned/queue")]
    [InlineData("/other/queue", "/private/var", "/other/queue")]
    public void MacSystemAlias_OnlyExactVerifiedOperatingSystemTargetsNormalize(string directory, string target, string expected)
    {
        Assert.Equal(expected, ImportQueue.MacDataDirectory(directory, _ => target));
    }

    [Fact]
    public void OrdinarySymlinkedParent_RemainsRejected()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("POSIX symbolic link fixture; Windows reparse handling remains in shared path policy.");
        }

        Directory.CreateDirectory(_root);
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(target);
        var alias = Path.Combine(_root, "ordinary-alias");
        Directory.CreateSymbolicLink(alias, target);
        Assert.Throws<InvalidDataException>(() => new ImportQueue(Path.Combine(alias, "queue"), CompleteAsync, NullLogger.Instance));
        Assert.False(Directory.Exists(Path.Combine(target, "queue")));
    }

    [Fact]
    public async Task NullJournalEntryFailsClosedAndPreservesTheOriginalFile()
    {
        await using (var queue = Open(CompleteAsync))
        {
            await SubmitAsync(queue);
        }

        var path = Path.Combine(_root, "queue.json");
        const string broken = "{\"Version\":1,\"Jobs\":[null]}";
        await File.WriteAllTextAsync(path, broken, _token);
        Assert.Throws<InvalidDataException>(() => Open(CompleteAsync));
        Assert.Equal(broken, await File.ReadAllTextAsync(path, _token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private ImportQueue Open(Func<ImportQueue.Job, Action<long>, CancellationToken, Task<ImportQueue.Receipt>> execute) => new(_root, execute, NullLogger.Instance);

    private Task<ImportQueue.Job> SubmitAsync(ImportQueue queue, string suffix = "") => queue.SubmitAsync("https://example.com/owned" + suffix + ".mp4?secret-source-token", _library, suffix.Length == 0 ? "Owned video" : suffix, "Creator-owned test material", _admin, _token);

    private async Task<ImportQueue.Receipt> CompleteAsync(ImportQueue.Job job, Action<long> progress, CancellationToken token)
    {
        // Actual private file I/O and checksum; this is not a claim of media/library validation.
        var bytes = new byte[128];
        var path = Path.Combine(_root, job.Id.ToString("N") + ".fixture");
        await File.WriteAllBytesAsync(path, bytes, token);
        progress(bytes.Length);
        return new ImportQueue.Receipt(Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length);
    }

    private async Task<ImportQueue.Job> WaitAsync(ImportQueue queue, Guid id, ImportQueue.State state)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            var job = (await queue.ListAsync(deadline.Token)).Single(candidate => candidate.Id.Equals(id));
            if (job.State == state)
            {
                return job;
            }

            await Task.Delay(10, deadline.Token);
        }
    }
}
