using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Api.Castlumora;

/// <summary>
/// Private, bounded single-worker journal owned by the hosted import service.
/// The executor must be idempotent for a job ID.
/// </summary>
internal sealed class ImportQueue : IAsyncDisposable
{
    private const int MaximumJobs = 1000;
    private readonly string _directory;
    private readonly string _journal;
    private readonly FileStream _ownership;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Func<Job, Action<long>, CancellationToken, Task<Receipt>> _execute;
    private readonly ILogger _logger;
    private readonly List<Job> _jobs;
    private readonly Dictionary<Guid, long> _progress = [];
    private Action? _cancelActive;
    private Guid _activeId;
    private Task? _worker;

    internal ImportQueue(string data, Func<Job, Action<long>, CancellationToken, Task<Receipt>> execute, ILogger logger)
    {
        var directory = Path.GetFullPath(data);
        _directory = OperatingSystem.IsMacOS()
            ? MacDataDirectory(directory, alias => new DirectoryInfo(alias).ResolveLinkTarget(returnFinalTarget: true)?.FullName)
            : directory;
        for (var parent = _directory; parent is not null; parent = Path.GetDirectoryName(parent))
        {
            YtDlpPlatform.RejectLink(parent);
        }

        YtDlpPlatform.PrivateDirectory(_directory);
        _journal = Path.Combine(_directory, "queue.json");
        _execute = execute;
        _logger = logger;
        var ownership = Path.Combine(_directory, "queue.lock");
        YtDlpPlatform.RejectLink(ownership);
        _ownership = new FileStream(ownership, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            PrivateFile(ownership);
            YtDlpPlatform.RejectLink(_journal);
            if (File.Exists(_journal))
            {
                if (new FileInfo(_journal).Length > 8388608)
                {
                    throw new InvalidDataException("The private import journal exceeds its safety limit.");
                }

                var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(_journal));
                if (journal is null || journal.Version != 1 || journal.Jobs is null || journal.Jobs.Count > MaximumJobs)
                {
                    throw new InvalidDataException("Unsupported import journal; preserve it for recovery.");
                }

                _jobs = journal.Jobs;
                foreach (var job in _jobs)
                {
                    if (job is null)
                    {
                        throw new InvalidDataException("Invalid import journal entry; preserve it for recovery.");
                    }

                    Validate(job.Source, job.DestinationId, job.Title, job.Rights, job.RequestedBy);
                    if (job.Id.Equals(Guid.Empty) || job.Attempts is < 0 or > 3 || !Enum.IsDefined(job.State)
                        || job.SourceKey != Key(job.Source, job.DestinationId)
                        || (job.State == State.Completed && !ValidReceipt(job.Sha256, job.Bytes))
                        || (job.State == State.Queued && job.Attempts >= 3))
                    {
                        throw new InvalidDataException("Invalid import journal; preserve it for recovery.");
                    }
                }

                if (_jobs.Select(job => job.Id).Distinct().Count() != _jobs.Count)
                {
                    throw new InvalidDataException("Duplicate import journal IDs.");
                }

                // Interrupted execution is retried by the same ID; publication must reconcile its receipt.
                _jobs = _jobs.Select(job => job.State switch
                {
                    State.Running => job with { State = job.Attempts < 3 ? State.Queued : State.Failed, Error = "interrupted" },
                    State.Cancelling => job with { State = State.Cancelled, Error = null },
                    _ => job
                }).ToList();
            }
            else
            {
                _jobs = [];
            }

            Save();
        }
        catch
        {
            _ownership.Dispose();
            _gate.Dispose();
            _wake.Dispose();
            _shutdown.Dispose();
            throw;
        }
    }

    internal enum State
    {
        Queued,
        Running,
        Cancelling,
        Cancelled,
        Failed,
        Completed
    }

    internal Task Completion => _worker ?? Task.CompletedTask;

    internal void Start()
    {
        if (_worker is not null)
        {
            throw new InvalidOperationException("The import worker is already started.");
        }

        _worker = RunAsync();
        Wake();
    }

    internal async Task<Job> SubmitAsync(string source, Guid destinationId, string title, string rights, Guid requestedBy, CancellationToken token)
    {
        source = Validate(source, destinationId, title, rights, requestedBy);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var key = Key(source, destinationId);
            var duplicate = _jobs.Find(job => job.SourceKey == key && job.State is State.Queued or State.Running or State.Cancelling or State.Completed);
            if (duplicate is not null)
            {
                return duplicate;
            }

            if (_jobs.Count >= MaximumJobs)
            {
                throw new InvalidOperationException("The import history limit is reached; retain the journal and review storage before adding jobs.");
            }

            var job = new Job(Guid.NewGuid(), source, key, destinationId, title, rights, requestedBy, DateTimeOffset.UtcNow);
            _jobs.Add(job);
            try
            {
                Save();
            }
            catch
            {
                _jobs.Remove(job);
                throw;
            }

            Wake();
            return job;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<Job[]> ListAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return _jobs.Select(job => _progress.TryGetValue(job.Id, out var bytes) ? job with { Bytes = bytes } : job).ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<Job> CancelAsync(Guid id, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var job = Find(id);
            if (job.State is State.Queued or State.Running)
            {
                job = job with { State = job.State == State.Running ? State.Cancelling : State.Cancelled };
                Replace(job);
                if (_activeId.Equals(id))
                {
                    _cancelActive?.Invoke();
                }
            }

            return job;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<Job> RetryAsync(Guid id, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var job = Find(id);
            if (job.State is not (State.Failed or State.Cancelled) || job.Attempts >= 3
                || _jobs.Any(other => !other.Id.Equals(id) && other.SourceKey == job.SourceKey && other.State is State.Queued or State.Running or State.Cancelling or State.Completed))
            {
                throw new InvalidOperationException("Only failed or cancelled jobs with fewer than three attempts and no duplicate can be retried.");
            }

            job = job with { State = State.Queued, Error = null, Bytes = 0 };
            Replace(job);
            Wake();
            return job;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _shutdown.CancelAsync().ConfigureAwait(false);
            if (_worker is not null)
            {
                await _worker.ConfigureAwait(false);
            }
        }
        finally
        {
            _ownership.Dispose();
            _gate.Dispose();
            _wake.Dispose();
            _shutdown.Dispose();
        }
    }

    // macOS owns these root aliases. Normalize only after verifying the exact system
    // target; arbitrary aliases/reparse points throughout the resulting path still fail.
    internal static string MacDataDirectory(string directory, Func<string, string?> resolve)
    {
        foreach (var alias in new[] { "/var", "/tmp" })
        {
            var expected = "/private" + alias;
            if (directory.StartsWith(alias + "/", StringComparison.Ordinal)
                && string.Equals(resolve(alias), expected, StringComparison.Ordinal))
            {
                return expected + directory[alias.Length..];
            }
        }

        return directory;
    }

    private async Task RunAsync()
    {
        try
        {
            while (true)
            {
                await _wake.WaitAsync(_shutdown.Token).ConfigureAwait(false);
                while (!_shutdown.IsCancellationRequested)
                {
                    Job? job;
                    using var stop = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                    await _gate.WaitAsync(_shutdown.Token).ConfigureAwait(false);
                    try
                    {
                        job = _jobs.Find(candidate => candidate.State == State.Queued);
                        if (job is null)
                        {
                            break;
                        }

                        job = job with { State = State.Running, Attempts = job.Attempts + 1, Error = null };
                        Replace(job);
                        _cancelActive = stop.Cancel;
                        _activeId = job.Id;
                    }
                    finally
                    {
                        _gate.Release();
                    }

                    Receipt? receipt = null;
                    string? failureCode = null;
                    try
                    {
                        receipt = await _execute(job, bytes => Report(job.Id, bytes), stop.Token).ConfigureAwait(false);
                        if (!ValidReceipt(receipt.Sha256, receipt.Bytes))
                        {
                            throw new InvalidDataException("Invalid import publication receipt.");
                        }
                    }
                    catch (OperationCanceledException) when (stop.IsCancellationRequested)
                    {
                        // User cancellation and shutdown are distinguished below and durably recorded.
                    }
                    catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or TimeoutException or System.ComponentModel.Win32Exception or NotSupportedException)
                    {
                        receipt = null;
                        failureCode = ImportFailure.Code(exception);
                        // Deliberately exclude exception text, URLs, query tokens, filenames and raw subprocess output.
                        _logger.LogWarning("Import {ImportId} attempt {Attempt} failed ({ErrorCode}, {ErrorType})", job.Id, job.Attempts, failureCode, exception.GetType().Name);
                    }

                    await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                    try
                    {
                        var current = Find(job.Id);
                        var state = receipt is not null ? State.Completed
                            : current.State == State.Cancelling ? State.Cancelled
                            : _shutdown.IsCancellationRequested ? State.Running : State.Failed;
                        Replace(current with { State = state, Sha256 = receipt?.Sha256, Bytes = receipt?.Bytes ?? 0, Error = failureCode });
                        _progress.Remove(job.Id);
                        _cancelActive = null;
                        _activeId = Guid.Empty;
                    }
                    finally
                    {
                        _gate.Release();
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // A persisted Running job is reconciled on the next startup, never silently lost.
        }
    }

    private void Report(Guid id, long bytes)
    {
        if (bytes < 0 || !_gate.Wait(0))
        {
            return;
        }

        try
        {
            _progress[id] = bytes;
        }
        finally
        {
            _gate.Release();
        }
    }

    private Job Find(Guid id) => _jobs.Find(job => job.Id.Equals(id)) ?? throw new KeyNotFoundException("Import job not found.");

    private void Replace(Job job)
    {
        var index = _jobs.FindIndex(candidate => candidate.Id.Equals(job.Id));
        var previous = _jobs[index];
        _jobs[index] = job;
        try
        {
            Save();
        }
        catch
        {
            _jobs[index] = previous;
            throw;
        }
    }

    private void Save()
    {
        YtDlpPlatform.RejectLink(_directory);
        YtDlpPlatform.RejectLink(_journal);
        var temporary = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                PrivateFile(temporary);
                JsonSerializer.Serialize(stream, new Journal(1, _jobs));
                if (stream.Length > 8388608)
                {
                    throw new InvalidDataException("The private import journal exceeds its safety limit.");
                }

                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, _journal, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static void PrivateFile(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static string Validate(string source, Guid destinationId, string title, string rights, Guid requestedBy)
    {
        var uri = ImportNetworkPolicy.ValidateSource(source);
        if (uri.AbsoluteUri.Length > 4096 || !uri.AbsolutePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || destinationId.Equals(Guid.Empty) || requestedBy.Equals(Guid.Empty)
            || string.IsNullOrWhiteSpace(title) || title.Length > 200 || title.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(rights) || rights.Length > 2000 || rights.Any(char.IsControl))
        {
            throw new InvalidDataException("A direct HTTPS MP4 source, destination library ID, title, rights record and administrator ID are required.");
        }

        return uri.AbsoluteUri;
    }

    private static bool ValidReceipt(string? sha256, long bytes) => bytes is > 0 and <= 33554432 && sha256 is { Length: 64 } && sha256.All(char.IsAsciiHexDigit);

    private static string Key(string source, Guid destinationId) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(destinationId.ToString("N") + "\n" + source)));

    private void Wake()
    {
        if (_wake.CurrentCount == 0)
        {
            _wake.Release();
        }
    }

    internal sealed record Receipt(string Sha256, long Bytes);

    internal sealed record Job(Guid Id, string Source, string SourceKey, Guid DestinationId, string Title, string Rights, Guid RequestedBy, DateTimeOffset CreatedAt, State State = State.Queued, int Attempts = 0, long Bytes = 0, string? Sha256 = null, string? Error = null);

    private sealed record Journal(int Version, List<Job> Jobs);
}
