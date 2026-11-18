using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Library;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Api.Castlumora;

/// <summary>Self-scoped media preferences without granting identity or policy editing.</summary>
public sealed class MediaPreferencesService
{
    private readonly IUserManager _users;
    private readonly ProfileService _profiles;
    private readonly IDbContextFactory<JellyfinDbContext> _database;

    /// <summary>Initializes a new instance of the <see cref="MediaPreferencesService"/> class.</summary>
    /// <param name="users">Existing user authorization and configuration.</param>
    /// <param name="profiles">Managed-profile reservation and owner policy.</param>
    /// <param name="database">Existing user database; updates track only whitelisted columns.</param>
    public MediaPreferencesService(IUserManager users, ProfileService profiles, IDbContextFactory<JellyfinDbContext> database)
    {
        _users = users;
        _profiles = profiles;
        _database = database;
    }

    /// <summary>Reads or updates only the current identity's harmless media settings.</summary>
    /// <param name="caller">Authenticated identity; never taken from request JSON.</param>
    /// <param name="value">Whitelisted updates, or null for a read.</param>
    /// <param name="token">Request cancellation.</param>
    /// <returns>Current preferences and whether this identity may edit them.</returns>
    public async Task<JsonObject> ReadOrUpdateAsync(Guid caller, JsonObject? value, CancellationToken token)
    {
        var identity = _users.GetUserById(caller) ?? throw new UnauthorizedAccessException();
        if (identity.HasPermission(PermissionKind.IsDisabled))
        {
            throw new UnauthorizedAccessException();
        }

        var canEdit = identity.HasPermission(PermissionKind.IsAdministrator) || identity.EnableUserPreferenceAccess
            || (await _profiles.CanEditManagedPreferencesAsync(caller, token).ConfigureAwait(false)
                && await _profiles.SessionAllowedAsync(caller, token).ConfigureAwait(false));
        if (value is not null && !canEdit)
        {
            throw new UnauthorizedAccessException();
        }

        await using var context = await _database.CreateDbContextAsync(token).ConfigureAwait(false);
        var user = await context.Users.SingleOrDefaultAsync(user => user.Id.Equals(caller), token).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException();
        if (value is not null)
        {
            if (value.Count is < 1 or > 4 || value.Any(pair => pair.Key is not ("audioLanguage" or "subtitleLanguage" or "useDefaultAudioTrack" or "subtitleMode")))
            {
                throw new InvalidDataException("Only media language, source audio and subtitle mode may be changed.");
            }

            var audio = value.ContainsKey("audioLanguage") ? Language(value["audioLanguage"], true) : user.AudioLanguagePreference;
            var subtitle = value.ContainsKey("subtitleLanguage") ? Language(value["subtitleLanguage"], false) : user.SubtitleLanguagePreference;
            var useDefault = value.ContainsKey("useDefaultAudioTrack") ? value["useDefaultAudioTrack"]!.GetValue<bool>() : user.PlayDefaultAudioTrack;
            var mode = user.SubtitleMode;
            if (value.ContainsKey("subtitleMode"))
            {
                var name = value["subtitleMode"]!.GetValue<string>();
                if (!Enum.TryParse(name, out mode) || !Enum.IsDefined(mode) || mode.ToString() != name)
                {
                    throw new InvalidDataException("Choose a supported subtitle mode.");
                }
            }

            // Do not call a full configuration replacement: concurrent administrator settings
            // (including identity/security fields) must never be overwritten by a stale DTO.
            user.AudioLanguagePreference = audio;
            user.SubtitleLanguagePreference = subtitle;
            user.PlayDefaultAudioTrack = useDefault;
            user.SubtitleMode = mode;
            await context.SaveChangesAsync(token).ConfigureAwait(false);
        }

        return new JsonObject
        {
            ["canEdit"] = canEdit,
            ["audioLanguage"] = user.AudioLanguagePreference,
            ["subtitleLanguage"] = user.SubtitleLanguagePreference,
            ["useDefaultAudioTrack"] = user.PlayDefaultAudioTrack,
            ["subtitleMode"] = user.SubtitleMode.ToString()
        };
    }

    private static string? Language(JsonNode? node, bool originalAudio)
    {
        var value = node?.GetValue<string>()?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return null;
        }

        if (originalAudio && value == "OriginalLanguage")
        {
            return value;
        }

        var parts = value.Split('-');
        if (value.Length > 35 || parts.Length > 3 || parts[0].Length is < 2 or > 3 || !parts[0].All(char.IsAsciiLetter)
            || parts.Skip(1).Any(part => part.Length is < 2 or > 8 || !part.All(char.IsAsciiLetterOrDigit)))
        {
            throw new InvalidDataException("Use an ISO language/language-region code, or blank for automatic.");
        }

        return value.ToLowerInvariant();
    }
}
