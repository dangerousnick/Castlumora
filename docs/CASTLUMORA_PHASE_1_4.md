# Castlumora Phase 1.4 — retained master task

## Authoritative Linux-only server policy — 2026-11-13

Castlumora1.0 targets Linux servers only: Mint22.3/newer compatible versions, Ubuntu
LTS, Debian, Docker on Linux and compatible Linux NAS. macOS and Windows server
support are deferred indefinitely, including CI/release/packaging acceptance.
Preserve inherited cross-platform code and clients where practical; no unnecessary
platform-removal refactoring. Linux security, integrity and functionality remain
mandatory. This supersedes older platform requirements below; all original1.0
features and the future1.0→2.0 migration plan remain in scope. No backend rewrite.

Use focused feature tests, full backend validation for substantial application
increments, relevant frontend/native checks, and lightweight documentation-only
validation. Classify the whole unpublished range, fail closed on mixed/unknown
changes, and preserve unique security/backup/migration/regression coverage. Do not
repeatedly rebuild unchanged source or create needless documentation checkpoints.


Version: Castlumora 1.0 (development). Repository: dangerousnick/Castlumora. Base: main. Continue development/castlumora-web-1.3 and PR #3; never restart existing work or automatically merge/deploy. This addendum retains the owner's Phase 1.4 requirements alongside the complete earlier product and import/creator metadata specifications. Requirements are not implementation claims.

## Platform strategy update — 2026-11-11 (authoritative)

Castlumora is Linux-first with macOS supported where verified. Required targets are
Linux Mint22.3/Ubuntu-compatible distributions, Debian and other supported Linux,
Docker/Linux NAS deployments, and macOS. Windows (including Windows Server) is
**deferred for Phase1.4 and stable1.0 acceptance**, with inherited compatibility
code preserved and advisory CI retained. Windows-only failures do not block
Linux/macOS development or releases. Investigate any Windows failure that suggests
a shared-code or security defect; do not remove tests or intentionally break it.

This owner-approved platform change supersedes conflicting historical Windows
release prerequisites below. **Every previously agreed Castlumora1.0 feature
requirement remains mandatory.** No feature is moved to2.0 by this platform change.
CI success is not installation/runtime certification; see
[platform evidence and required checks](CASTLUMORA_PLATFORM_SUPPORT.md).

## Product and compatibility

Build an independent, free/open-source self-hosted platform for Family & Friends, Content Creators and Businesses. These are editable configuration presets, not editions; every feature is available in all modes, with no mandatory payments or feature paywalls.

Preserve movies/TV/music/photos/books/other media, libraries/scanning/metadata/artwork/collections/playlists, direct play/stream/remux/transcoding, FFmpeg/hardware acceleration, tracks/subtitles/chapters/trickplay/progress, Live TV/IPTV/M3U/XMLTV/EPG/tuners/DVR, users/parental policies/authentication, plugins/repositories/configuration, networking/discovery/remote access, dashboard/logs/tasks/diagnostics/backups and compatible databases/configuration/API/migration behavior. Keep legacy administration access until parity is tested. Preserve GPL/copyright/attribution/history and compatibility-sensitive Jellyfin/Emby/MediaBrowser identifiers.

## Complete web workflows

Continue React/TypeScript and dark purple castle identity. Real backend workflows must cover Home/Browse/My List/Search; movies/series/seasons/episodes; music/photos/books/collections; player controls; guide/recordings; profiles/accounts; administration/configuration; metadata/imports/downloads; categories and centrally controlled layouts. No placeholder production functionality.

## Mandatory built-in importing and metadata

The core yt-dlp subsystem and separate native YouTube metadata provider are mandatory production targets, not external-plugin requirements. See CASTLUMORA_IMPORT_AND_CREATOR_METADATA.md for the full retained specification.

Importer: authorized single/multiple URL, playlist/channel, bulk archive/library migration; destination libraries; quality/format/subtitle/thumbnail choices; previews/progress/history/queues; pause/cancel/retry; stable-ID/checksum deduplication; opt-in scheduled source synchronization; bandwidth/concurrency/storage controls; supported interruption recovery; scan only successful imports. Pin/verify/manage/update executable/dependencies safely with useful health/error reporting. Respect rights/permissions, source terms and DRM; no unauthorized-access/security bypass, cookie harvesting or silent credential use. Retain source attribution/provenance and rights records for family curation, owner-authorized creator archives and licensed business media.

Metadata independently identifies existing local files without redownload: canonical video/channel IDs, handles/names, original titles/descriptions/publication dates/duration, permitted artwork, playlist/channel relationships, language/subtitle metadata, source references and .info.json sidecars. Support manual overrides/locks, bulk repair, provider enable/disable/priority, refresh/cache and legitimate offline fallback. Reliable IDs/source metadata and creator identities outrank fuzzy film/TV titles. Respect explicitly chosen types; show confidence and require confirmation for ambiguity. Never turn creator names such as Unspeakable/MrBeast into unrelated movies/shows. Improve ordinary title/naming/alternate-title/punctuation matching without weakening confidence safeguards.

Native admin Media → Imports & Sources: add/preview URLs, rights confirmation, destinations/quality/options, progress/queues/schedules/cancel/retry/history/errors. Media → Metadata Management: provider order/settings, creator matches/uncertain review, title/description/artwork editing/locks, bulk refresh/repair and duplicates. Persist settings server-side; enforce authorization and protect SSRF/private addresses/redirects, path traversal, shell injection, malicious filenames/untrusted metadata, secrets and resource exhaustion. Test legitimate single/bulk/recovery/dedup/conflicting titles/overrides/unauthorized requests/unsafe URLs/existing-local identification.

## Remaining platform features

- Live TV/DVR: channel lists/logos/groups, sources/XMLTV/guide/details, live play/switch, schedule/cancel, actual recording completion/playback/deletion/organization, storage limits and recording permissions. Test complete workflows; retain tuners/FFmpeg.
- Native skipper: recurring intro/outro detection; central timestamps; manual/automatic/per-profile skip; manual correction/admin review/override; scheduled bounded detection; Next Episode; post-credit protection; representative-media accuracy; browser/TV/mobile behavior without mandatory plugin.
- Server UI: navigation/rows/categories/featured banners/themes/backgrounds/artwork/profile visibility/device layouts/branding/safe plugin definitions. Visual ordering, TV/desktop/tablet/mobile previews and banner safe zones, navigation/theme editing, central versioned draft/publish/history/rollback with graceful unsupported-component fallback.
- Categories: cross-library virtual folder/metadata/manual inclusion/exclusion rules; multiple categories/title, names/colors/icons/emoji/banner/artwork/profile restrictions/order. No actual file movement/duplication. Examples include Kids/Teen/Princess/Animated/Disney/Marvel/holidays/Anime/creators.
- Identity: separate accounts and multiple viewing profiles; kids/adult presets; isolated progress/preferences/restrictions; secure account creation; admin-issued expiring/revocable/capped-success invitations, optional self-signup, remembered pairing, device/session management. Preserve authentication/security/admin control.
- Personalization: Continue Watching, Up Next/new episodes, watchlists, separate followed shows/interests/history-based recommendations/diverse discovery, notifications for episodes/seasons/recordings, notification center, Surprise Me filters/reroll. Hide resume via options without deleting progress/history.
- Full native administration: health, libraries/metadata/imports, Live TV/DVR, playback/hardware, users/profiles/policies, plugins, UI/branding, resources/tiers, networking, tasks/logs/diagnostics, backup/restore; retain legacy fallback for gaps.
- Optional owner tiers: concurrent streams, per-stream/aggregate bandwidth, transcode capacity, DVR/cloud storage, users/devices, subscriptions/payment processing. Every tier may cost $0; software never requires payment.
- Creator/business: OBS ingest, owned uploads/pages/channels/archive migration, subscriptions/donations/optional ads/analytics, white-label managed offerings and scaling, implemented/tested incrementally. Never claim unfinished services production-ready.
- Private per-user cloud/DVR storage, quotas, permission-controlled offline downloads, uploads/access/privacy/recovery/backups; honor DRM/rights. Safe declarative plugin UI and future native TV/mobile layouts/input/playback.
- Identity audit: replace unnecessary visible upstream names/descriptions/logos/startup/dashboard/promotional/docs/installation/web/error references. Establish Castlumora solution/workspace/packaging/application/release/path/distribution identity deliberately after dependency/compatibility review; no blind technical rename.

## Execution and recovery rules

Priority A: stabilize existing web/DVR/profiles/admin/integrations. B: native metadata and safe importer/queue/admin/library integration. C: skipper/layouts/categories/discovery/notifications/matching. D: packaging/branding independence. E: resource/creator/business/cloud/offline/plugin/native clients. Exception: reserve capacity for the mandatory runnable package before spending the window expanding features.

Finish one coherent milestone before another. Define completion before coding; run affected builds/tests/API/browser/media/authorization/migration/dependency checks, fix regressions, update DEVELOPMENT_PROGRESS.md/FEATURE_PARITY.md/PRODUCT_ROADMAP.md, commit and publish. Sync relevant main documentation without discarding development work. Never claim unrun tests passed. Preserve all unfinished requirements.

Before each new task, check reliable five-hour usage/reset/execution data if exposed. Otherwise explicitly record “Not accessible”; never invent percentages/reset times. If allowance is insufficient/uncertain or a limit warning occurs, stop starting features, finish the smallest safe unit, publish/checkpoint or safely preserve unfinished files. Do not repeatedly retry usage-limited operations or claim background continuation/reset waiting without actual supported capability. A new session reads specifications/status/branch/history and resumes the first unfinished milestone without rebuilding completed work.

Recovery status must record last completed feature, exact latest published commit, branch, tests/failures/skips, unfinished files, exact next task, usage status and safe recovery commands. Keep PR #3 open, no main merge. A task is complete only when tested/committed/published or a specific blocker is documented.

## Mandatory runnable Linux test build

Deliver an actual GitHub Actions artifact or GitHub prerelease asset named Castlumora 1.0 Development Preview for Linux Mint 22.3/Ubuntu-compatible x86-64. A source archive, successful compile or development server alone is insufficient. Use the existing tested branch; no main merge required.

Package the server executable/dependencies, compiled Castlumora Web served by that server, purple castle logos/icons/static assets, fresh runtime configuration, GPL/third-party notices, startup/initialization instructions and access or explicit installation instructions for required legacy administrative web assets. A fresh installation must not need cloning, Node or frontend compilation. Package .NET or document required runtime/dependencies; document compatible FFmpeg and native Linux dependencies.

Provide exact version/source commit, SHA-256 checksum, reproducible build workflow, known gaps, verification report, testing/bug checklist and verified GitHub download location. Do not label production-stable or claim an upload exists before it is uploaded and checked.

Create INSTALL_TEST_BUILD.md: download/checksum/extract or graphical installation, dependencies, start/URL/initial setup, libraries/users/profiles, media/features/advanced admin, stop/restart/troubleshoot/log collection and removal. Prefer practical Linux Mint graphical workflows; isolate preview data from existing installations.

Smoke-test the actual extracted package: launch/config creation/initial setup/restart; integrated branding/home/navigation/login/logout/permissions; library creation/scanning/movies/TV/metadata/artwork/categories/collections; movie/direct/HLS/transcode/progress/resume/audio/subtitles; Live TV configuration/channels/guide/play/recording management/lifecycle where fixtures permit; skipper controls, layout persistence/admin publication/profile rules; metadata/import if implemented; libraries/encoding/hardware settings/Live TV/plugins/users/logs/tasks access. Distinguish automated/manual/unverified; never claim GPU testing without hardware. Document missing importer/provider explicitly.

Feature checklist must give each feature's location, steps, expected behavior, limitations and verification type; include bug report steps/logs/environment/screenshots. First preview may have incomplete planned features, but must expose completed working code for realistic owner evaluation. Link artifact/report/exact tested SHA in PR #3. Preserve requirements and publish more previews as features mature.

Final acceptance requires the owner can download, install/extract, start, open the integrated UI, configure accounts/libraries, browse/play and evaluate implemented administration/Live TV/categories/skipper and report defects. A blocked required step must have a specific blocker, not a full-success claim. If packaging cannot finish, preserve it as the highest-priority next task. No Coon Media or official Jellyfin repository modification/deployment.
