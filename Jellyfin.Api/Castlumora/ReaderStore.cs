using System;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Api.Castlumora;

/// <summary>Central per-profile reading positions and reader preferences without database changes.</summary>
public sealed class ReaderStore : IDisposable
{
    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="ReaderStore"/> class.</summary>
    /// <param name="paths">Existing server paths.</param>
    public ReaderStore(IApplicationPaths paths) => _directory = Path.Combine(paths.ConfigurationDirectoryPath, "castlumora", "reader");

    /// <summary>Reads or saves one profile/book position.</summary>
    /// <param name="userId">Authenticated profile ID.</param>
    /// <param name="itemId">Permission-checked book ID.</param>
    /// <param name="value">Null to read, otherwise format/location/preferences.</param>
    /// <returns>Stored position and preferences.</returns>
    public async Task<JsonObject> StateAsync(Guid userId, Guid itemId, JsonObject? value = null)
    {
        if (userId.Equals(Guid.Empty) || itemId.Equals(Guid.Empty))
        {
            throw new ArgumentException("A profile and book are required.");
        }

        JsonObject? normalized = null;
        if (value is not null)
        {
            var format = value["format"]?.GetValue<string>();
            var location = value["location"]?.GetValue<string>() ?? string.Empty;
            var theme = value["theme"]?.GetValue<string>() ?? "dark";
            var font = value["fontSize"]?.GetValue<int>() ?? 18;
            var validLocation = format == "pdf"
                ? int.TryParse(location, NumberStyles.None, CultureInfo.InvariantCulture, out var page) && page > 0
                : format == "epub" && (location.Length == 0 || (location.StartsWith("epubcfi(", StringComparison.Ordinal) && location.EndsWith(')')));
            if (!validLocation || location.Length > 2048 || theme is not ("dark" or "light" or "sepia") || font is < 10 or > 40)
            {
                throw new ArgumentException("Invalid reading position or preferences.");
            }

            normalized = new JsonObject { ["schemaVersion"] = 1, ["format"] = format, ["location"] = location, ["theme"] = theme, ["fontSize"] = font, ["updatedAt"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) };
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var path = Path.Combine(_directory, userId.ToString("N") + "-" + itemId.ToString("N") + ".json");
            if (normalized is null)
            {
                return File.Exists(path) ? JsonNode.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false))!.AsObject() : new JsonObject { ["schemaVersion"] = 1, ["location"] = string.Empty, ["theme"] = "dark", ["fontSize"] = 18 };
            }

            Directory.CreateDirectory(_directory);
            var temporary = path + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporary, normalized.ToJsonString()).ConfigureAwait(false);
                File.Move(temporary, path, true);
            }
            finally
            {
                File.Delete(temporary);
            }

            return normalized;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();
}
