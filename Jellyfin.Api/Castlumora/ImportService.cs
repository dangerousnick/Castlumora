using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Api.Castlumora;

/// <summary>Administrator-only durable direct-video import lifecycle, owned by the server host.</summary>
public sealed class ImportService : IHostedService, IAsyncDisposable
{
    private readonly YtDlpRuntime _runtime;
    private readonly IServerApplicationHost _host;
    private readonly IApplicationPaths _paths;
    private readonly ILibraryManager _libraries;
    private readonly IUserManager _users;
    private readonly ILibraryMonitor _monitor;
    private readonly IMediaEncoder _encoder;
    private readonly ILogger<ImportService> _logger;
    private readonly Func<ImportPublication> _publicationFactory;
    private readonly SemaphoreSlim _previewGate = new(1, 1);
    private readonly CancellationTokenSource _previewStop = new();
    private int _disposeStarted;
    private ImportQueue? _queue;
    private ImportPublication? _publication;
    private string _failure = "The import queue has not started.";

    /// <summary>Initializes a new instance of the <see cref="ImportService"/> class.</summary>
    /// <param name="runtime">Reviewed managed downloader.</param>
    /// <param name="host">Core initialization readiness for recovered jobs.</param>
    /// <param name="paths">Existing application paths.</param>
    /// <param name="libraries">Existing library registry.</param>
    /// <param name="users">Existing administrator permissions.</param>
    /// <param name="monitor">Existing library refresh integration.</param>
    /// <param name="encoder">Existing configured media engine.</param>
    /// <param name="logger">Structured diagnostics without source secrets.</param>
    public ImportService(YtDlpRuntime runtime, IServerApplicationHost host, IApplicationPaths paths, ILibraryManager libraries, IUserManager users, ILibraryMonitor monitor, IMediaEncoder encoder, ILogger<ImportService> logger)
    {
        _runtime = runtime;
        _host = host;
        _paths = paths;
        _libraries = libraries;
        _users = users;
        _monitor = monitor;
        _encoder = encoder;
        _logger = logger;
        _publicationFactory = () => new ImportPublication(_runtime, _paths, _libraries, _users, _monitor, _encoder, _logger);
    }

    internal ImportService(YtDlpRuntime runtime, IServerApplicationHost host, IApplicationPaths paths, ILibraryManager libraries, IUserManager users, ILibraryMonitor monitor, IMediaEncoder encoder, ILogger<ImportService> logger, Func<ImportPublication> publicationFactory)
        : this(runtime, host, paths, libraries, users, monitor, encoder, logger)
    {
        _publicationFactory = publicationFactory;
    }

    /// <summary>Gets a value indicating whether this host can accept the implemented import workflow.</summary>
    public bool Available => Volatile.Read(ref _disposeStarted) == 0 && _host.CoreStartupHasCompleted && (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        && _queue is { Completion.IsCompleted: false }
        && !string.IsNullOrWhiteSpace(_encoder.ProbePath) && Path.IsPathFullyQualified(_encoder.ProbePath) && File.Exists(_encoder.ProbePath)
        && !string.IsNullOrWhiteSpace(_encoder.EncoderPath) && Path.IsPathFullyQualified(_encoder.EncoderPath) && File.Exists(_encoder.EncoderPath);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            _failure = "This import preview targets Linux and macOS; existing server functionality is unchanged.";
            return Task.CompletedTask;
        }

        try
        {
            _publication = _publicationFactory();
            _queue = new ImportQueue(Path.Combine(_paths.DataPath, "castlumora", "imports", "queue"), ExecuteAsync, _logger);
            // Queue constructor holds exclusive ownership before abandoned attempts are touched.
            ImportStagingRecovery.Recover(_paths.DataPath, _logger);
            _queue.Start();
            _failure = "Check the configured FFmpeg/FFprobe dependencies or queue diagnostics.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            _failure = "The private import journal is unavailable. Preserve its directory and inspect administrator diagnostics.";
            _logger.LogError("Import queue initialization failed ({ErrorType}); existing server functions remain available", exception.GetType().Name);
        }

        return Task.CompletedTask;
    }

    /// <summary>Returns private administrator queue state with source query credentials omitted.</summary>
    /// <param name="administrator">Authenticated administrator, never request-body identity.</param>
    /// <param name="token">Request cancellation.</param>
    /// <returns>Capabilities, eligible libraries and bounded job history.</returns>
    public async Task<JsonObject> StateAsync(Guid administrator, CancellationToken token)
    {
        var destinations = new JsonArray();
        if (_publication is not null && !administrator.Equals(Guid.Empty))
        {
            foreach (var folder in _libraries.GetVirtualFolders().Where(folder => folder.CollectionType == CollectionTypeOptions.homevideos))
            {
                if (!Guid.TryParse(folder.ItemId, out var id))
                {
                    continue;
                }

                try
                {
                    _publication.ValidateDestination(administrator, id);
                    destinations.Add(new JsonObject { ["id"] = id.ToString("N"), ["name"] = folder.Name });
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    // Do not offer a destination that cannot pass the same execution-time policy.
                }
            }
        }

        var jobs = new JsonArray();
        var queue = _queue;
        if (queue is not null)
        {
            foreach (var job in (await queue.ListAsync(token).ConfigureAwait(false)).OrderByDescending(job => job.CreatedAt))
            {
                jobs.Add(View(job));
            }
        }

        return new JsonObject
        {
            ["available"] = Available,
            ["message"] = Available ? "Direct HTTPS H264/AAC MP4 preview: 32 MiB per video, one worker, up to three attempts. Completed files request library scanning." : _failure,
            ["destinations"] = destinations,
            ["jobs"] = jobs,
            ["maxMediaBytes"] = 33554432,
            ["youtubeImportsAvailable"] = Available && OperatingSystem.IsLinux() && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64 && File.Exists(Path.Combine(_paths.ProgramSystemPath, "tools", "node", YouTubeNodeRuntime.Version, "linux-x64", "node")),
            ["concurrency"] = 1,
            ["playlistsAvailable"] = false,
            ["channelsAvailable"] = false,
            ["batchImportsAvailable"] = Available,
            ["maxBatchEntries"] = 25
        };
    }

    /// <summary>Persists one rights-confirmed administrator import.</summary>
    /// <param name="value">URL, title, library ID and rights record only.</param>
    /// <param name="administrator">Authenticated administrator.</param>
    /// <param name="token">Submission cancellation, not the job lifetime.</param>
    /// <returns>Existing duplicate or new persistent job, with private query values omitted.</returns>
    public async Task<JsonObject> SubmitAsync(JsonObject value, Guid administrator, CancellationToken token)
    {
        if (value.Any(pair => pair.Key is not ("url" or "destinationId" or "title" or "rights" or "rightsConfirmed" or "automaticRetries")))
        {
            throw new InvalidDataException("Only source, title, library, rights and retry options are accepted.");
        }

        var options = ReadOptions(value);
        var queue = await PrepareAsync(administrator, options.Library, token).ConfigureAwait(false);
        var job = await queue.SubmitAsync(Text(value, "url"), options.Library, Text(value, "title"), options.Rights, administrator, token, options.Automatic).ConfigureAwait(false);
        return View(job);
    }

    /// <summary>Extracts a single authorized source into a private administrator/library-bound confirmation.</summary>
    /// <param name="value">Source, library and bounded quality/resource choices.</param>
    /// <param name="administrator">Authenticated administrator.</param>
    /// <param name="token">Preview cancellation.</param>
    /// <returns>Safe metadata and a short-lived opaque confirmation ID.</returns>
    public async Task<JsonObject> PreviewYouTubeAsync(JsonObject value, Guid administrator, CancellationToken token)
    {
        if (Volatile.Read(ref _disposeStarted) != 0)
        {
            throw new InvalidOperationException("The import service is stopping.");
        }

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, _previewStop.Token);
        token = lifetime.Token;
        if (value.Any(pair => pair.Key is not ("url" or "destinationId" or "maximumHeight" or "maximumMiB"))
            || !Guid.TryParse(Text(value, "destinationId"), out var library))
        {
            throw new InvalidDataException("Provide a source, destination and bounded quality/size choices.");
        }

        var height = Number(value, "maximumHeight");
        var size = Number(value, "maximumMiB");
        if (height is not (360 or 480 or 720 or 1080) || size is not (32 or 128 or 256 or 512))
        {
            throw new InvalidDataException("Choose a supported quality and size budget.");
        }

        var source = YouTubeVideoSource.Parse(Text(value, "url"));
        await PrepareAsync(administrator, library, token).ConfigureAwait(false);
        if (!await _previewGate.WaitAsync(0, token).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Another YouTube preview is running; wait for it to finish.");
        }

        try
        {
            var metadata = await YouTubeMetadataPreview.ReadAsync(_runtime, _paths.ProgramSystemPath, _paths.DataPath, source.CanonicalUrl, token).ConfigureAwait(false);
            var options = YouTubeImportOptions.Select(metadata, height, size);
            var preview = new YouTubePreviewStore(_paths.DataPath).Create(administrator, library, source.CanonicalUrl, metadata.Title[..Math.Min(metadata.Title.Length, 200)], options);
            return new JsonObject
            {
                ["previewId"] = preview.Id.ToString("N"),
                ["videoId"] = options.VideoId,
                ["channelId"] = options.ChannelId,
                ["title"] = preview.Title,
                ["creator"] = options.Creator,
                ["description"] = options.Description,
                ["height"] = options.Height,
                ["maximumBytes"] = options.MaximumBytes,
                ["duration"] = options.Duration,
                ["expiresAt"] = preview.Expires.ToString("O", CultureInfo.InvariantCulture)
            };
        }
        finally
        {
            _previewGate.Release();
        }
    }

    /// <summary>Persists only a source reviewed by the same administrator for the same library.</summary>
    /// <param name="value">Opaque preview ID, title and explicit rights/destination confirmation.</param>
    /// <param name="administrator">Authenticated administrator.</param>
    /// <param name="token">Submission cancellation.</param>
    /// <returns>A durable source-ID duplicate or new job.</returns>
    public async Task<JsonObject> SubmitYouTubeAsync(JsonObject value, Guid administrator, CancellationToken token)
    {
        if (value.Any(pair => pair.Key is not ("previewId" or "destinationId" or "title" or "rights" or "rightsConfirmed" or "automaticRetries"))
            || !Guid.TryParse(Text(value, "previewId"), out var id))
        {
            throw new InvalidDataException("A reviewed preview and explicit rights/destination confirmation are required.");
        }

        var options = ReadOptions(value);
        var queue = await PrepareAsync(administrator, options.Library, token).ConfigureAwait(false);
        var preview = new YouTubePreviewStore(_paths.DataPath).Read(id, administrator, options.Library);
        var jobs = await queue.SubmitManyAsync([(preview.Source, Text(value, "title"))], options.Library, options.Rights, administrator, token, options.Automatic, preview.Options).ConfigureAwait(false);
        return View(jobs[0]);
    }

    private static int Number(JsonObject value, string key)
        => value[key] is JsonValue number && number.TryGetValue<int>(out var result) ? result : throw new InvalidDataException("Quality and size choices must be supported integers.");

    /// <summary>Atomically persists up to twenty-five rights-confirmed direct-video entries.</summary>
    /// <param name="value">Entries, library, common rights and retry options.</param>
    /// <param name="administrator">Authenticated administrator.</param>
    /// <param name="token">Submission cancellation.</param>
    /// <returns>One job reference per entry; duplicates retain their existing identity and policy.</returns>
    public async Task<JsonObject> SubmitBatchAsync(JsonObject value, Guid administrator, CancellationToken token)
    {
        if (value.Any(pair => pair.Key is not ("entries" or "destinationId" or "rights" or "rightsConfirmed" or "automaticRetries"))
            || value["entries"] is not JsonArray entries || entries.Count is < 1 or > 25)
        {
            throw new InvalidDataException("Submit one to twenty-five entries with an eligible library and confirmed rights.");
        }

        var parsed = entries.Select(entry =>
        {
            if (entry is not JsonObject item || item.Any(pair => pair.Key is not ("url" or "title")))
            {
                throw new InvalidDataException("Each entry accepts only a direct URL and title.");
            }

            return (Source: Text(item, "url"), Title: Text(item, "title"));
        }).ToArray();
        var options = ReadOptions(value);
        var queue = await PrepareAsync(administrator, options.Library, token).ConfigureAwait(false);
        var jobs = await queue.SubmitManyAsync(parsed, options.Library, options.Rights, administrator, token, options.Automatic).ConfigureAwait(false);
        var result = new JsonArray();
        foreach (var job in jobs)
        {
            result.Add(View(job));
        }

        return new JsonObject { ["jobs"] = result };
    }

    /// <summary>Cancels one job or explicitly retries a failed/cancelled job.</summary>
    /// <param name="id">Persistent job ID.</param>
    /// <param name="retry">Retry rather than cancel.</param>
    /// <param name="token">Request cancellation.</param>
    /// <returns>Persisted state with private source queries omitted.</returns>
    public async Task<JsonObject> ChangeAsync(Guid id, bool retry, CancellationToken token)
    {
        var queue = Required();
        var job = retry ? await queue.RetryAsync(id, token).ConfigureAwait(false) : await queue.CancelAsync(id, token).ConfigureAwait(false);
        return View(job);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken) => await DisposeAsync().ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        await _previewStop.CancelAsync().ConfigureAwait(false);
        var queue = Interlocked.Exchange(ref _queue, null);
        if (queue is not null)
        {
            try
            {
                await queue.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                _logger.LogError("Import queue stopped after storage failure ({ErrorType}); persisted jobs require recovery", exception.GetType().Name);
            }
        }
        await _previewGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        _previewGate.Dispose();
        _previewStop.Dispose();
    }

    private async Task<ImportQueue> PrepareAsync(Guid administrator, Guid library, CancellationToken token)
    {
        var queue = Required();
        _publication!.ValidateDestination(administrator, library);
        var runtime = await _runtime.StatusAsync(token).ConfigureAwait(false);
        if (runtime["ready"]?.GetValue<bool>() != true)
        {
            throw new InvalidOperationException("Install and verify the bundled managed runtime before submitting an import.");
        }

        return queue;
    }

    private static (Guid Library, string Rights, bool Automatic) ReadOptions(JsonObject value)
    {
        if (value["rightsConfirmed"] is not JsonValue confirmed || !confirmed.TryGetValue<bool>(out var rights) || !rights
            || !Guid.TryParse(Text(value, "destinationId"), out var library))
        {
            throw new InvalidDataException("Confirm rights and select an eligible destination library.");
        }

        var automatic = false;
        if (value.ContainsKey("automaticRetries") && (value["automaticRetries"] is not JsonValue retry || !retry.TryGetValue<bool>(out automatic)))
        {
            throw new InvalidDataException("Automatic retries must be an explicit boolean.");
        }

        return (library, Text(value, "rights"), automatic);
    }

    private static string Text(JsonObject value, string key)
        => value[key] is JsonValue text && text.TryGetValue<string>(out var result) ? result : throw new InvalidDataException("Required import fields must be text.");

    private async Task<ImportQueue.Receipt> ExecuteAsync(ImportQueue.Job job, Action<long> progress, CancellationToken token)
    {
        // Hosted services start before core FFmpeg initialization. Persist/recover
        // jobs immediately, but do not execute downloads until the core is ready.
        while (!_host.CoreStartupHasCompleted)
        {
            await Task.Delay(100, token).ConfigureAwait(false);
        }

        return await _publication!.ExecuteAsync(job, progress, token).ConfigureAwait(false);
    }

    private ImportQueue Required()
    {
        if (!Available)
        {
            throw new InvalidOperationException(_failure);
        }

        return _queue!;
    }

    private static JsonObject View(ImportQueue.Job job) => new()
    {
        ["id"] = job.Id.ToString("N"),
        ["title"] = job.Title,
        ["sourceHost"] = new Uri(job.Source).IdnHost,
        ["sourceType"] = job.YouTube is null ? "direct" : "youtube",
        ["videoId"] = job.YouTube?.VideoId,
        ["creator"] = job.YouTube?.Creator,
        ["maximumBytes"] = job.YouTube?.MaximumBytes ?? 33554432,
        ["destinationId"] = job.DestinationId.ToString("N"),
        ["requestedBy"] = job.RequestedBy.ToString("N"),
        ["rights"] = job.Rights,
        ["state"] = job.State.ToString().ToLowerInvariant(),
        ["attempts"] = job.Attempts,
        ["bytes"] = job.Bytes,
        ["sha256"] = job.Sha256,
        ["error"] = ImportFailure.PublicCode(job.Error),
        ["automaticRetries"] = job.AutomaticRetries,
        ["nextAttemptAt"] = job.NextAttemptAt?.ToString("O", CultureInfo.InvariantCulture),
        ["createdAt"] = job.CreatedAt.ToString("O", CultureInfo.InvariantCulture)
    };
}
