using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Extensions;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>Private on-refresh episode notifications for followed series.</summary>
[Authorize]
[Route("Castlumora/Notifications")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CastlumoraNotificationsController : BaseJellyfinApiController
{
    private readonly PresentationStore _store;
    private readonly IUserManager _users;
    private readonly ILibraryManager _library;

    /// <summary>Initializes a new instance of the <see cref="CastlumoraNotificationsController"/> class.</summary>
    /// <param name="store">Private atomic state.</param>
    /// <param name="users">Authenticated identities.</param>
    /// <param name="library">Native permission-aware library.</param>
    public CastlumoraNotificationsController(PresentationStore store, IUserManager users, ILibraryManager library)
    {
        _store = store;
        _users = users;
        _library = library;
    }

    /// <summary>Refreshes notifications, resolving only currently permitted metadata.</summary>
    /// <returns>Settings and up to500 current private notifications.</returns>
    [HttpGet]
    public Task<ActionResult<JsonObject>> List() => Execute();

    /// <summary>Opts in or pauses; enabling baselines existing episodes without historical alerts.</summary>
    /// <param name="value">Only the enabled boolean.</param>
    /// <returns>Updated settings and visible notifications.</returns>
    [HttpPut("Settings")]
    [RequestSizeLimit(1024)]
    public Task<ActionResult<JsonObject>> Settings([FromBody] JsonObject value)
    {
        if (value.Count != 1 || value["enabled"] is not JsonValue flag || !flag.TryGetValue<bool>(out var enabled))
        {
            return Task.FromResult<ActionResult<JsonObject>>(BadRequest("Supply only enabled as a boolean."));
        }

        return Execute(enabled);
    }

    /// <summary>Marks an existing visible private notification read/unread or dismisses it.</summary>
    /// <param name="episodeId">Notification's native episode ID.</param>
    /// <param name="value">Exactly one read boolean or dismissed true.</param>
    /// <returns>Updated visible notifications.</returns>
    [HttpPut("{episodeId:guid}")]
    [RequestSizeLimit(1024)]
    public Task<ActionResult<JsonObject>> Update(Guid episodeId, [FromBody] JsonObject value)
    {
        if (value.Count != 1)
        {
            return Task.FromResult<ActionResult<JsonObject>>(BadRequest("Supply only read or dismissed."));
        }

        if (value["read"] is JsonValue read && read.TryGetValue<bool>(out var isRead))
        {
            return Execute(episodeId: episodeId, read: isRead);
        }

        if (value["dismissed"] is JsonValue dismissed && dismissed.TryGetValue<bool>(out var dismiss) && dismiss)
        {
            return Execute(episodeId: episodeId, dismiss: true);
        }

        return Task.FromResult<ActionResult<JsonObject>>(BadRequest("Supply a read boolean or dismissed true."));
    }

    private async Task<ActionResult<JsonObject>> Execute(bool? enabled = null, Guid? episodeId = null, bool? read = null, bool dismiss = false)
    {
        if (User.GetIsApiKey() || User.GetUserId().Equals(Guid.Empty))
        {
            return Forbid();
        }

        var user = _users.GetUserById(User.GetUserId());
        if (user is null)
        {
            return Forbid();
        }

        try
        {
            var preferences = await _store.FollowingAsync(user.Id).ConfigureAwait(false);
            var follows = preferences["followedSeries"]!.AsArray().Select(n => Guid.Parse(n!.GetValue<string>())).Distinct().ToArray();
            var shows = follows.Select(id => _library.GetItemById<Series>(id, user)).Where(s => s is not null).ToDictionary(s => s!.Id, s => s!);
            var current = await _store.NotificationsAsync(user.Id, []).ConfigureAwait(false);
            var observing = enabled ?? current["enabled"]!.GetValue<bool>();
            var episodes = new Dictionary<Guid, Episode>();
            if (!observing)
            {
                // Paused users can still read/dismiss retained alerts without scanning the catalog.
                foreach (var row in current["rows"]!.AsArray())
                {
                    var item = _library.GetItemById<Episode>(Guid.Parse(row!["id"]!.GetValue<string>()), user);
                    if (item is not null && shows.ContainsKey(item.SeriesId))
                    {
                        episodes[item.Id] = item;
                    }
                }
            }
            else if (shows.Count > 0)
            {
                var query = new InternalItemsQuery(user)
                {
                    Recursive = true,
                    IncludeItemTypes = [BaseItemKind.Episode],
                    Limit = 20001,
                    EnableTotalRecordCount = false,
                    GroupByPresentationUniqueKey = false,
                    IsVirtualItem = false
                };
                // Establish user roots before explicit ancestors; never use an unscoped all-media query.
                _library.ConfigureUserAccess(query, user);
                query.AncestorIds = shows.Keys.ToArray();
                var candidates = _library.GetItemList(query);
                if (candidates.Count > 20000)
                {
                    throw new InvalidDataException("Notification snapshot capacity exceeded.");
                }

                foreach (var item in candidates.OfType<Episode>())
                {
                    if (shows.ContainsKey(item.SeriesId) && _library.GetItemById<Episode>(item.Id, user) is not null)
                    {
                        episodes[item.Id] = item;
                    }
                }
            }

            if (episodeId.HasValue && !episodes.ContainsKey(episodeId.Value))
            {
                return NotFound();
            }

            var snapshot = shows.Keys.Select(id => new EpisodeNotificationSnapshot(id, episodes.Values.Where(e => e.SeriesId.Equals(id)).Select(e => e.Id).ToArray())).ToArray();
            var state = await _store.NotificationsAsync(user.Id, snapshot, enabled, episodeId, read, dismiss).ConfigureAwait(false);
            var rows = new JsonArray();
            foreach (var row in state["rows"]!.AsArray().Reverse())
            {
                if (row!["dismissed"]!.GetValue<bool>() || !episodes.TryGetValue(Guid.Parse(row["id"]!.GetValue<string>()), out var episode) || !shows.TryGetValue(episode.SeriesId, out var series))
                {
                    continue;
                }

                rows.Add(new JsonObject
                {
                    ["id"] = episode.Id.ToString("N"),
                    ["seriesId"] = series.Id.ToString("N"),
                    ["title"] = episode.Name,
                    ["seriesName"] = series.Name,
                    ["season"] = episode.ParentIndexNumber,
                    ["episode"] = episode.IndexNumber,
                    ["read"] = row["read"]!.GetValue<bool>(),
                    ["detectedAt"] = row["detectedAt"]!.GetValue<string>()
                });
            }

            return new JsonObject { ["enabled"] = state["enabled"]!.GetValue<bool>(), ["items"] = rows, ["unread"] = rows.Count(r => !r!["read"]!.GetValue<bool>()) };
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or InvalidOperationException or FormatException or ArgumentException)
        {
            return StatusCode(503, "Notifications are unavailable or their observation capacity was reached. Preserve configuration for administrator review; no successful update is claimed.");
        }
    }
}
