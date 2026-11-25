using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Jellyfin.Api.Castlumora;

public sealed partial class PresentationStore
{
    /// <summary>Atomically reconciles private notifications with a current visible library snapshot.</summary>
    /// <param name="userId">Authenticated viewing identity.</param>
    /// <param name="snapshot">Permission-filtered followed series and episodes.</param>
    /// <param name="enabled">Explicit opt-in or pause; resuming establishes a new baseline.</param>
    /// <param name="episodeId">Existing notification to update.</param>
    /// <param name="read">Read state or null.</param>
    /// <param name="dismiss">Permanently dismiss this notification without changing playback.</param>
    /// <returns>Private state; callers must filter rows again before returning metadata.</returns>
    public async Task<JsonObject> NotificationsAsync(Guid userId, IReadOnlyList<EpisodeNotificationSnapshot> snapshot, bool? enabled = null, Guid? episodeId = null, bool? read = null, bool dismiss = false)
    {
        Require(!userId.Equals(Guid.Empty), "A viewing identity is required.");
        Require(snapshot.Count <= 1000 && snapshot.All(s => !s.SeriesId.Equals(Guid.Empty) && s.EpisodeIds.All(id => !id.Equals(Guid.Empty))) && snapshot.Sum(s => s.EpisodeIds.Length) <= 20000, "Notification snapshot limit exceeded.");
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var filename = "notifications-" + userId.ToString("N") + ".json";
            var state = await ReadAsync(filename).ConfigureAwait(false);
            if (state.Count == 0 && !File.Exists(Path.Combine(_directory, filename)))
            {
                state = new JsonObject { ["schemaVersion"] = 1, ["enabled"] = false, ["series"] = new JsonObject(), ["rows"] = new JsonArray() };
            }

            ValidateNotifications(state);
            var before = state.ToJsonString();
            var active = state["enabled"]!.GetValue<bool>();
            var baseline = enabled == true && !active;
            state["enabled"] = enabled ?? active;
            var series = state["series"]!.AsObject();
            var rows = state["rows"]!.AsArray();
            if (state["enabled"]!.GetValue<bool>())
            {
                foreach (var show in snapshot)
                {
                    var key = show.SeriesId.ToString("N");
                    var existing = series[key] as JsonArray;
                    var seen = existing?.Select(n => Guid.Parse(n!.GetValue<string>())).ToHashSet() ?? new HashSet<Guid>();
                    foreach (var id in show.EpisodeIds.Distinct())
                    {
                        if (!seen.Add(id) || existing is null || baseline)
                        {
                            continue;
                        }

                        // No copied titles/artwork: authorization and current metadata are resolved on every read.
                        rows.Add(new JsonObject { ["id"] = id.ToString("N"), ["seriesId"] = key, ["read"] = false, ["dismissed"] = false, ["detectedAt"] = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture) });
                    }

                    series[key] = new JsonArray(seen.Select(id => (JsonNode?)JsonValue.Create(id.ToString("N"))).ToArray());
                }
            }

            while (rows.Count > 500)
            {
                rows.RemoveAt(0);
            }

            if (episodeId.HasValue)
            {
                var row = rows.SingleOrDefault(n => n!["id"]!.GetValue<string>() == episodeId.Value.ToString("N"))?.AsObject();
                if (row is null || row["dismissed"]!.GetValue<bool>())
                {
                    throw new KeyNotFoundException("Notification unavailable.");
                }

                if (read.HasValue)
                {
                    row["read"] = read.Value;
                }

                if (dismiss)
                {
                    row["dismissed"] = true;
                }
            }

            ValidateNotifications(state);
            if (state.ToJsonString() != before)
            {
                await WriteAsync(filename, state).ConfigureAwait(false);
            }

            return state;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void ValidateNotifications(JsonObject state)
    {
        if (state["schemaVersion"]?.GetValue<int>() != 1 || state["enabled"] is not JsonValue flag || !flag.TryGetValue<bool>(out _) || state["series"] is not JsonObject series || state["rows"] is not JsonArray rows || rows.Count > 500 || series.Count > 1000)
        {
            throw new InvalidDataException("Invalid notification state.");
        }

        var count = 0;
        foreach (var show in series)
        {
            if (!Guid.TryParseExact(show.Key, "N", out var seriesId) || seriesId.Equals(Guid.Empty) || show.Value is not JsonArray seen)
            {
                throw new InvalidDataException("Invalid notification series.");
            }

            count += seen.Count;
            foreach (var id in seen)
            {
                if (id is not JsonValue value || !value.TryGetValue<string>(out var text) || !Guid.TryParseExact(text, "N", out var parsed) || parsed.Equals(Guid.Empty))
                {
                    throw new InvalidDataException("Invalid notification episode.");
                }
            }
        }

        if (count > 20000)
        {
            throw new InvalidDataException("Notification observation capacity reached; preserve configuration for administrator review.");
        }

        var ids = new HashSet<Guid>();
        foreach (var row in rows)
        {
            if (row is not JsonObject item || !Guid.TryParseExact(item["id"]?.GetValue<string>(), "N", out var id) || id.Equals(Guid.Empty) || !ids.Add(id) || !Guid.TryParseExact(item["seriesId"]?.GetValue<string>(), "N", out _) || item["read"] is not JsonValue read || !read.TryGetValue<bool>(out _) || item["dismissed"] is not JsonValue dismissed || !dismissed.TryGetValue<bool>(out _) || !DateTime.TryParse(item["detectedAt"]?.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out _))
            {
                throw new InvalidDataException("Invalid notification row.");
            }
        }
    }
}
