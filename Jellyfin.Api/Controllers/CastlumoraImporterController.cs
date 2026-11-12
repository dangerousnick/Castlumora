using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Extensions;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>Administrator-owned runtime and authorized persistent direct-video imports.</summary>
[Authorize(Policy = Policies.RequiresElevation)]
[Route("Castlumora/Imports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CastlumoraImporterController : BaseJellyfinApiController
{
    private readonly YtDlpRuntime _runtime;
    private readonly ImportService _imports;

    /// <summary>Initializes a new instance of the <see cref="CastlumoraImporterController"/> class.</summary>
    /// <param name="runtime">Pinned, offline managed executable lifecycle.</param>
    /// <param name="imports">Durable administrator import lifecycle.</param>
    public CastlumoraImporterController(YtDlpRuntime runtime, ImportService imports)
    {
        _runtime = runtime;
        _imports = imports;
    }

    /// <summary>Checks the installed executable's integrity and bounded version response.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Administrator runtime state.</returns>
    [HttpGet("Runtime")]
    public async Task<JsonObject> GetRuntime(CancellationToken cancellationToken)
    {
        var state = await _runtime.StatusAsync(cancellationToken).ConfigureAwait(false);
        state["downloadJobsAvailable"] = _imports.Available && state["ready"]!.GetValue<bool>();
        return state;
    }

    /// <summary>Lists administrator-only import history and eligible library IDs.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Safe queue state without source query credentials.</returns>
    [HttpGet("Queue")]
    public Task<JsonObject> GetQueue(CancellationToken cancellationToken) => _imports.StateAsync(User.GetUserId(), cancellationToken);

    /// <summary>Submits one authorized direct-video import.</summary>
    /// <param name="value">URL, title, rights confirmation and destination ID.</param>
    /// <param name="cancellationToken">Submission cancellation.</param>
    /// <returns>A persistent job or a meaningful policy rejection.</returns>
    [HttpPost("Jobs")]
    [RequestSizeLimit(32768)]
    public Task<ActionResult<JsonObject>> SubmitImport([FromBody] JsonObject value, CancellationToken cancellationToken) => QueueAction(() => _imports.SubmitAsync(value, User.GetUserId(), cancellationToken));

    /// <summary>Cancels only the selected import and its owned process.</summary>
    /// <param name="id">Persistent job ID.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Persisted cancellation state.</returns>
    [HttpPost("Jobs/{id:guid}/Cancel")]
    public Task<ActionResult<JsonObject>> CancelImport([FromRoute] Guid id, CancellationToken cancellationToken) => QueueAction(() => _imports.ChangeAsync(id, false, cancellationToken));

    /// <summary>Retries a failed/cancelled import within its attempt limit.</summary>
    /// <param name="id">Persistent job ID.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Persisted retry state.</returns>
    [HttpPost("Jobs/{id:guid}/Retry")]
    public Task<ActionResult<JsonObject>> RetryImport([FromRoute] Guid id, CancellationToken cancellationToken) => QueueAction(() => _imports.ChangeAsync(id, true, cancellationToken));

    /// <summary>Installs only the reviewed platform asset shipped with this application.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Verified runtime state or a meaningful installation failure.</returns>
    [HttpPost("Runtime/Install")]
    [RequestSizeLimit(1024)]
    public async Task<ActionResult<JsonObject>> InstallRuntime(CancellationToken cancellationToken)
    {
        try
        {
            var state = await _runtime.InstallAsync(cancellationToken).ConfigureAwait(false);
            state["downloadJobsAvailable"] = _imports.Available && state["ready"]!.GetValue<bool>();
            return state;
        }
        catch (YtDlpRuntime.BusyException)
        {
            return Conflict("The managed runtime is in use by an import. Wait for active imports to finish before installing it.");
        }
        catch (NotSupportedException)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, "Managed yt-dlp is available for Linux x64, macOS x64/ARM64 and Windows x64 in this preview.");
        }
        catch (InvalidDataException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "The bundled runtime could not be installed or verified. Use a complete preview package, check executable filesystem permissions and OS runtime dependencies, then retry. The existing executable was retained.");
        }
    }
    private async Task<ActionResult<JsonObject>> QueueAction(Func<Task<JsonObject>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Import job not found.");
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        catch (IOException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "The import state could not be saved. Check storage permissions/capacity and queue diagnostics; preserve existing job files.");
        }
    }

}
