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
