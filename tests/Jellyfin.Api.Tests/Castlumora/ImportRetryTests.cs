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

public sealed class ImportRetryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "castlumora-retry-" + Guid.NewGuid().ToString("N"));
    private readonly TestClock _clock = new();
    private readonly CancellationToken _token = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("sourceNetwork", 1, 15)]
    [InlineData("sourceNetwork", 2, 30)]
    [InlineData("sourceRateLimited", 1, 60)]
    [InlineData("sourceRateLimited", 2, 120)]
    [InlineData("sourceNetwork", 0, 0)]
    [InlineData("sourceNetwork", 3, 0)]
    [InlineData("sourceRateLimited", 4, 0)]
    [InlineData("certificateRejected", 1, 0)]
    [InlineData("untrusted-private-diagnostic", 1, 0)]
    public void OnlyRecognizedTransientFailuresWithinTheBudgetHaveBackoff(string code, int attempts, int seconds)
    {
        Assert.Equal(seconds == 0 ? null : (TimeSpan?)TimeSpan.FromSeconds(seconds), ImportRetryPolicy.Delay(code, attempts));
    }

    [Theory]
    [InlineData("sourceNetwork")]
    [InlineData("sourceRateLimited")]
    public async Task OptInBackoff_PersistsAndDoesNotExecuteBeforeDeadline(string code)
    {
        await using var queue = Open((job, progress, token) => job.Attempts == 1 ? throw ImportFailure.Download(code) : CompleteAsync(job, progress, token));
        var submitted = await SubmitAsync(queue);
        queue.Start();
        var waiting = await WaitAsync(queue, submitted.Id, job => job.NextAttemptAt is not null);
        var delay = ImportRetryPolicy.Delay(code, 1)!.Value;
        Assert.Equal(_clock.GetUtcNow().Add(delay), waiting.NextAttemptAt);
        Assert.Equal(1, waiting.Attempts);
        Assert.Equal(ImportQueue.State.Queued, waiting.State);
        _clock.Advance(delay - TimeSpan.FromSeconds(1));
        // Let the real worker poll; advancing only the clock must not bypass the deadline.
        await Task.Delay(1100, _token);
        Assert.Equal(1, Assert.Single(await queue.ListAsync(_token)).Attempts);
        _clock.Advance(TimeSpan.FromSeconds(1));
        var completed = await WaitAsync(queue, submitted.Id, job => job.State == ImportQueue.State.Completed);
        Assert.Equal(2, completed.Attempts);
        Assert.Null(completed.NextAttemptAt);
        Assert.Null(completed.Error);
        Assert.True(File.Exists(Path.Combine(_root, completed.Id.ToString("N") + ".fixture")));
    }

    [Fact]
    public async Task WaitingRetry_SurvivesReloadWithExactDeadlineIdentityAndRights()
    {
        ImportQueue.Job waiting;
        await using (var queue = Open((_, _, _) => throw ImportFailure.Download("sourceRateLimited")))
        {
            var submitted = await SubmitAsync(queue);
            queue.Start();
            waiting = await WaitAsync(queue, submitted.Id, job => job.NextAttemptAt is not null);
        }

        await using var reopened = Open(CompleteAsync);
        Assert.Equal(waiting, Assert.Single(await reopened.ListAsync(_token)));
        Assert.True(waiting.AutomaticRetries);
        reopened.Start();
        _clock.Advance(TimeSpan.FromMinutes(1));
        var completed = await WaitAsync(reopened, waiting.Id, job => job.State == ImportQueue.State.Completed);
        Assert.Equal(waiting.Id, completed.Id);
        Assert.Equal(waiting.Rights, completed.Rights);
        Assert.Equal(waiting.RequestedBy, completed.RequestedBy);
        Assert.Equal(2, completed.Attempts);
    }

    [Fact]
    public async Task RepeatedTransientFailures_ExhaustTheSharedThreeAttemptBudget()
    {
        await using var queue = Open((_, _, _) => throw ImportFailure.Download("sourceNetwork"));
        var submitted = await SubmitAsync(queue);
        queue.Start();
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var expected = attempt;
            var waiting = await WaitAsync(queue, submitted.Id, job => job.Attempts == expected && job.NextAttemptAt is not null);
            Assert.Equal(_clock.GetUtcNow().AddSeconds(15 * attempt), waiting.NextAttemptAt);
            _clock.Advance(TimeSpan.FromSeconds(15 * attempt));
        }

        var failed = await WaitAsync(queue, submitted.Id, job => job.State == ImportQueue.State.Failed);
        Assert.Equal(3, failed.Attempts);
        Assert.Null(failed.NextAttemptAt);
        Assert.Equal("sourceNetwork", failed.Error);
        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.RetryAsync(failed.Id, _token));
    }

    [Theory]
    [InlineData("certificateRejected")]
    [InlineData("sourceDenied")]
    [InlineData("sizeLimit")]
    [InlineData("incompleteOutput")]
    [InlineData("unexpectedOutput")]
    [InlineData("unsupportedSource")]
    [InlineData("downloadFailed")]
    public async Task PermanentAndSecurityFailures_RequireAdministratorActionEvenWhenOptedIn(string code)
    {
        await using var queue = Open((_, _, _) => throw ImportFailure.Download(code));
        var submitted = await SubmitAsync(queue);
        queue.Start();
        var failed = await WaitAsync(queue, submitted.Id, job => job.State == ImportQueue.State.Failed);
        Assert.Equal(1, failed.Attempts);
        Assert.Null(failed.NextAttemptAt);
        Assert.Equal(code, failed.Error);
    }

    [Fact]
    public async Task WaitingRetry_DoesNotBlockReadyJobsAndCanBeCancelledDurably()
    {
        await using (var queue = Open((job, progress, token) => job.Title == "waiting" ? throw ImportFailure.Download("sourceNetwork") : CompleteAsync(job, progress, token)))
        {
            var waiting = await SubmitAsync(queue, "waiting");
            queue.Start();
            await WaitAsync(queue, waiting.Id, job => job.NextAttemptAt is not null);
            Assert.Equal(waiting.Id, (await queue.SubmitAsync(waiting.Source, waiting.DestinationId, waiting.Title, waiting.Rights, waiting.RequestedBy, _token)).Id);
            var ready = await SubmitAsync(queue, "ready");
            await WaitAsync(queue, ready.Id, job => job.State == ImportQueue.State.Completed);
            var cancelled = await queue.CancelAsync(waiting.Id, _token);
            Assert.Equal(ImportQueue.State.Cancelled, cancelled.State);
            Assert.Equal(1, cancelled.Attempts);
            Assert.Null(cancelled.NextAttemptAt);
        }

        _clock.Advance(TimeSpan.FromHours(1));
        await using var reopened = Open(CompleteAsync);
        reopened.Start();
        var cancelledAfterRestart = (await reopened.ListAsync(_token)).Single(job => job.Title == "waiting");
        Assert.Equal(ImportQueue.State.Cancelled, cancelledAfterRestart.State);
        Assert.Equal(1, cancelledAfterRestart.Attempts);
    }

    [Fact]
    public async Task OldJournalWithoutRetryFields_RemainsManualByDefault()
    {
        await using (var queue = Open(CompleteAsync))
        {
            await SubmitAsync(queue);
        }

        var journalPath = Path.Combine(_root, "queue.json");
        var journal = JsonNode.Parse(await File.ReadAllTextAsync(journalPath, _token))!;
        var job = journal["Jobs"]![0]!.AsObject();
        job.Remove("AutomaticRetries");
        job.Remove("NextAttemptAt");
        await File.WriteAllTextAsync(journalPath, journal.ToJsonString(), _token);
        await using var reopened = Open(CompleteAsync);
        var restored = Assert.Single(await reopened.ListAsync(_token));
        Assert.False(restored.AutomaticRetries);
        Assert.Null(restored.NextAttemptAt);
    }

    [Theory]
    [InlineData(false, 0, "sourceNetwork")]
    [InlineData(true, 4, "sourceNetwork")]
    [InlineData(true, 0, "certificateRejected")]
    public async Task InvalidScheduledJournal_IsRejectedWithoutOverwritingIt(bool enabled, int state, string error)
    {
        await using (var queue = Open(CompleteAsync))
        {
            await SubmitAsync(queue);
        }

        var path = Path.Combine(_root, "queue.json");
        var journal = JsonNode.Parse(await File.ReadAllTextAsync(path, _token))!;
        var job = journal["Jobs"]![0]!;
        job["AutomaticRetries"] = enabled;
        job["NextAttemptAt"] = _clock.GetUtcNow().AddMinutes(1).ToString("O");
        job["Attempts"] = 1;
        job["State"] = state;
        job["Error"] = error;
        var invalid = journal.ToJsonString();
        await File.WriteAllTextAsync(path, invalid, _token);
        Assert.Throws<InvalidDataException>(() => Open(CompleteAsync));
        Assert.Equal(invalid, await File.ReadAllTextAsync(path, _token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private ImportQueue Open(Func<ImportQueue.Job, Action<long>, CancellationToken, Task<ImportQueue.Receipt>> execute) => new(_root, execute, NullLogger.Instance, _clock);

    private Task<ImportQueue.Job> SubmitAsync(ImportQueue queue, string title = "owned") => queue.SubmitAsync("https://example.com/" + title + ".mp4", Guid.NewGuid(), title, "Owned test media", Guid.NewGuid(), _token, automaticRetries: true);

    private async Task<ImportQueue.Receipt> CompleteAsync(ImportQueue.Job job, Action<long> progress, CancellationToken token)
    {
        var bytes = new byte[128];
        await File.WriteAllBytesAsync(Path.Combine(_root, job.Id.ToString("N") + ".fixture"), bytes, token);
        progress(bytes.Length);
        return new ImportQueue.Receipt(Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length);
    }

    private async Task<ImportQueue.Job> WaitAsync(ImportQueue queue, Guid id, Func<ImportQueue.Job, bool> condition)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            var job = (await queue.ListAsync(deadline.Token)).Single(candidate => candidate.Id.Equals(id));
            if (condition(job))
            {
                return job;
            }

            await Task.Delay(10, deadline.Token);
        }
    }

    private sealed class TestClock : TimeProvider
    {
        private long _ticks = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;

        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

        internal void Advance(TimeSpan duration) => Interlocked.Add(ref _ticks, duration.Ticks);
    }
}
