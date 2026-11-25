using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Api.Controllers;

/// <summary>Harmless media settings scoped exclusively to the authenticated viewing identity.</summary>
[Authorize]
[Route("Castlumora/Profiles/MediaPreferences")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CastlumoraMediaPreferencesController : BaseJellyfinApiController
{
    private readonly MediaPreferencesService _preferences;

    /// <summary>Initializes a new instance of the <see cref="CastlumoraMediaPreferencesController"/> class.</summary>
    /// <param name="preferences">Whitelisted, identity-scoped configuration updates.</param>
    public CastlumoraMediaPreferencesController(MediaPreferencesService preferences) => _preferences = preferences;

    /// <summary>Reads only this viewing identity's media defaults.</summary>
    /// <param name="token">Request cancellation.</param>
    /// <returns>Defaults and edit availability.</returns>
    [HttpGet]
    public Task<ActionResult<JsonObject>> Get(CancellationToken token) => ReadOrUpdate(null, token);

    /// <summary>Updates only whitelisted media defaults, never identity or policy.</summary>
    /// <param name="value">Language, source audio and subtitle-mode settings.</param>
    /// <param name="token">Request cancellation.</param>
    /// <returns>Saved defaults.</returns>
    [HttpPut]
    [RequestSizeLimit(8192)]
    public Task<ActionResult<JsonObject>> Put([FromBody] JsonObject value, CancellationToken token) => ReadOrUpdate(value, token);

    private async Task<ActionResult<JsonObject>> ReadOrUpdate(JsonObject? value, CancellationToken token)
    {
        try
        {
            return await _preferences.ReadOrUpdateAsync(User.GetUserId(), value, token).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidDataException or NullReferenceException)
        {
            return BadRequest("Choose valid media language/source-audio/subtitle defaults; identity and permissions cannot be changed here.");
        }
        catch (Exception exception) when (exception is IOException or DbUpdateException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Media preferences could not be saved. Check server storage and retry.");
        }
    }
}
