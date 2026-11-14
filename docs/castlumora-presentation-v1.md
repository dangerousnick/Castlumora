# Presentation API v1

Permanent product requirements: [CASTLUMORA_PRODUCT_REQUIREMENTS.md](CASTLUMORA_PRODUCT_REQUIREMENTS.md).

The server stores presentation separately from media, user authorization and inherited configuration. Native clients may implement this JSON contract without using React. This is a presentation foundation, not a permission engine: all media queries still use the authenticated user's existing Jellyfin authorization and parental-control filters. Visibility never grants media access.

## Endpoints

All routes require normal server authentication. Do not use an admin/API key as an ordinary viewing profile.

| Route | Permission | Effect |
| --- | --- | --- |
| GET `/Castlumora/Presentation?schemaVersion=1&components=resume,next,recent,movies,series,category` | Authenticated | Published layout, filtered to requesting profile and optional supported row kinds. `X-Castlumora-Presentation-Version: 1`; unsupported major versions return 406. |
| GET `/Castlumora/Presentation/Draft` | Existing `RequiresElevation` policy | Full saved draft, including visibility rules. |
| PUT `/Castlumora/Presentation/Draft` | Same administrator policy | Validates complete JSON; compares revision, increments it, persists atomically. |
| POST `/Castlumora/Presentation/Publish` | Same administrator policy | Compares saved revision and publishes the **saved** draft; unsaved client edits are ignored. Revision increments. |
| GET `/Castlumora/Presentation/Preferences` | Authenticated | Own `watchlist` and `hiddenResume` arrays. |
| PUT `/Castlumora/Presentation/Preferences/{watchlist\|hiddenResume}/{itemId}?enabled=true\|false` | Authenticated | Membership only; user ID derives from claims, not request input. Never modifies media or playback history. |

A stale draft write/publish returns 409. Invalid structures return 400. Oversized requests are rejected. Filesystem failures return a server error rather than claiming a save succeeded.

## Contract

Root keys: `schemaVersion: 1`, `revision: integer >= 0`, `theme`, `navigation`, `featured`, `rows`, `categories`, `devices`.

- `theme`: `accent`, `background`, both six-digit hex colors. No arbitrary CSS/HTML/scripts.
- `navigation`: ordered array (max 4) of `{id: home|browse|list|search, label, userIds?}`. IDs unique. Empty arrays intentionally hide navigation. Route authorization stays server-owned. Icon customization is future work.
- `featured`: ordered array (max 20) of `{itemId, userIds?}`. First accessible title supplies the hero and its existing backdrop artwork. Inaccessible IDs are skipped. Banner rotation/custom text is future work.
- `rows`: ordered array (max 40) of `{id, title, kind: resume|next|recent|movies|series|category, categoryId?, userIds?}`. `categoryId` must reference a category for category rows. IDs unique.
- `categories`: ordered reusable definitions (max 100): `{id,title,icon?,color?,artworkItemId?,rules,userIds?}`. `icon` is text/emoji, not executable markup. `artworkItemId` uses a server media image. Category order is independent from row order.
- `rules`: required arrays `itemIds`, `folderIds`, `genres`, `studios`; optional `excludeIds`, `contentTypes` (Movie/Series/Episode). Each rule list has at most 200 entries. IDs are GUIDs; metadata strings max 128 characters. Rule groups are OR-unioned and deduplicated; manual exclusions always win. An empty category matches nothing. Folder IDs refer to existing library folders. Folder-name pattern matching is not implemented yet. Queries span permitted libraries; each source query currently retrieves a bounded set, and category display is capped at 48 titles. Full category pagination/indexing is future work.
- `userIds`: absent/empty means every authenticated profile; otherwise GUID allowlist. Server filters navigation, categories, rows, banners and dependent category rows before returning the published config. IDs are removed from the visible entries. These are presentation restrictions, not a replacement for parental controls.
- `devices`: optional settings under `desktop`, `tablet`, `mobile`, `tv`: `posterWidth` (100–320), `safeZone` (0–10 percent), `rowIds` (ordered subset of valid row IDs). Absent overrides inherit defaults. Web selects mobile <640 px, tablet <1024 px, desktop otherwise. TV settings can be previewed; a native TV client must select that class explicitly. Configuration previews show layout blocks, clearly labeled as previews, not real media.

The executable frontend validator is `Castlumora.Web/src/config/schema.ts`; server validation is `Jellyfin.Api/Castlumora/PresentationStore.cs`. Defaults are intentionally mirrored and covered by contract tests. Older clients skip unsupported row kinds; unknown major versions fall back safely. Plugins cannot inject arbitrary executable UI. A future registry must namespace component kinds, declare client capabilities, validate payloads and enforce plugin/profile permissions before publication. New rendering types may require client updates.

## Persistence and migration

Storage: `<ConfigurationDirectoryPath>/castlumora/layout.json` containing `draft` and `published`; per-user `user-<GUID>.json` containing separate preference lists (max 1000 IDs each). No database migrations or inherited config changes. One singleton serializes access inside the server process. Same-directory temporary files and atomic rename avoid partially written JSON. Multiple server processes sharing the same config directory are not supported. Revision checks protect concurrent editors; corruption raises an error rather than silently replacing published data. Include the directory in backups. Do not edit files while the server runs.

No history/rollback is shipped yet. Back up the directory before manual restoration while stopped. Future schema migrations must preserve old published versions until clients advertise compatibility. These endpoints do not deploy services or packages.

Hiding a series from Continue Watching hides its episodes in this client. Playback history stays intact; legacy clients do not consume the new exclusion list. Hidden IDs can be removed using the preferences endpoint to restore visibility. Following, notifications, watched state and watchlists remain distinct concepts.
