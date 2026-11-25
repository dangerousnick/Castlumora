# Native Metadata Management — selected-item workflow

Open **Administration → Metadata Management** (`/#/admin/metadata`). This is an administrator-only interface using inherited library, item-update and metadata-refresh APIs; it introduces no new authorization bypass or database schema.

## Local reader settings

Select a library, enable/disable **Castlumora YouTube sidecar**, reorder listed local readers, and save. Settings persist in server library options and survive page reloads. Saving sets an explicit library-level LocalMetadataReaderOrder; unlisted readers follow their default relative order. Existing unknown options and unrelated disabled readers are retained. This reader applies only to generic Video media, not movie/episode reclassification. Online provider management remains future work.

## Inspect, correct, lock and repair

Search by title (first 20 results; refine to narrow), then select a media item. The page shows its actual media type, stored YouTube/channel IDs if present, or a clear no-reliable-source-ID status. A matching title alone never becomes an asserted YouTube identity.

Edit title/description and optionally lock all manual metadata corrections. Saving fetches a **fresh, complete item DTO** before merging only the edited fields; it never posts the limited search-result DTO. This preserves other fields, ratings, provider IDs and per-field locks supported by the inherited API. This is not an atomic concurrent-edit/ETag protocol; administrators should avoid simultaneous edits to the same fields.

Refresh metadata queues the normal refresh. Repair from source requires explicit confirmation because it may replace unlocked metadata from configured sources. It is disabled while whole-item metadata is locked or unsaved edits exist. Existing per-field locks remain effective. Neither action replaces images or media files. Jobs are asynchronous: use **Reload metadata** after the server finishes. A queued response is not claimed as completed repair.

## Executed verification

- Node24 frontend typecheck/lint/build and **56 tests** pass, including complete-DTO preservation, unrelated library options/readers, ordinary-vs-replacement refresh, confirmation/lock behavior and denied viewer workspace.
- Actual Chromium + isolated backend workflow passes: persisted reader disable/re-enable/order after page reload, title/description correction and lock, unlock, explicit source repair, metadata reload and unchanged provider IDs.
- Ordinary synthetic viewer directly denied item update, refresh and library-options write (401/403), independent of the UI guard.
- Original synthetic library options/item metadata restored and temporary viewer removed afterwards. Guarded test snapshots to private metadata-admin-state.json before changes; successful cleanup removes it. If interrupted, use `browser/cleanup-metadata-fixture.py --fixture-root /tmp/castlumora-web-real-server` with private test environment variables. It verifies loopback, ownership marker, actual ProgramDataPath, original paths and synthetic viewer name before restoration.
- Initial browser failures exposed ambiguous wrapped select/textarea labels; explicit label associations fixed them and the full workflow passed. Backend remains the prior tested creator-provider implementation (532 provider tests); no new server module was added by this UI milestone.

Run `CASTLUMORA_TEST_CREATOR=1 npm run test:browser -- browser/metadata-admin-server.spec.ts` only after the documented owned creator fixture is prepared. Never point mutating tests at production.

## Remaining work

This is not the complete production metadata dashboard. Thumbnail/artwork editing, direct source-ID overrides, ambiguity/candidate confidence review, duplicate management, bulk repair/pagination, online enrichment/provider caching and richer audit/concurrency handling remain required. The safe yt-dlp importer/queue and Imports & Sources screen remain separate mandatory work. Existing advanced administrative access is retained as documented; compiled legacy assets are still a separate dependency.
