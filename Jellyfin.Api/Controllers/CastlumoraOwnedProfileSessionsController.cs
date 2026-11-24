using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Extensions;
using Jellyfin.Data.Queries;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Session;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>Owner-authorized management of registered viewing-profile sign-ins.</summary>
[Authorize]
[Route("Castlumora/Account/ProfileSessions/{profileId:guid}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CastlumoraOwnedProfileSessionsController : BaseJellyfinApiController
{
    private readonly ProfileService _profiles;
    private readonly IDeviceManager _devices;
    private readonly ISessionManager _sessions;

    /// <summary>Initializes a new instance of the <see cref="CastlumoraOwnedProfileSessionsController"/> class.</summary>
    /// <param name="profiles">Private ownership and serialized lifecycle.</param>
    /// <param name="devices">Persisted authorizations.</param>
    /// <param name="sessions">Inherited token revocation.</param>
    public CastlumoraOwnedProfileSessionsController(ProfileService profiles, IDeviceManager devices, ISessionManager sessions)
    {
        _profiles = profiles;
        _devices = devices;
        _sessions = sessions;
    }

    /// <summary>Lists a registered child's sign-ins only for its owning account.</summary>
    /// <param name="profileId">Managed child identity.</param>
    /// <param name="startIndex">Nonnegative page offset.</param>
    /// <param name="limit">Bounded page size.</param>
    /// <param name="token">Request cancellation.</param>
    /// <returns>Safe device metadata, never tokens.</returns>
    [HttpGet]
    public Task<ActionResult<JsonObject>> List(Guid profileId, [FromQuery, Range(0, int.MaxValue)] int startIndex = 0, [FromQuery, Range(1, 100)] int limit = 20, CancellationToken token = default)
        => Owned(
            profileId,
            name =>
            {
                var page = CastlumoraAccountSessionsController.Describe(_devices, profileId, null, startIndex, limit);
                page["profileName"] = name;
                return Task.FromResult<ActionResult<JsonObject>>(page);
            },
            token);

    /// <summary>Revokes one saved sign-in belonging to the owned child.</summary>
    /// <param name="profileId">Managed child identity.</param>
    /// <param name="id">Saved authorization ID.</param>
    /// <param name="token">Request cancellation.</param>
    /// <returns>No content, or not found for unknown/foreign authorization IDs.</returns>
    [HttpDelete("{id:int:min(1)}")]
    public Task<ActionResult<JsonObject>> Revoke(Guid profileId, int id, CancellationToken token)
        => Owned(
            profileId,
            async _ =>
            {
                var device = _devices.GetDevices(new DeviceQuery { UserId = profileId }).Items.FirstOrDefault(device => device.Id == id);
                if (device is null)
                {
                    return NotFound();
                }

                await _sessions.Logout(device).ConfigureAwait(false);
                return NoContent();
            },
            token);

    /// <summary>Revokes all sign-ins for one owned child, preserving owner and sibling identities.</summary>
    /// <param name="profileId">Managed child identity.</param>
    /// <param name="value">Empty confirmation object.</param>
    /// <param name="token">Request cancellation.</param>
    /// <returns>No content; no profile key, history or lifecycle is changed.</returns>
    [HttpPost("RevokeAll")]
    [RequestSizeLimit(1024)]
    public Task<ActionResult<JsonObject>> RevokeAll(Guid profileId, [FromBody] JsonObject value, CancellationToken token)
        => Owned(
            profileId,
            async _ =>
            {
                if (value.Count != 0)
                {
                    return BadRequest("Profile sign-out accepts no account, credential or permission overrides.");
                }

                await _sessions.RevokeUserTokens(profileId, null).ConfigureAwait(false);
                return NoContent();
            },
            token);

    private async Task<ActionResult<JsonObject>> Owned(Guid identity, Func<string, Task<ActionResult<JsonObject>>> operation, CancellationToken token)
    {
        if (User.GetIsApiKey() || User.GetUserId() == Guid.Empty || string.IsNullOrEmpty(User.GetToken()))
        {
            return Forbid();
        }

        try
        {
            return await _profiles.UseOwnedProfileSessionsAsync(User.GetUserId(), identity, operation, token).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException)
        {
            return StatusCode(503, "Profile ownership is unavailable. No successful sign-out is claimed; preserve configuration and retry after repair.");
        }
    }
}
