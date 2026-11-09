using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Api.Castlumora;

/// <summary>Versioned, server-owned Castlumora presentation and user preferences.</summary>
public sealed class PresentationStore : IDisposable
{
    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="PresentationStore"/> class.</summary>
    /// <param name="paths">Server paths.</param>
    public PresentationStore(IApplicationPaths paths)
    {
        _directory = Path.Combine(paths.ConfigurationDirectoryPath, "castlumora");
    }

    /// <summary>Creates a safe default shared by every client.</summary>
    /// <returns>A new default configuration.</returns>
    public static JsonObject Defaults() => JsonNode.Parse("""
        {"schemaVersion":1,"revision":0,"theme":{"accent":"#ad70ff","background":"#0d0b14"},
         "navigation":[{"id":"home","label":"Home"},{"id":"browse","label":"Browse"},{"id":"list","label":"My List"},{"id":"search","label":"Search"}],
         "featured":[],"rows":[{"id":"resume","title":"Continue Watching","kind":"resume"},{"id":"next","title":"Up Next","kind":"next"},{"id":"recent","title":"Fresh arrivals","kind":"recent"},{"id":"movies","title":"Movie night starts here","kind":"movies"},{"id":"series","title":"Your next obsession","kind":"series"}],
         "categories":[],"devices":{}}
        """)!.AsObject();

    /// <summary>Validates schema v1 without accepting executable HTML, CSS or scripts.</summary>
    /// <param name="config">Configuration.</param>
    public static void Validate(JsonObject config)
    {
        Require(config["revision"]?.GetValue<int>() is >= 0, "Invalid revision.");
        Require(config["schemaVersion"]?.GetValue<int>() == 1, "Only schemaVersion 1 is supported.");
        Require(config.ToJsonString().Length <= 131072, "Configuration is too large.");
        var theme = config["theme"]!.AsObject();
        foreach (var key in new[] { "accent", "background" })
        {
            Require(Regex.IsMatch(theme[key]!.GetValue<string>(), "^#[0-9a-fA-F]{6}$"), "Theme colors must be six digit hex colors.");
        }

        var navigation = Array(config, "navigation", 4);
        var rows = Array(config, "rows", 40);
        var categories = Array(config, "categories", 100);
        var featured = Array(config, "featured", 20);
        foreach (var collection in new[] { navigation, rows, categories })
        {
            var ids = collection.Select(n => n!["id"]!.GetValue<string>()).ToArray();
            Require(ids.Distinct(StringComparer.Ordinal).Count() == ids.Length, "Duplicate IDs are not allowed.");
            foreach (var entry in collection)
            {
                Require(Regex.IsMatch(entry!["id"]!.GetValue<string>(), "^[a-zA-Z0-9_-]{1,64}$"), "Invalid ID.");
                ValidateUsers(entry.AsObject());
            }
        }

        foreach (var nav in navigation)
        {
            Require(new[] { "home", "browse", "list", "search" }.Contains(nav!["id"]!.GetValue<string>(), StringComparer.Ordinal), "Unknown navigation item.");
            Text(nav, "label");
        }

        foreach (var row in rows)
        {
            Text(row!, "title");
            Require(new[] { "resume", "next", "recent", "movies", "series", "category" }.Contains(row!["kind"]!.GetValue<string>(), StringComparer.Ordinal), "Unknown row kind.");
            if (row["kind"]!.GetValue<string>() == "category")
            {
                Require(categories.Any(c => c!["id"]!.GetValue<string>() == row["categoryId"]?.GetValue<string>()), "Unknown category reference.");
            }
        }

        foreach (var category in categories)
        {
            Text(category!, "title");
            if (category!["color"] is not null)
            {
                Require(Regex.IsMatch(category["color"]!.GetValue<string>(), "^#[0-9a-fA-F]{6}$"), "Invalid category color.");
            }

            if (category["icon"] is not null)
            {
                Text(category, "icon");
            }

            if (category["artworkItemId"] is not null)
            {
                Require(Guid.TryParse(category["artworkItemId"]!.GetValue<string>(), out _), "Invalid artwork item ID.");
            }

            var rules = category["rules"]!.AsObject();
            if (rules["excludeIds"] is not null)
            {
                foreach (var value in Array(rules, "excludeIds", 200))
                {
                    Require(Guid.TryParse(value!.GetValue<string>(), out _), "Invalid excluded item ID.");
                }
            }

            if (rules["contentTypes"] is not null)
            {
                foreach (var value in Array(rules, "contentTypes", 3))
                {
                    Require(new[] { "Movie", "Series", "Episode" }.Contains(value!.GetValue<string>(), StringComparer.Ordinal), "Unsupported category content type.");
                }
            }

            foreach (var key in new[] { "itemIds", "folderIds", "genres", "studios" })
            {
                foreach (var value in Array(rules, key, 200))
                {
                    var text = value!.GetValue<string>();
                    Require(text.Length is > 0 and <= 128, "Invalid category rule.");
                    if (key.EndsWith("Ids", StringComparison.Ordinal))
                    {
                        Require(Guid.TryParse(text, out _), "Invalid media ID.");
                    }
                }
            }
        }

        foreach (var banner in featured)
        {
            Require(Guid.TryParse(banner!["itemId"]!.GetValue<string>(), out _), "Invalid featured item ID.");
            ValidateUsers(banner.AsObject());
        }

        foreach (var device in config["devices"]!.AsObject())
        {
            Require(new[] { "desktop", "tablet", "mobile", "tv" }.Contains(device.Key, StringComparer.Ordinal), "Unknown device class.");
            var settings = device.Value!.AsObject();
            if (settings["safeZone"] is not null)
            {
                Require(settings["safeZone"]!.GetValue<double>() is >= 0 and <= 10, "Safe zone must be 0–10 percent.");
            }

            if (settings["posterWidth"] is not null)
            {
                Require(settings["posterWidth"]!.GetValue<int>() is >= 100 and <= 320, "Poster width must be 100–320.");
            }

            if (settings["rowIds"] is not null)
            {
                var ids = Array(settings, "rowIds", 40);
                Require(ids.Select(n => n!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count() == ids.Count, "Duplicate device row IDs.");
                foreach (var id in ids)
                {
                    Require(rows.Any(r => r!["id"]!.GetValue<string>() == id!.GetValue<string>()), "Unknown device row ID.");
                }
            }
        }
    }

    /// <summary>Filters presentation visibility; media authorization remains with the existing media API.</summary>
    /// <param name="config">Published configuration.</param>
    /// <param name="userId">Authenticated profile.</param>
    /// <returns>A detached visible configuration.</returns>
    public static JsonObject ForUser(JsonObject config, Guid userId)
    {
        var visible = (JsonObject)config.DeepClone();
        foreach (var key in new[] { "navigation", "rows", "categories", "featured" })
        {
            var entries = visible[key]!.AsArray();
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var users = entries[i]!["userIds"]?.AsArray();
                if (users is { Count: > 0 } && !users.Any(u => Guid.Parse(u!.GetValue<string>()).Equals(userId)))
                {
                    entries.RemoveAt(i);
                }
                else
                {
                    entries[i]!.AsObject().Remove("userIds");
                }
            }
        }

        var rows = visible["rows"]!.AsArray();
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i]!["kind"]!.GetValue<string>() == "category" && !visible["categories"]!.AsArray().Any(c => c!["id"]!.GetValue<string>() == rows[i]!["categoryId"]!.GetValue<string>()))
            {
                rows.RemoveAt(i);
            }
        }

        foreach (var device in visible["devices"]!.AsObject())
        {
            var ids = device.Value!["rowIds"]?.AsArray();
            if (ids is null)
            {
                continue;
            }

            for (var i = ids.Count - 1; i >= 0; i--)
            {
                if (!rows.Any(r => r!["id"]!.GetValue<string>() == ids[i]!.GetValue<string>()))
                {
                    ids.RemoveAt(i);
                }
            }
        }

        return visible;
    }

    /// <summary>Reads either the published layout or the administrator draft.</summary>
    /// <param name="draft">Whether to read the draft.</param>
    /// <returns>The layout.</returns>
    public async Task<JsonObject> ReadLayoutAsync(bool draft)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var document = await ReadAsync("layout.json").ConfigureAwait(false);
            return (JsonObject)(document[draft ? "draft" : "published"]?.DeepClone() ?? Defaults());
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Saves a validated draft or publishes it using an optimistic revision check.</summary>
    /// <param name="config">Draft with its current revision.</param>
    /// <param name="publish">Publish the saved draft.</param>
    /// <returns>The updated layout.</returns>
    public async Task<JsonObject> SaveLayoutAsync(JsonObject config, bool publish)
    {
        Validate(config);
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var document = await ReadAsync("layout.json").ConfigureAwait(false);
            var current = document["draft"]?.AsObject() ?? Defaults();
            if (config["revision"]?.GetValue<int>() != current["revision"]!.GetValue<int>())
            {
                throw new InvalidOperationException("The draft changed. Reload it before saving.");
            }

            var updated = (JsonObject)(publish ? current : config).DeepClone();
            updated["revision"] = checked(current["revision"]!.GetValue<int>() + 1);
            document["draft"] = updated.DeepClone();
            if (publish)
            {
                document["published"] = updated.DeepClone();
            }

            await WriteAsync("layout.json", document).ConfigureAwait(false);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Reads or updates a watchlist/resume preference for the authenticated user only.</summary>
    /// <param name="userId">Authenticated user.</param>
    /// <param name="kind">watchlist or hiddenResume.</param>
    /// <param name="itemId">Item or series ID, or null to read.</param>
    /// <param name="enabled">Whether membership is enabled.</param>
    /// <returns>Both preference lists.</returns>
    public async Task<JsonObject> PreferencesAsync(Guid userId, string kind, Guid? itemId, bool enabled)
    {
        Require(kind is "watchlist" or "hiddenResume", "Unknown preference.");
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var filename = "user-" + userId.ToString("N") + ".json";
            var state = await ReadAsync(filename).ConfigureAwait(false);
            state["watchlist"] ??= new JsonArray();
            state["hiddenResume"] ??= new JsonArray();
            if (itemId.HasValue)
            {
                var list = state[kind]!.AsArray();
                var id = itemId.Value.ToString("N");
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i]!.GetValue<string>() == id)
                    {
                        list.RemoveAt(i);
                    }
                }

                if (enabled)
                {
                    Require(list.Count < 1000, "Preference list limit reached.");
                    list.Add(id);
                }

                await WriteAsync(filename, state).ConfigureAwait(false);
            }

            return state;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message);
        }
    }

    private static JsonArray Array(JsonObject value, string key, int limit)
    {
        var result = value[key]!.AsArray();
        Require(result.Count <= limit, "Too many " + key + ".");
        return result;
    }

    private static void Text(JsonNode value, string key) => Require(value[key]!.GetValue<string>().Length is > 0 and <= 120, "Invalid " + key + ".");

    private static void ValidateUsers(JsonObject entry)
    {
        if (entry["userIds"] is null)
        {
            return;
        }

        foreach (var user in Array(entry, "userIds", 200))
        {
            Require(Guid.TryParse(user!.GetValue<string>(), out _), "Invalid profile ID.");
        }
    }

    private async Task<JsonObject> ReadAsync(string filename)
    {
        var path = Path.Combine(_directory, filename);
        if (!File.Exists(path))
        {
            return new JsonObject();
        }

        var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        return JsonNode.Parse(json)!.AsObject();
    }

    private async Task WriteAsync(string filename, JsonObject value)
    {
        Directory.CreateDirectory(_directory);
        var destination = Path.Combine(_directory, filename);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, value.ToJsonString(new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
            File.Move(temporary, destination, true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
