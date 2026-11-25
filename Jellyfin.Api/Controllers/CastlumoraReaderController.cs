using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>Permission-checked book positions owned only by the authenticated profile.</summary>
[Authorize]
[Route("Castlumora/Reader")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class CastlumoraReaderController : BaseJellyfinApiController
{
    private readonly ReaderStore _store;
    private readonly ILibraryManager _library;

    /// <summary>Initializes a new instance of the <see cref="CastlumoraReaderController"/> class.</summary>
    /// <param name="store">Profile/book storage.</param>
    /// <param name="library">Existing permission-aware lookup.</param>
    public CastlumoraReaderController(ReaderStore store, ILibraryManager library)
    {
        _store = store;
        _library = library;
    }

    /// <summary>Gets the authenticated profile's reading position.</summary>
    /// <param name="id">Accessible book ID.</param>
    /// <returns>Position or not found.</returns>
    [HttpGet("{id:guid}")]
    public Task<ActionResult<JsonObject>> GetState(Guid id) => StateAsync(id, null);

    /// <summary>Saves only the authenticated profile's reading position.</summary>
    /// <param name="id">Accessible book ID.</param>
    /// <param name="value">Position/preferences.</param>
    /// <returns>Validated state.</returns>
    [HttpPut("{id:guid}")]
    [RequestSizeLimit(8192)]
    public Task<ActionResult<JsonObject>> SaveState(Guid id, [FromBody] JsonObject value) => StateAsync(id, value);

    private async Task<ActionResult<JsonObject>> StateAsync(Guid id, JsonObject? value)
    {
        var user = User.GetUserId();
        if (user.Equals(Guid.Empty))
        {
            return Unauthorized();
        }

        if (_library.GetItemById<BaseItem>(id, user) is not Book)
        {
            return NotFound();
        }

        try
        {
            return await _store.StateAsync(user, id, value).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return BadRequest("Invalid reading position or preferences.");
        }
    }
}
