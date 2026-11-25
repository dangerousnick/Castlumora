using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Extensions;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>Authenticated account ownership and independently restricted viewing profiles.</summary>
[Authorize]
[Route("Castlumora/Profiles")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CastlumoraProfilesController : BaseJellyfinApiController
{
    private readonly ProfileService _profiles;
    private readonly IAuthorizationContext _authorization;

    /// <summary>Initializes a new instance of the <see cref="CastlumoraProfilesController"/> class.</summary>
    /// <param name="profiles">Private profile ownership and inherited authentication.</param>
    /// <param name="authorization">Authenticated device identity.</param>
    public CastlumoraProfilesController(ProfileService profiles, IAuthorizationContext authorization)
    {
        _profiles = profiles;
        _authorization = authorization;
    }

    /// <summary>Lists only the current account's linked viewing profiles.</summary>
    /// <param name="token">Request cancellation.</param>
    /// <returns>Safe identities without managed credentials.</returns>
    [HttpGet]
    public async Task<ActionResult<JsonObject>> List(CancellationToken token)
    {
        try
        {
            return await _profiles.ListAsync(User.GetUserId(), token).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Profile configuration requires administrator recovery; preserve its files.");
        }
    }

    /// <summary>Lists managed profiles for administrator review and cleanup.</summary>
    /// <param name="token">Request cancellation.</param>
    /// <returns>Profiles including pending and orphaned records, without credentials.</returns>
    [HttpGet("Managed")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult<JsonObject>> Managed(CancellationToken token)
    {
        try
        {
            return await _profiles.ListManagedAsync(User.GetUserId(), token).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Profile configuration requires administrator recovery; preserve its files.");
        }
    }

    /// <summary>Renames only the display label of a registered managed profile.</summary>
    /// <param name="profileId">Managed identity; owning accounts cannot be renamed here.</param>
    /// <param name="value">Only the new display name.</param>
    /// <param name="token">Request cancellation.</param>
    /// <returns>No content after saving; not found for an account or unknown profile.</returns>
    [HttpPut("Managed/{profileId:guid}/Name")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [RequestSizeLimit(4096)]
    public async Task<ActionResult> Rename([FromRoute] Guid profileId, [FromBody] JsonObject value, CancellationToken token)
    {
        try
        {
            if (value.Count != 1 || value["name"] is not JsonValue name || !name.TryGetValue<string>(out var text))
            {
                return BadRequest("Supply only the new profile display name.");
            }

            return await _profiles.RenameAsync(User.GetUserId(), profileId, text, token).ConfigureAwait(false) ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidDataException)
        {
            return BadRequest("Choose a unique profile display name of up to forty characters.");
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Profile configuration requires administrator recovery; preserve its files.");
        }
    }

    /// <summary>Updates a managed profile's preset and explicit libraries without replacing its identity.</summary>
    /// <param name="profileId">Existing managed identity.</param>
    /// <param name="value">Preset, library IDs and reviewed policy revision only.</param>
    /// <param name="token">Cancellation before the durable update begins.</param>
    /// <returns>No content after applying; conflict for stale or unavailable profiles.</returns>
    [HttpPut("Managed/{profileId:guid}/Access")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [RequestSizeLimit(8192)]
    public async Task<ActionResult> UpdateAccess([FromRoute] Guid profileId, [FromBody] JsonObject value, CancellationToken token)
    {
        if (User.GetIsApiKey() || User.GetUserId().Equals(Guid.Empty))
        {
            return Forbid();
        }

        if (value.Count != 3 || value["preset"] is not JsonValue preset || !preset.TryGetValue<string>(out var presetName)
            || value["revision"] is not JsonValue revision || !revision.TryGetValue<string>(out var expectedRevision)
            || value["libraries"] is not JsonArray libraries || libraries.Count is < 1 or > 100)
        {
            return BadRequest("Supply only a preset, explicit library IDs and the reviewed access revision.");
        }

        var folders = new Guid[libraries.Count];
        for (var index = 0; index < folders.Length; index++)
        {
            if (libraries[index] is not JsonValue library || !library.TryGetValue<string>(out var text) || !Guid.TryParse(text, out folders[index]))
            {
                return BadRequest("Choose valid library IDs.");
            }
        }

        try
        {
            return await _profiles.UpdateAccessAsync(User.GetUserId(), profileId, presetName, folders, expectedRevision, token).ConfigureAwait(false) ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Profile access did not complete. Preserve the identity, key and configuration; review interrupted setup before retrying.");
        }
    }

    /// <summary>Resumes only the saved setup plan of an interrupted managed profile.</summary>
    /// <param name="profileId">Original managed identity.</param>
    /// <param name="value">An empty confirmation object; no identity or policy overrides.</param>
    /// <param name="token">Cancellation before repair begins.</param>
    /// <returns>No content on success, not found for unknown identities, conflict for unsafe states.</returns>
    [HttpPost("Managed/{profileId:guid}/Recover")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [RequestSizeLimit(4096)]
    public async Task<ActionResult> Recover([FromRoute] Guid profileId, [FromBody] JsonObject value, CancellationToken token)
    {
        if (value.Count != 0)
        {
            return BadRequest("Recovery accepts no identity, permission or library overrides.");
        }

        try
        {
            return await _profiles.RecoverAsync(User.GetUserId(), profileId, token).ConfigureAwait(false) ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException)
        {
            return Conflict("Recovery requires explicit interrupted provisioning, the original restricted identity and currently permitted owner/libraries.");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Recovery did not complete. Preserve the profile, key and configuration; correct the problem before retrying.");
        }
    }

    /// <summary>Deletes only a managed profile, preserving its account and media files.</summary>
    /// <param name="profileId">Managed identity.</param>
    /// <param name="token">Cancellation before deletion begins.</param>
    /// <returns>No content after deletion; not found for an account or unknown profile.</returns>
    [HttpDelete("Managed/{profileId:guid}")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult> Delete([FromRoute] Guid profileId, CancellationToken token)
    {
        try
        {
            return await _profiles.DeleteAsync(User.GetUserId(), profileId, token).ConfigureAwait(false) ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Profile removal requires review. Preserve its unavailable record and retry after correcting the problem.");
        }
    }

    /// <summary>Creates a managed kids/adult profile with explicit libraries.</summary>
    /// <param name="value">Existing account ID, display name, preset and library IDs.</param>
    /// <param name="token">Request cancellation.</param>
    /// <returns>The new viewing identity.</returns>
    [HttpPost]
    [Authorize(Policy = Policies.RequiresElevation)]
    [RequestSizeLimit(16384)]
    public async Task<ActionResult<JsonObject>> Create([FromBody] JsonObject value, CancellationToken token)
    {
        try
        {
            if (value.Any(pair => pair.Key is not ("accountId" or "name" or "preset" or "libraries")) || value["libraries"] is not JsonArray folders || folders.Count is < 1 or > 100)
            {
                return BadRequest("Choose an account, profile name, preset and explicit libraries.");
            }

            return await _profiles.CreateAsync(User.GetUserId(), Guid.Parse(value["accountId"]!.GetValue<string>()), value["name"]!.GetValue<string>(), value["preset"]!.GetValue<string>(), folders.Select(folder => Guid.Parse(folder!.GetValue<string>())).ToArray(), token).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or InvalidDataException or NullReferenceException)
        {
            return BadRequest("Check the enabled password-protected account, unique profile name and permitted libraries.");
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Profile provisioning requires review. Preserve configuration and any disabled pending user; do not repeat blindly.");
        }
    }

    /// <summary>Switches profiles only after account-password verification and old-token revocation.</summary>
    /// <param name="value">Target identity and account password.</param>
    /// <param name="token">Request cancellation before token issuance.</param>
    /// <returns>A normal user-scoped session.</returns>
    [HttpPost("Switch")]
    [RequestSizeLimit(8192)]
    public async Task<ActionResult<AuthenticationResult>> Switch([FromBody] JsonObject value, CancellationToken token)
    {
        try
        {
            if (value.Any(pair => pair.Key is not ("profileId" or "accountPassword")))
            {
                return BadRequest("Only a profile identity and account password are accepted.");
            }

            var auth = await _authorization.GetAuthorizationInfo(Request).ConfigureAwait(false);
            return await _profiles.SwitchAsync(User.GetUserId(), Guid.Parse(value["profileId"]!.GetValue<string>()), value["accountPassword"]!.GetValue<string>(), auth, HttpContext.GetNormalizedRemoteIP().ToString(), token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or AuthenticationException or SecurityException)
        {
            return Unauthorized("Profile switch denied. Verify the account password, profile permissions and device access.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or NullReferenceException)
        {
            return BadRequest("Select a linked profile and provide the account password.");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Profile configuration requires administrator recovery.");
        }
    }
}
