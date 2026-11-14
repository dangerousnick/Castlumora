# Authorized single-video YouTube importing — development workflow

Administration → Imports & Sources now includes a YouTube single-video form when
the Linux x64 server, configured FFmpeg/FFprobe and bundled Node dependency are
available. This extends the existing durable queue and safe publication boundary;
it does not implement playlists, channels or scheduled source synchronization.
It is a development feature, not a production-certified importer.

## Owner test workflow

1. Install/check the shipped managed yt-dlp runtime in Imports & Sources.
2. Create a Videos / Home Videos library with one local, securely writable root.
3. Enter a plain HTTPS YouTube watch/share/shorts/live/embed video link.
4. Select the destination, maximum quality (360/480/720/1080p) and size budget
   (32/128/256/512MiB). The highest supported H264/AAC choice within that quality
   is selected; unsupported formats are rejected rather than silently transcoded.
5. Preview and review the video ID, creator, title, description and actual quality.
   A private confirmation expires after20minutes and binds this metadata/resource
   snapshot to the authenticated administrator and library. Changing source,
   destination or resource choices requires another preview.
6. Confirm your rights/permission record and queue the reviewed video. The browser
   submits the opaque receipt, not arbitrary format IDs, paths or executable flags.
7. Monitor progress/history in the shared queue; cancel/retry there. Jobs survive
   restart, retain the original policy and deduplicate canonical video IDs per library.
8. After successful inspection/publication, normal scanning exposes a generic video
   for browsing/playback. Creator/channel IDs, original title, description, source
   identity and rights provenance persist. Locked NFO metadata prevents unrelated
   fuzzy movie identification; administrator corrections are retained on recovery.

One worker and three total attempts apply. Temporary-network retry backoff is
optional. Each attempt has a ten-minute deadline, a4MiB/s downloader limit, bounded
wire/staging/metadata output and a private disk-capacity reservation. Large selected
budgets require corresponding staging capacity; this is separate from, and does
not change, production backup safeguards. Unsupported/auth-required/DRM/live
sources fail without bypasses. Thumbnails/subtitles, general format support and
independent online creator-metadata enrichment remain subsequent requirements.

## Implementation and security

The pinned yt-dlp executable performs extraction and native HTTPS downloads. Its
bundled EJS solver uses the reviewed Node24.22.1 executable with Node permissions;
remote components/plugins, ambient configuration, cookies and arbitrary runtimes
are disabled. The official Node archive, ELF executable and license are checksum-
pinned in packaging/node/release.json, embedded into the server and shipped with
upstream notices. No ambient Node installation or frontend build is needed by users.
The pinned upstream yt-dlp YouTube extractor was also consulted to preserve its
nullable audio-only height schema, with a specific parser regression test. The
upstream yt-dlp FFmpegMergerPP implementation was consulted for stream-copy
mapping; Castlumora performs a stricter, local-only bounded mux using its existing
configured encoder. Upstream yt-dlp source/licenses remain bundled; Castlumora's
GPL license, inherited notices and history remain intact. No extraction engine is
reimplemented or copied wholesale from youtube-dl.

Every extraction/download connection still passes the existing authenticated
CONNECT tunnel, fresh whole-answer public-address validation and normal HTTPS
certificate checks. URLs are passed on stdin; source/proxy secrets are not placed
in process arguments, public responses or logs. Only numeric reviewed format IDs
and fixed private output names reach the downloader. Raw info JSON stays private
and is deleted with staging; it is never published into a library.

Fragmented H264/AAC inputs are allowed only at the private remux boundary. Their
container/data references are checked first; FFmpeg receives local inputs, file-only
protocols, disabled external references, bounded allocations and a deadline. The
final file must pass the existing strict nonfragmented container, native FFprobe,
actual frame-decode, size, quality and hash checks before atomic no-overwrite library
publication. The32MiB direct-import policy is unchanged; larger limits apply only
to validated YouTube job snapshots. Completion wins cancellation after commit.

Queue ownership is acquired before startup reclamation of explicitly marked,
regenerable abandoned YouTube attempts. Unknown/unmarked directories and symlink
targets are preserved. Source/administrator/library permissions are rechecked at
execution/publication; a receipt cannot be transferred to another account/library.

## Verification and honest external-source limitation

Focused tests cover policy selection, expiry/administrator/library binding, queue
persistence/deduplication, actual native fragmented/muxed streams, cancellation,
playable media above the legacy32MiB limit, safe cleanup, creator NFO publication
and existing container/path regressions. An authenticated server integration test
uses real owned HTTPS media through the actual persistent queue, publication,
scanning and playback API. It deliberately substitutes the owned transport fixture
for YouTube extraction; it is not a real public YouTube success claim. Frontend
tests verify preview-before-submit, rights confirmation, safe text and invalidation.

The owner authorized https://youtu.be/4Xsn6oe-AEg. Its earlier actual managed preview
failed sourceNetwork. A separate diagnostic subsequently validated the configured
network proxy's numeric-address CONNECT and normal TLS handshake, but the authorized
watch page returned HTTP403. No proxy policy, cookies, source authentication or TLS
checks were changed. Real extraction/download of this video remains unverified.
Do not interpret owned-fixture/native tests or CI as proof of public-source access.

The opt-in actual-source test can be run where normal public egress and source
permission are available; failure remains failure, not a substituted pass:

```bash
export CASTLUMORA_TEST_YTDLP_BUNDLE=/path/to/reviewed/system-root
export CASTLUMORA_TEST_NODE_BUNDLE=/path/to/reviewed/system-root
export CASTLUMORA_TEST_AUTHORIZED_YOUTUBE_URL=https://youtu.be/4Xsn6oe-AEg
dotnet test tests/Jellyfin.Api.Tests -c Release \
  --filter FullyQualifiedName~AuthorizedExternalSource_UsesManagedRuntimeAndNormalNetworkPolicy
```

Keep failure logs private and share fixed error codes/job IDs. Report source access
restrictions honestly; do not provide passwords/cookies or disable security controls.

## API and next milestones

Elevated administrator routes: POST Castlumora/Imports/YouTube/Preview and
POST Castlumora/Imports/Jobs/YouTube. Queue/status/cancel/retry remain the existing
routes. Preview receipts are private server-side JSON; durable version1 jobs add an
optional validated YouTube snapshot. Older direct jobs remain valid. Downgrading to
a build predating YouTube jobs cannot execute the new source type; preserve journals
and use the newer build rather than deleting state.

Next: verify actual authorized external extraction/download when permitted, add
thumbnail/subtitle/provenance enrichment, then playlist/channel/bulk management and
synchronization. Independent metadata identification for existing files remains a
separate subsystem. All remaining Castlumora1.0 requirements stay in scope.
