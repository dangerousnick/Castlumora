using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Extensions;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>Private, permission-checked followed series for the current viewing identity.</summary>
[Authorize]
[Route("Castlumora/Following")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CastlumoraFollowingController : BaseJellyfinApiController
{
    private readonly PresentationStore _store;
    private readonly IUserManager _users;
    private readonly ILibraryManager _library;

    /// <summary>Initializes a new instance of the <see cref="CastlumoraFollowingController"/> class.</summary>
    /// <param name="store">Existing private preference persistence.</param>
    /// <param name="users">Inherited user identities.</param>
    /// <param name="library">Inherited type and visibility checks.</param>
    public CastlumoraFollowingController(PresentationStore store, IUserManager users, ILibraryManager library)
    {
        _store = store;
        _users = users;
        _library = library;
    }

    /// <summary>Lists currently visible followed series without leaking unavailable titles.</summary>
    /// <param name="startIndex">Nonnegative page offset.</param>
    /// <param name="limit">Bounded page size.</param>
    /// <returns>Visible IDs and total for this viewing identity only.</returns>
    [HttpGet]
    public Task<ActionResult<JsonObject>> List([FromQuery, Range(0, int.MaxValue)] int startIndex = 0, [FromQuery, Range(1, 100)] int limit = 48)
        => Owned(async () =>
        {
            var user = _users.GetUserById(User.GetUserId());
            if (user is null)
            {
                return Forbid();
            }

            var ids = ReadIds(await _store.FollowingAsync(user.Id).ConfigureAwait(false));
            var visible = ids.Where(id => _library.GetItemById<Series>(id, user) is not null).ToArray();
            return new JsonObject
            {
                ["seriesIds"] = new JsonArray(visible.Skip(startIndex).Take(limit).Select(id => (JsonNode?)JsonValue.Create(id.ToString("N"))).ToArray()),
                ["total"] = visible.Length
            };
        });

    /// <summary>Gets follow state only for a visible series.</summary>
    /// <param name="seriesId">Series identifier.</param>
    /// <returns>Membership without exposing other identities.</returns>
    [HttpGet("{seriesId:guid}")]
    public Task<ActionResult<JsonObject>> Status(Guid seriesId)
        => Owned(async () =>
        {
            var user = _users.GetUserById(User.GetUserId());
            if (user is null || _library.GetItemById<Series>(seriesId, user) is null)
            {
                return NotFound();
            }

            return new JsonObject { ["followed"] = ReadIds(await _store.FollowingAsync(user.Id).ConfigureAwait(false)).Contains(seriesId) };
        });

    /// <summary>Follows a visible series or removes an owned reference even after access changes.</summary>
    /// <param name="seriesId">Series identifier, never an identity override.</param>
    /// <param name="value">Only the enabled boolean.</param>
    /// <returns>The requested membership after durable persistence.</returns>
    [HttpPut("{seriesId:guid}")]
    [RequestSizeLimit(1024)]
    public Task<ActionResult<JsonObject>> Set(Guid seriesId, [FromBody] JsonObject value)
        => Owned(async () =>
        {
            if (seriesId.Equals(Guid.Empty) || value.Count != 1 || value["enabled"] is not JsonValue node || !node.TryGetValue<bool>(out var enabled))
            {
                return BadRequest("Supply only the enabled boolean and a valid series ID.");
            }

            var user = _users.GetUserById(User.GetUserId());
            if (user is null)
            {
                return Forbid();
            }

            if (enabled && _library.GetItemById<Series>(seriesId, user) is null)
            {
                return NotFound();
            }

            await _store.FollowingAsync(user.Id, seriesId, enabled).ConfigureAwait(false);
            return new JsonObject { ["followed"] = enabled };
        });

    /// <summary>Clears this identity's following, including unavailable references.</summary>
    /// <param name="value">An empty confirmation object.</param>
    /// <returns>No content after durable persistence.</returns>
    [HttpPost("Clear")]
    [RequestSizeLimit(1024)]
    public Task<ActionResult<JsonObject>> Clear([FromBody] JsonObject value)
        => Owned(async () =>
        {
            if (value.Count != 0)
            {
                return BadRequest("Clear accepts no identity or preference overrides.");
            }

            await _store.FollowingAsync(User.GetUserId(), clear: true).ConfigureAwait(false);
            return NoContent();
        });

    private async Task<ActionResult<JsonObject>> Owned(Func<Task<ActionResult<JsonObject>>> operation)
    {
        if (User.GetIsApiKey() || User.GetUserId().Equals(Guid.Empty) || _users.GetUserById(User.GetUserId()) is null)
        {
            return Forbid();
        }

        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            return BadRequest("Following limit reached or invalid preference request.");
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or InvalidOperationException)
        {
            return StatusCode(503, "Following preferences are unavailable. Preserve configuration and retry after repair; no successful change is claimed.");
        }
    }

    private static Guid[] ReadIds(JsonObject state)
    {
        var rows = state["followedSeries"]?.AsArray() ?? throw new InvalidDataException();
        if (rows.Count > 1000)
        {
            throw new InvalidDataException();
        }

        var ids = new List<Guid>();
        foreach (var row in rows)
        {
            if (row is not JsonValue value || !value.TryGetValue<string>(out var text) || !Guid.TryParse(text, out var id) || id.Equals(Guid.Empty))
            {
                throw new InvalidDataException();
            }

            ids.Add(id);
        }

        return ids.Distinct().ToArray();
    }
}
