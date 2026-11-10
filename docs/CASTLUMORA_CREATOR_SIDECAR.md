# Native retained creator metadata — first increment

This is a built-in local metadata reader, not a plugin or downloader. It identifies **existing generic Video files** from adjacent `.info.json` files without contacting YouTube, yt-dlp, thumbnail hosts or any other service. No media is redownloaded, moved or duplicated by the provider. The full production metadata/import specification remains mandatory and incomplete.

## Identification and configuration

Use an explicitly **Home videos** library for creator videos. Place `filename.info.json` beside `filename.mp4` (the media extension is replaced, not appended). Required fields: `id` with exactly eleven YouTube identifier characters, `extractor_key: "Youtube"` or `extractor: "youtube"`, and a nonempty bounded `title`. A supplied `webpage_url` must be the matching canonical HTTPS YouTube watch URL or youtu.be URL. No fuzzy title matching is performed. A recognizable `[video-id]` in the media filename must agree with the sidecar; conflicting source identities return no match. Explicit Movie/Episode types are not silently reclassified.

The reader imports the title/original title, escaped description, publication date/year, `YouTube` provider ID, and validated stable `YouTubeChannel` ID. A channel display name is stored as a studio only alongside a valid stable channel ID. CreatorVideo/YouTube tags permit future organization; this increment does not create actual channel/playlist entities. Runtime and streams remain FFprobe-authoritative; rating, permissions and manual locks are not inferred from source text.

The provider is named **Castlumora YouTube sidecar**. Existing local NFO corrections precede it by default. Existing library options support its ordering through `LocalMetadataReaderOrder`; put its exact name in `DisabledLocalMetadataReaders` to disable this reader. Native administrator library settings expose advanced server-side JSON for these options; a dedicated metadata-provider dashboard is a later required increment.

A normal refresh can retain an existing title while adding source IDs. For intentional repair, use the inherited administrator item-refresh API with `metadataRefreshMode=FullRefresh&replaceAllMetadata=true` and local metadata saving disabled. This is an explicit replacement operation. Existing whole-item `LockData` and per-field `LockedFields` still protect corrections; unlocking permits repair. Do not change global merge semantics or relabel ordinary movies to make a filename match.

## Browsing and description safety

Browse/Search/My List can include generic videos. Browse offers **All videos** and **Creator & home videos**, alongside existing movie/series filters. Media details and the existing player work for these generic items. Existing library/user restrictions remain backend-enforced.

Descriptions use a formatting-only DOMPurify allowlist in native details/episode summaries. It permits basic paragraphs/emphasis/lists and no attributes, images, links, frames or executable content. Escaped source markup displays as literal text. It cannot load external thumbnail URLs or execute embedded script. Local artwork continues through existing local-image providers; JSON thumbnail URLs are deliberately ignored.

## Input/resource boundaries

Only an exact adjacent sidecar chosen from the actual library media path is opened. JSON cannot select a file path, destination, executable or URL to fetch. Symlink sidecars are ignored. Initial size and actual streamed bytes are bounded to 2 MiB, JSON depth to 32, and duplicate root keys are rejected. Field lengths and identifier alphabets are validated. Cancellation is preserved. Missing, unreadable, malformed, ambiguous or oversized metadata produces no guessed identity. This does not independently verify the authenticity/ownership of locally retained source claims; administrators remain responsible for authorized media.

## Executed verification

- Release provider build: zero warnings/errors.
- Full metadata-provider suite: **532 passed**, including **17** new retained-sidecar cases covering identity, conflicting IDs/URLs/types, malformed/duplicate JSON, field bounds, cancellation, symlink rejection and disabled-reader behavior.
- Real isolated backend: previously scanned generic media identified by subsequently added sidecar; explicit title repair; escaped description; original duration; whole-item and title-only manual locks; unlock repair; conflicting filename rejected. All source media retained, no external metadata/network request required by this provider.
- Node 24 frontend: typecheck/lint/build and **50 tests**, including formatting-only sanitizer cases.
- Actual Chromium creator browsing/details/escaped description/playback and existing movie browse/playback regression are recorded in DEVELOPMENT_PROGRESS.md after execution.

Guarded actual-server verification (only the marked disposable loopback fixture):

```bash
cd Castlumora.Web
python3 browser/verify-creator-sidecar.py --fixture-root /tmp/castlumora-web-real-server
CASTLUMORA_TEST_CREATOR=1 npm run test:browser -- browser/creator-server.spec.ts
```

Provide the usual test server/user/password environment privately. The helper verifies the server ProgramDataPath and never prints credentials. It retains its dedicated synthetic library/files/result for inspection; `--resume` requires the owned creator-fixture marker. It does not target production or overwrite arbitrary existing folders. Final fixture metadata is unlocked after testing.

## Remaining mandatory work

Online API-based enrichment, embedded IDs beyond the exact sidecar/filename guard, language/subtitle/playlist relationships, dedicated channel pages, source-policy caching, ambiguity review UI, bulk repair/deduplication UI, provider management UI and wider category/recommendation integration remain unfinished. No movie/TV fuzzy-match improvement or migration/reclassification of wrongly typed existing titles is claimed. The native safe yt-dlp runner, queue, scheduler, dependency management, recovery and Imports & Sources dashboard are separate required work, not supplied by this reader. This increment is not the full production YouTube provider or importer.
