using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Extensions;
using Jellyfin.Data.Queries;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Session;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>Token-free inventory and revocation of the authenticated identity's saved sign-ins.</summary>
[Authorize]
[Route("Castlumora/Account/Sessions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CastlumoraAccountSessionsController : BaseJellyfinApiController
{
    private readonly IDeviceManager _devices;
    private readonly ISessionManager _sessions;

    /// <summary>Initializes a new instance of the <see cref="CastlumoraAccountSessionsController"/> class.</summary>
    /// <param name="devices">Existing persisted authorization records.</param>
    /// <param name="sessions">Existing scoped token/session revocation.</param>
    public CastlumoraAccountSessionsController(IDeviceManager devices, ISessionManager sessions)
    {
        _devices = devices;
        _sessions = sessions;
    }

    /// <summary>Lists only this identity's saved authorizations, never tokens or other identities.</summary>
    /// <param name="startIndex">Nonnegative page offset.</param>
    /// <param name="limit">Page size, at most one hundred.</param>
    /// <returns>Bounded safe device metadata and the current sign-in marker.</returns>
    [HttpGet]
    public ActionResult<JsonObject> List([FromQuery, Range(0, int.MaxValue)] int startIndex = 0, [FromQuery, Range(1, 100)] int limit = 20)
    {
        if (!PersonalIdentity())
        {
            return Forbid();
        }

        return Describe(_devices, User.GetUserId(), User.GetToken(), startIndex, limit);
    }

    internal static JsonObject Describe(IDeviceManager devices, Guid identity, string? currentToken, int startIndex, int limit)
    {
        var found = devices.GetDevices(new DeviceQuery { UserId = identity, Skip = startIndex, Limit = limit });
        var entries = new JsonArray();
        foreach (var device in found.Items)
        {
            entries.Add(new JsonObject
            {
                ["id"] = device.Id,
                ["deviceName"] = device.DeviceName,
                ["appName"] = device.AppName,
                ["appVersion"] = device.AppVersion,
                ["lastActive"] = DateTime.SpecifyKind(device.DateLastActivity, DateTimeKind.Utc),
                ["current"] = currentToken is not null && string.Equals(device.AccessToken, currentToken, StringComparison.OrdinalIgnoreCase)
            });
        }

        return new JsonObject { ["identityId"] = identity.ToString("N"), ["total"] = found.TotalRecordCount, ["sessions"] = entries };
    }

    /// <summary>Revokes only an owned saved sign-in; use ordinary logout for the current token.</summary>
    /// <param name="id">Authorization record ID, not a client-supplied physical device ID.</param>
    /// <returns>No content, not found for unknown/foreign IDs, or conflict for the current token.</returns>
    [HttpDelete("{id:int:min(1)}")]
    public async Task<ActionResult> Revoke([FromRoute] int id)
    {
        if (!PersonalIdentity())
        {
            return Forbid();
        }

        var devices = _devices.GetDevices(new DeviceQuery { UserId = User.GetUserId() });
        foreach (var device in devices.Items)
        {
            if (device.Id != id)
            {
                continue;
            }

            if (string.Equals(device.AccessToken, User.GetToken(), StringComparison.OrdinalIgnoreCase))
            {
                return Conflict("Use Sign out to end your current sign-in.");
            }

            await _sessions.Logout(device).ConfigureAwait(false);
            return NoContent();
        }

        return NotFound();
    }

    /// <summary>Revokes other saved sign-ins for this identity, retaining the caller's authorization.</summary>
    /// <param name="value">Empty confirmation object, with no requested target identity.</param>
    /// <returns>No content after inherited token revocation completes.</returns>
    [HttpPost("RevokeOthers")]
    [RequestSizeLimit(1024)]
    public async Task<ActionResult> RevokeOthers([FromBody] JsonObject value)
    {
        if (!PersonalIdentity())
        {
            return Forbid();
        }

        if (value.Count != 0)
        {
            return BadRequest("Sign-out accepts no target identity or permission overrides.");
        }

        await _sessions.RevokeUserTokens(User.GetUserId(), User.GetToken()).ConfigureAwait(false);
        return NoContent();
    }

    private bool PersonalIdentity() => !User.GetIsApiKey() && !User.GetUserId().Equals(Guid.Empty) && !string.IsNullOrEmpty(User.GetToken());
}
