using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Extensions;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaSegments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>Native skipper settings, profile modes and administrator-reviewed central intervals.</summary>
[Authorize]
[Route("Castlumora/Skipper")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class CastlumoraSkipperController : BaseJellyfinApiController
{
    private readonly SkipperService _skipper;
    private readonly ILibraryManager _library;
    private readonly IMediaSegmentManager _segments;

    /// <summary>Initializes a new instance of the <see cref="CastlumoraSkipperController"/> class.</summary>
    /// <param name="skipper">Built-in provider.</param>
    /// <param name="library">Existing user-aware library.</param>
    /// <param name="segments">Central cross-client intervals.</param>
    public CastlumoraSkipperController(SkipperService skipper, ILibraryManager library, IMediaSegmentManager segments)
    {
        _skipper = skipper;
        _library = library;
        _segments = segments;
    }

    /// <summary>Gets global analysis configuration and default skip mode.</summary>
    /// <returns>Global settings.</returns>
    [HttpGet("Settings")]
    public Task<JsonObject> GetSettings() => _skipper.SettingsAsync();

    /// <summary>Saves administrator-only analysis settings.</summary>
    /// <param name="value">Global settings.</param>
    /// <returns>Validated settings or an error.</returns>
    [HttpPut("Settings")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult<JsonObject>> SaveSettings([FromBody] JsonObject value)
    {
        try
        {
            return await _skipper.SaveSettingsAsync(value).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return BadRequest("Invalid skipper settings.");
        }
    }

    /// <summary>Gets only the authenticated profile's preference.</summary>
    /// <returns>Profile preference.</returns>
    [HttpGet("Profile")]
    public Task<JsonObject> GetProfile() => _skipper.SettingsAsync(User.GetUserId());

    /// <summary>Saves only the authenticated profile's preference.</summary>
    /// <param name="value">Skip preference.</param>
    /// <returns>Validated preference.</returns>
    [HttpPut("Profile")]
    public async Task<ActionResult<JsonObject>> SaveProfile([FromBody] JsonObject value)
    {
        if (User.GetUserId().Equals(Guid.Empty))
        {
            return Unauthorized();
        }

        try
        {
            return await _skipper.SaveSettingsAsync(value, User.GetUserId()).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return BadRequest("Invalid skip mode.");
        }
    }

    /// <summary>Gets review state for an administrator-accessible episode.</summary>
    /// <param name="id">Existing episode.</param>
    /// <returns>Revisioned intervals.</returns>
    [HttpGet("Items/{id:guid}")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult<JsonObject>> GetItem(Guid id)
    {
        var item = _library.GetItemById<BaseItem>(id, User.GetUserId());
        return item is null ? NotFound() : await _skipper.ItemAsync(id).ConfigureAwait(false);
    }

    /// <summary>Analyzes local repeated audio without approving or skipping content.</summary>
    /// <param name="id">Target episode.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <returns>Reviewable suggestions.</returns>
    [HttpPost("Items/{id:guid}/Analyze")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult<JsonObject>> Analyze(Guid id, CancellationToken cancellationToken)
    {
        var item = _library.GetItemById<BaseItem>(id, User.GetUserId());
        if (item is null)
        {
            return NotFound();
        }

        try
        {
            return await _skipper.AnalyzeAsync(item, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    /// <summary>Corrects/approves intervals and refreshes existing enabled segment providers without global deletion.</summary>
    /// <param name="id">Target episode.</param>
    /// <param name="value">Revision and intervals.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    /// <returns>Saved state or a revision conflict.</returns>
    [HttpPut("Items/{id:guid}")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult<JsonObject>> SaveItem(Guid id, [FromBody] JsonObject value, CancellationToken cancellationToken)
    {
        var item = _library.GetItemById<BaseItem>(id, User.GetUserId());
        if (item is null)
        {
            return NotFound();
        }

        if (!await _skipper.Supports(item).ConfigureAwait(false))
        {
            return BadRequest("A local episode is required.");
        }

        try
        {
            var result = await _skipper.SaveItemAsync(item, value).ConfigureAwait(false);
            await _segments.RunSegmentPluginProviders(item, _library.GetLibraryOptions(item), false, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception exception) when (exception is ArgumentException or NullReferenceException or FormatException)
        {
            return BadRequest("Invalid skipper intervals.");
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }
}
