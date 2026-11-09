using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Extensions;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>Castlumora's additive, versioned presentation API. Existing media contracts are unchanged.</summary>
[Authorize]
[Route("Castlumora/Presentation")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class CastlumoraPresentationController : BaseJellyfinApiController
{
    private readonly PresentationStore _store;

    /// <summary>Initializes a new instance of the <see cref="CastlumoraPresentationController"/> class.</summary>
    /// <param name="store">Server-owned persistence.</param>
    public CastlumoraPresentationController(PresentationStore store) => _store = store;

    /// <summary>Gets the published presentation filtered for the authenticated profile.</summary>
    /// <param name="schemaVersion">Requested major schema version.</param>
    /// <param name="components">Optional comma-separated supported row kinds.</param>
    /// <returns>The visible configuration.</returns>
    [HttpGet]
    public async Task<ActionResult<JsonObject>> GetLayout([FromQuery] int schemaVersion = 1, [FromQuery] string? components = null)
    {
        if (schemaVersion != 1)
        {
            return StatusCode(406, "Supported presentation schema versions: 1.");
        }

        Response.Headers["X-Castlumora-Presentation-Version"] = "1";
        var layout = PresentationStore.ForUser(await _store.ReadLayoutAsync(false).ConfigureAwait(false), User.GetUserId());
        if (!string.IsNullOrEmpty(components))
        {
            var supported = components.Split(',');
            var rows = layout["rows"]!.AsArray();
            for (var index = rows.Count - 1; index >= 0; index--)
            {
                if (!supported.Contains(rows[index]!["kind"]!.GetValue<string>(), StringComparer.Ordinal))
                {
                    rows.RemoveAt(index);
                }
            }
        }

        return layout;
    }

    /// <summary>Gets the full administrator draft.</summary>
    /// <returns>The draft.</returns>
    [HttpGet("Draft")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult<JsonObject>> GetDraft() => await _store.ReadLayoutAsync(true).ConfigureAwait(false);

    /// <summary>Saves a validated draft without publishing it.</summary>
    /// <param name="config">Schema v1 configuration with the current revision.</param>
    /// <returns>The saved draft or a validation/conflict response.</returns>
    [HttpPut("Draft")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [RequestSizeLimit(262144)]
    public Task<ActionResult<JsonObject>> SaveDraft([FromBody] JsonObject config) => SaveAsync(config, false);

    /// <summary>Publishes the saved draft at the supplied revision.</summary>
    /// <param name="config">Current saved draft; unsaved client edits are not published.</param>
    /// <returns>The published configuration.</returns>
    [HttpPost("Publish")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [RequestSizeLimit(262144)]
    public Task<ActionResult<JsonObject>> Publish([FromBody] JsonObject config) => SaveAsync(config, true);

    /// <summary>Lists published presentation snapshots.</summary>
    /// <returns>Revision metadata.</returns>
    [HttpGet("History")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult<JsonArray>> GetHistory() => await _store.HistoryAsync().ConfigureAwait(false);

    /// <summary>Restores a historical publication to the draft for review.</summary>
    /// <param name="revision">Historical revision.</param>
    /// <param name="expectedRevision">Current draft revision.</param>
    /// <returns>The restored draft.</returns>
    [HttpPost("History/{revision:int}/Restore")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult<JsonObject>> Restore(int revision, [FromQuery] int expectedRevision)
    {
        try
        {
            return await _store.RestoreAsync(revision, expectedRevision).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        catch (System.Collections.Generic.KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Gets this user's independent watchlist and resume exclusions.</summary>
    /// <returns>User preferences.</returns>
    [HttpGet("Preferences")]
    public async Task<ActionResult<JsonObject>> GetPreferences()
    {
        if (User.GetUserId().Equals(Guid.Empty))
        {
            return Unauthorized("A viewing user session is required.");
        }

        return await _store.PreferencesAsync(User.GetUserId(), "watchlist", null, false).ConfigureAwait(false);
    }

    /// <summary>Changes one membership without touching media or playback history.</summary>
    /// <param name="kind">watchlist or hiddenResume.</param>
    /// <param name="itemId">Item or series identifier.</param>
    /// <param name="enabled">Whether to enable membership.</param>
    /// <returns>The user's updated preferences.</returns>
    [HttpPut("Preferences/{kind}/{itemId:guid}")]
    public async Task<ActionResult<JsonObject>> SetPreference(string kind, Guid itemId, [FromQuery] bool enabled)
    {
        if (User.GetUserId().Equals(Guid.Empty))
        {
            return Unauthorized("A viewing user session is required.");
        }

        try
        {
            return await _store.PreferencesAsync(User.GetUserId(), kind, itemId, enabled).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    private async Task<ActionResult<JsonObject>> SaveAsync(JsonObject config, bool publish)
    {
        try
        {
            return await _store.SaveLayoutAsync(config, publish).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message.StartsWith("The draft changed.", StringComparison.Ordinal)
                ? Conflict(exception.Message)
                : BadRequest("Invalid presentation configuration.");
        }
        catch (Exception exception) when (exception is ArgumentException or NullReferenceException or FormatException or System.Text.Json.JsonException)
        {
            return BadRequest("Invalid presentation configuration: " + exception.Message);
        }
    }
}
