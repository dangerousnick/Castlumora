using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Extensions;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>Opt-in self-registration and administrator-owned invitation lifecycle.</summary>
[Authorize]
[Route("Castlumora/Registration")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class CastlumoraRegistrationController : BaseJellyfinApiController
{
    private readonly RegistrationService _registration;

    /// <summary>Initializes a new instance of the <see cref="CastlumoraRegistrationController"/> class.</summary>
    /// <param name="registration">Registration journal and existing account provisioning.</param>
    public CastlumoraRegistrationController(RegistrationService registration) => _registration = registration;

    /// <summary>Gets only public registration availability.</summary>
    /// <returns>Enabled/invitation-required flags.</returns>
    [AllowAnonymous]
    [HttpGet]
    public Task<JsonObject> GetPublic() => _registration.PublicAsync();

    /// <summary>Creates an ordinary account using server policy and an optional invitation.</summary>
    /// <param name="value">Username, password and optional invitation token.</param>
    /// <returns>Creation confirmation without a session or policy details.</returns>
    [AllowAnonymous]
    [HttpPost]
    [RequestSizeLimit(16384)]
    public async Task<ActionResult<JsonObject>> Register([FromBody] JsonObject value)
    {
        if (!_registration.TryAttempt(HttpContext.GetNormalizedRemoteIP().ToString()))
        {
            Response.Headers.RetryAfter = "60";
            return StatusCode(StatusCodes.Status429TooManyRequests);
        }

        try
        {
            await _registration.RegisterAsync(value["username"]?.GetValue<string>() ?? string.Empty, value["password"]?.GetValue<string>() ?? string.Empty, value["invitation"]?.GetValue<string>()).ConfigureAwait(false);
            return Ok(new JsonObject { ["created"] = true });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException)
        {
            return BadRequest("Registration could not be completed with these details. Contact the administrator or check sign-in if an earlier request was interrupted.");
        }
        catch (NotSupportedException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "This account manager does not support safe self-registration.");
        }
    }

    /// <summary>Gets administrator settings, redacted invitation metadata and registration audit.</summary>
    /// <returns>Administrator state.</returns>
    [HttpGet("Admin")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public Task<JsonObject> GetAdmin() => _registration.AdminAsync();

    /// <summary>Saves administrator-controlled registration settings.</summary>
    /// <param name="value">Revision, switches and default non-administrator policy.</param>
    /// <returns>Validated settings.</returns>
    [HttpPut("Settings")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult<JsonObject>> SaveSettings([FromBody] JsonObject value)
    {
        try
        {
            return await _registration.SaveSettingsAsync(value).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return Conflict("Settings changed or have invalid field types. Reload and check the configuration.");
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or NullReferenceException)
        {
            return BadRequest("Invalid registration settings or policy.");
        }
    }

    /// <summary>Creates an expiring capped invitation; its raw token is returned once.</summary>
    /// <param name="value">Invitation label, expiry, cap and optional policy.</param>
    /// <returns>Invitation ID and one-time token.</returns>
    [HttpPost("Invitations")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult<JsonObject>> CreateInvitation([FromBody] JsonObject value)
    {
        try
        {
            return await _registration.CreateInviteAsync(value, User.GetUserId()).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException)
        {
            return BadRequest("Check invitation label, future expiry, successful-registration cap and non-administrator policy.");
        }
    }

    /// <summary>Revokes an invitation for future registration requests.</summary>
    /// <param name="id">Invitation ID.</param>
    /// <returns>No content.</returns>
    [HttpDelete("Invitations/{id:guid}")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult> RevokeInvitation(Guid id)
    {
        try
        {
            await _registration.RevokeAsync(id.ToString("N")).ConfigureAwait(false);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Resolves an outstanding crash reservation without enabling or deleting accounts.</summary>
    /// <param name="id">Registration journal ID.</param>
    /// <param name="consume">Whether to count the existing account as completed.</param>
    /// <returns>No content or a conflict requiring administrator inspection.</returns>
    [HttpPost("Reservations/{id:guid}/Resolve")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult> ResolveReservation(Guid id, [FromQuery] bool consume)
    {
        try
        {
            await _registration.ResolveAsync(id.ToString("N"), consume, User.GetUserId()).ConfigureAwait(false);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException)
        {
            return Conflict("Inspect the matching account and reservation before resolving it.");
        }
    }
}
