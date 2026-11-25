using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Extensions;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>Permission-filtered, paginated virtual categories across existing libraries.</summary>
[Authorize]
[Route("Castlumora/Categories")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class CastlumoraCategoriesController : BaseJellyfinApiController
{
    private readonly PresentationStore _store;
    private readonly ILibraryManager _library;
    private readonly IUserManager _users;
    private readonly IDtoService _dto;

    /// <summary>Initializes a new instance of the <see cref="CastlumoraCategoriesController"/> class.</summary>
    /// <param name="store">Published category definitions.</param>
    /// <param name="library">Existing permission-aware library.</param>
    /// <param name="users">Authenticated users.</param>
    /// <param name="dto">Existing DTO service.</param>
    public CastlumoraCategoriesController(PresentationStore store, ILibraryManager library, IUserManager users, IDtoService dto)
    {
        _store = store;
        _library = library;
        _users = users;
        _dto = dto;
    }

    /// <summary>Gets an ordered category page; no media is moved or modified.</summary>
    /// <param name="id">Published category ID.</param>
    /// <param name="startIndex">Zero-based page offset.</param>
    /// <param name="limit">Page size, 1–100.</param>
    /// <param name="searchTerm">Optional name filter.</param>
    /// <returns>Accessible category media and total count.</returns>
    [HttpGet("{id}/Items")]
    public async Task<ActionResult<QueryResult<BaseItemDto>>> GetItems(string id, [FromQuery] int startIndex = 0, [FromQuery] int limit = 48, [FromQuery] string? searchTerm = null)
    {
        if (startIndex < 0 || limit is < 1 or > 100 || searchTerm?.Length > 128)
        {
            return BadRequest("Invalid category page parameters.");
        }

        var user = _users.GetUserById(User.GetUserId());
        if (user is null)
        {
            return Unauthorized();
        }

        var layout = PresentationStore.ForUser(await _store.ReadLayoutAsync(false).ConfigureAwait(false), user.Id);
        var category = layout["categories"]!.AsArray().FirstOrDefault(c => string.Equals(c!["id"]!.GetValue<string>(), id, StringComparison.Ordinal));
        if (category is null)
        {
            return NotFound();
        }

        var rules = category["rules"]!.AsObject();
        var types = Strings(rules, "contentTypes").Select(Enum.Parse<BaseItemKind>).ToArray();
        if (types.Length == 0)
        {
            types = [BaseItemKind.Movie, BaseItemKind.Series];
        }

        InternalItemsQuery Query()
        {
            var query = new InternalItemsQuery(user)
            {
                Recursive = true,
                IsVirtualItem = false,
                IncludeItemTypes = types,
                DtoOptions = DtoOptions.StoredColumnsOnly
            };
            // Establish the user's allowed library roots before adding explicit item/folder filters.
            _library.ConfigureUserAccess(query, user);
            return query;
        }

        var matches = new HashSet<Guid>();
        void Include(InternalItemsQuery query) => matches.UnionWith(_library.GetItemIds(query));
        var manual = Strings(rules, "itemIds").Select(Guid.Parse).ToArray();
        if (manual.Length > 0)
        {
            var query = Query();
            query.ItemIds = manual;
            Include(query);
        }

        var genres = Strings(rules, "genres");
        if (genres.Length > 0)
        {
            var query = Query();
            query.Genres = genres;
            Include(query);
        }

        var studios = Strings(rules, "studios");
        if (studios.Length > 0)
        {
            var query = Query();
            query.StudioIds = studios.Select(name => _library.GetStudio(name).Id).ToArray();
            Include(query);
        }

        var folders = Strings(rules, "folderIds").Select(Guid.Parse).ToHashSet();
        var folderNames = Strings(rules, "folderNames");
        if (folderNames.Length > 0)
        {
            var allowedFolders = _library.GetItemList(new InternalItemsQuery(user) { Recursive = true, IsFolder = true, DtoOptions = DtoOptions.StoredColumnsOnly });
            foreach (var folder in allowedFolders)
            {
                var physicalName = string.IsNullOrEmpty(folder.Path) ? string.Empty : Path.GetFileName(Path.TrimEndingDirectorySeparator(folder.Path));
                var configuredRootMatch = folder is CollectionFolder && _library.GetLibraryOptions(folder).PathInfos.Any(path =>
                    !string.IsNullOrEmpty(path.Path) && folderNames.Contains(Path.GetFileName(Path.TrimEndingDirectorySeparator(path.Path)), StringComparer.OrdinalIgnoreCase));
                if (configuredRootMatch || folderNames.Contains(folder.Name, StringComparer.OrdinalIgnoreCase) || folderNames.Contains(physicalName, StringComparer.OrdinalIgnoreCase))
                {
                    folders.Add(folder.Id);
                }
            }
        }

        foreach (var folderId in folders)
        {
            // Resolve through the existing user-aware lookup, never trust a published folder ID as authorization.
            if (_library.GetItemById<BaseItem>(folderId, user) is not Folder)
            {
                continue;
            }

            var query = Query();
            query.ParentId = folderId;
            // GetItemList expands virtual collection-folder parents to their physical roots.
            matches.UnionWith(_library.GetItemList(query).Select(item => item.Id));
        }

        matches.ExceptWith(Strings(rules, "excludeIds").Select(Guid.Parse));
        if (matches.Count == 0)
        {
            return new QueryResult<BaseItemDto>(startIndex, 0, []);
        }

        var final = Query();
        final.ItemIds = matches.ToArray();
        final.NameContains = searchTerm;
        final.OrderBy = [(ItemSortBy.SortName, SortOrder.Ascending)];
        final.StartIndex = startIndex;
        final.Limit = limit;
        final.EnableTotalRecordCount = true;
        final.DtoOptions = new DtoOptions(false) { Fields = [ItemFields.Overview, ItemFields.Genres] };
        var result = _library.GetItemsResult(final);
        return new QueryResult<BaseItemDto>(startIndex, result.TotalRecordCount, _dto.GetBaseItemDtos(result.Items, final.DtoOptions, user));
    }

    private static string[] Strings(JsonObject rules, string key) => rules[key]?.AsArray().Select(n => n!.GetValue<string>()).ToArray() ?? [];
}
