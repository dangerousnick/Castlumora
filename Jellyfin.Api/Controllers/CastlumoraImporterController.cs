using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>Administrator-owned importer prerequisites; no URL/download jobs in this increment.</summary>
[Authorize(Policy = Policies.RequiresElevation)]
[Route("Castlumora/Imports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CastlumoraImporterController : BaseJellyfinApiController
{
    private readonly YtDlpRuntime _runtime;

    /// <summary>Initializes a new instance of the <see cref="CastlumoraImporterController"/> class.</summary>
    /// <param name="runtime">Pinned, offline managed executable lifecycle.</param>
    public CastlumoraImporterController(YtDlpRuntime runtime) => _runtime = runtime;

    /// <summary>Checks the installed executable's integrity and bounded version response.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Administrator runtime state.</returns>
    [HttpGet("Runtime")]
    public Task<JsonObject> GetRuntime(CancellationToken cancellationToken) => _runtime.StatusAsync(cancellationToken);

    /// <summary>Installs only the reviewed platform asset shipped with this application.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Verified runtime state or a meaningful installation failure.</returns>
    [HttpPost("Runtime/Install")]
    [RequestSizeLimit(1024)]
    public async Task<ActionResult<JsonObject>> InstallRuntime(CancellationToken cancellationToken)
    {
        try
        {
            return await _runtime.InstallAsync(cancellationToken).ConfigureAwait(false);
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
}
