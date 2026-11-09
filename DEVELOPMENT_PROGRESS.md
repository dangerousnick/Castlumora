# Castlumora Web Phase 1.3 development progress

Branch: `development/castlumora-web-1.3`; PR targets `main`. Base main: `b06078bb7c6b280ea799ebf0737e6b9c3485a828` (contains Phase 1 branding and Phase 1.1 version/project documentation).

Permanent source of truth: [docs/CASTLUMORA_PRODUCT_REQUIREMENTS.md](docs/CASTLUMORA_PRODUCT_REQUIREMENTS.md). Remaining product vision: [PRODUCT_ROADMAP.md](PRODUCT_ROADMAP.md). Actual coverage: [FEATURE_PARITY.md](FEATURE_PARITY.md).

## Current checkpoint — native DVR recorded-media lifecycle (Phase 1.4)

- **Last completed milestone:** native recording refresh and distinct library-only removal/permanent file deletion; recordings API retains the requested server-calculated CanDelete field. Expected FFmpeg stop/duration exits follow the normal cancellation path after the owned process exits; unsolicited failures remain errors.
- **Branch:** `development/castlumora-web-1.3`, PR #3 targeting main. This section belongs to the DVR implementation commit; the exact published SHA is recorded in the next checkpoint entry after push. Previous published checkpoint: `ec93b8d86ad53c1d931bc01b2767ea4305355801`.
- **Verification:** Frontend typecheck/lint/production build and 48/48 tests passed. Release LiveTV library build passed with zero warnings/errors; LiveTV suite 81/81 passed on Linux. Real Chromium workflow passed against an isolated actual backend and local owned M3U/XMLTV media: schedule, cancel future recording, record to completion, play output, deny ordinary-viewer mutations, retain file on library-only removal, rediscover on scan, physically delete with per-item permission, preserve original source. Fixture settings/policy restored afterwards. Initial failures exposed an unindexed new library, XMLTV cache and stripped permission field; corrected and rerun. See docs/CASTLUMORA_DVR.md.
- **Unfinished:** active-recording stop needs its own browser coverage; real tuners/GPU/long-duration recording, channel groups and DVR storage quotas remain unverified or unimplemented. The master specification remains incomplete; metadata provider/importer, account/profile separation, personalization and extended platform work remain required.
- **Next development task:** add and verify explicit active-recording Stop behavior and partial-file retention using the owned real-time synthetic source; publish before moving to source-aware local creator metadata integration. Preserve all existing preview assets.
- **Usage status:** Not accessible. No reliable remaining five-hour/reset data is exposed.
- **Recovery:** inspect status/log before fetching and fast-forwarding this branch; preserve unfinished work. Use the guarded DVR fixture helpers only on the explicitly marked disposable loopback root. Do not target production. Existing read-only Actions workflow generates intermediate previews after source pushes; a preview does not complete Phase 1.4. Continue milestones and rebuild the final preview from the final tested commit. No main merge or deployment.

## Latest recoverable checkpoint — Phase 1.4 runnable preview delivered

- **Last completed milestone:** self-contained Linux x64 package with compiled integrated Castlumora Web, native fresh setup, isolated launcher/temp storage, licenses/install checklist and read-only GitHub Actions build/extract/browser/artifact delivery.
- **Latest published tested build commit:** `cc59df91a8c06364d6ca1e6944ba938031754bd7`. Documentation-only checkpoints may follow this immutable application/build commit; inspect `git log` and remote branch before continuing.
- **Branch / review:** `development/castlumora-web-1.3`, PR https://github.com/dangerousnick/Castlumora/pull/3, targets main, not merged.
- **Download:** https://github.com/dangerousnick/Castlumora/actions/runs/42702481467/artifacts/13712785582 (GitHub sign-in; retained 30 days). Run: https://github.com/dangerousnick/Castlumora/actions/runs/42702481467 . Outer ZIP contains archive/checksum/manifest/install/checklist/CI report.
- **Archive:** `Castlumora-1.0-dev-cc59df91a8c0-linux-x64.tar.gz`; SHA-256 `57dd13e5ad925d3f9c9baa0bfb0779f505f50e3c17e4a63dca984f5e709ff89a`.
- **Verification:** local frontend 44 tests/typecheck/lint/build; Release publish; local extracted-package fresh setup/security/logout and restart/library create/scan/play; all 13 broader package browser workflows across full/targeted runs. GitHub Ubuntu 24.04 workflow succeeded at every step, independently rebuilt/extracted and passed both fresh and restart/library/playback browser cases before artifact upload. Downloaded through the connected GitHub app, inspected safe ZIP paths, verified inner checksum and exact manifest. Full report: docs/verification-preview-1.4.md. Windows/Mint physical hardware, GPUs, real tuners and completed DVR lifecycle are not certified. Prior backend tests are explicitly historical, not falsely rerun.
- **Unfinished work:** no unfinished application changes; master specification remains incomplete. Mandatory yt-dlp importer and independent creator/YouTube metadata are absent. Full DVR lifecycle/storage, linked profiles/presets, personalization/notifications, extended creator/business/cloud/offline/plugin/native clients and advanced admin gaps remain tracked.
- **Next single task:** Priority A DVR lifecycle. Add a short authorized synthetic guide program on the marked loopback fixture, snapshot/restore its own guide/options, schedule through native UI, verify actual recording completion/file and playback, cancellation/removal semantics and permissions. Existing DefaultLiveTvService copies guide dates into new timers and only updates padding on single-timer edits. Existing DELETE LiveTv/Recordings removes the library entry with DeleteFileLocation=false; do not claim physical deletion or change that compatibility behavior without explicit design/tests. After DVR, continue account/profile/admin stabilization and mandatory source-aware metadata provider then safe importer, per permanent requirements.
- **Usage status:** **Not accessible**; no verified five-hour allowance/reset information is exposed. No background continuation or scheduled reset resume is promised.
- **Recovery:** read all five permanent specifications/progress files plus docs/CASTLUMORA_PHASE_1_4.md; inspect `git status` and preserve any new uncommitted work. Fetch origin, use this existing branch, and fast-forward only if clean (`git merge --ff-only origin/development/castlumora-web-1.3`). Never hard-reset completed work. Rebuild with `packaging/linux/build-preview.sh` from a clean exact commit, or rerun the existing successful Actions run to refresh an expired artifact. INSTALL_TEST_BUILD.md covers owner download/extraction/start, no Node/SDK needed on the test computer.

The workspace uploads.github.com credential returned 401; the empty unpublished draft was removed. Automatic approval review rejected a broad persistent release-write workflow; delivery was completed through a narrower contents-read-only artifact workflow instead. Workspace CLI Azure artifact download returned 403, but connected GitHub artifact download succeeded and the retrieved bytes passed checksum/manifest validation. These are documented tooling-route failures, not claims of missing published assets. No main merge, official Jellyfin modification, Coon Media access or production deployment occurred.

## Current continuation status

The master specification is **not complete**. Tested continuation work now includes real HLS/track/quality controls, permission-filtered category pagination/folder names, layout revision rollback, native administration, Live TV/DVR schedules/channel switching the native reviewed intro/outro skipper, registration/invitation workflows, native music/private playlists, and native photo/book/collection workflows. Historical foundation limits below are superseded by the continuation checkpoints at the end of this file. See those checkpoints for the current exact next steps and unverified features.

## Published milestones

1. `b30674348ab61176bc1193a819522e11177b5e3f`: independent React/TypeScript frontend, real API adapter, primary screens/player, permanent requirements and progress checkpoint; frontend checks and 15 tests passed.
2. `73b7960fdae6bb48af4f34722249cc3da743e480`: server presentation persistence, categories/preferences, editor, regression tests, roadmap/parity/spec/API docs and screenshots. API regression suite 164 passed/3 skipped; isolated API/executable builds passed. HTTP test execution was still pending at this checkpoint.
3. `7204fcfa248a8b1c9cbcb1aa131613dbe7b2411e`: HTTP test cancellation-token fix, verified authorization, legacy-root fallback and 16 passing frontend tests.
4. `f46230b5d738a227f286661e7805fbb640b0dbe9`: actual-server playback verification results, final reports/screenshots and bundled GPL/dependency notices.
5. Documentation merge incorporates concurrent main commit `7fae5086785978ac081113d364c5655e3f0b3a17`, preserving both complete requirement documents and explicitly tracking the built-in intro/outro skipper as planned. No runtime source changed in this merge. PR: https://github.com/dangerousnick/Castlumora/pull/3.

Continuation publications:

- `5ac61c1159eec98292a43d40f078f2f52ba09858`: built-in reviewed intro/outro detection and profile skip modes.
- `45b81ca2dcc38fe9801f71a6e847a259b0359f7d`: tested initially-disabled account provisioning.
- `559bcb95566bf3f8777a2958ae47a6714cb15e04`: invitation registration and pairing.
- `6206b4e5c4f7bfe102135bf57892baaad068ba6c`: native music/background queue/private playlists.

- `e1786a8f7bedd074e493e43d5e2e2fdc2961e2ac`: real HLS and track/quality/source/chapter/seek controls.
- `8541ce71c7568c1b1de7d2538fd26c4740405745`: permission-scoped category pages, layout rollback and native administration.
- `9f4bcb6be4091b2e08a3670d21d7abdd67346f08`: native guide, DVR schedules and live channel switching.

Publishing uses the connected GitHub API because the terminal GH_TOKEN is invalid. Connector-created commits have different metadata/SHAs from local checkpoints; each uploaded Git tree is verified against the corresponding local committed tree. The code is not regenerated during publication. Local/remote history is synchronized after publication.

## Implemented and verified foundation

- Independent dark purple responsive web client with existing castle branding, simple navigation, Surprise Me, keyboard/focus styles, loading/errors, no empty/identical home rows, and real media imagery when available.
- Server connection remembered; existing login/logout/public-profile suggestions; profile switching requires credentials. Tokens are tab-scoped, passwords never persisted. Existing server permissions remain authoritative.
- Actual permitted library retrieval, movie/series search and paging, details, seasons/episodes; separate Resume/NextUp rows and watchlist.
- Conservative direct MP4 browser profile, server-negotiated HLS foundation, audio/subtitle selection, start/progress/pause/stop reporting and cleanup of negotiated sessions only.
- Live channel/three-hour program guide screens and channel play links; existing Live TV/DVR backend retained. No real tuner configured or verified here.
- Additive schema v1 server configuration; administrator-only validated drafts/publishing with optimistic revisions, profile filtering and device overrides. Atomic JSON storage beneath server configuration; no database migration.
- Reusable category rules (metadata, folder IDs, manual inclusion/exclusion, types), no media movement/duplication; ordered home/category placement and profile visibility.
- Working administrator overview and layout editor: navigation/row ordering, theme colors, category/profile rules, device sizing/safe zones, preview, advanced JSON editing, separate save/publish.
- Existing-client root links preserve advanced administration and other media interfaces without assuming a particular legacy router version. Existing compatible Web distribution must remain installed and may require separate sign-in; choose Dashboard/Live TV there.
- Complete product spec, parity inventory, roadmap, frontend instructions, adapter/schema/persistence documentation and verification report. GPL/upstream copyright/contributors preserved; static distribution includes runtime license notices.

## Verification completed

- Frontend typecheck, lint, production build pass. 16 unit/API/component tests pass.
- Chromium isolated API/media fixtures: 2 pass; mobile overflow checked. Screenshots are labeled fixture screenshots.
- API regression suite: 164 pass, 3 Windows-specific skips, including 7 new store/policy cases.
- Full server integration suite: 130 pass, 3 existing skips, including real HTTP administrator/ordinary-user authorization, revision conflict and preference-isolation test.
- Full Linux server/reference build: passed, 0 errors, 215 warnings in inherited code; no warnings in new presentation files. Isolated API and executable builds also passed with 0 warnings/errors.
- Actual disposable loopback server and synthetic H264/AAC movie: Chromium login, browse, details, real playback and successful start/progress/stop requests pass (1 test). Normal FFmpeg 7.1.5 validation/probing used. No production data/server involved.
- LICENSE and CONTRIBUTORS.md byte-identical to main; diff check passes. See docs/verification-web-1.3.md for commands and limits.

## Known limits and unfinished work

- This is a vertical slice, not full frontend parity. Music/photos/books, advanced library/metadata/playlist controls, tuner/XMLTV/DVR settings, encoding/hardware/plugins/networking/tasks/backups and full account/device administration use the existing client/dashboard.
- Live tuner playback/channel transitions/DVR, hardware acceleration, cross-browser codecs, real audio/subtitle variants, HLS seek/resume timelines and long-running sessions need fixture/hardware testing. HLS code exists; passing direct MP4 playback does not certify transcoding.
- Category output is bounded to 48 items; full indexing/pagination and folder-name patterns remain future work. Banners/visibility/device row ordering currently use the advanced schema editor. Navigation icons/white-label artwork picker and revision history/rollback are not implemented.
- Native built-in intro/outro detection and skip/auto-skip controls, native TV/mobile renderers, plugin UI registry, recommendations/interests/follows/notifications, invitations/signup, presets, optional $0 resource tiers/payments, creator/OBS, private cloud/offline are roadmap features, not completed screens or contracts.
- Player/HLS lazy chunk exceeds Vite's 500 kB advisory (~602 kB minified/~189 kB gzip). Dependencies are locked; evaluate bundle optimization without removing required audio/subtitle support.
- Persistence lock is in-process; shared config storage across multiple server processes is unsupported. Include castlumora config files in backup/restore verification.

## Exact next steps for a new task

1. Fetch and check out this existing development branch. Read this file, the permanent requirements, parity inventory and verification report. Inspect current HEAD/PR before changing anything; do not repeat Phase 1 or rebuild the client from scratch.
2. Review and test this PR before any merge. No automatic merge/deployment is authorized. Keep the existing Web fallback.
3. Run `npm ci`, typecheck/lint/test/build under Castlumora.Web. For browser tests run Vite on loopback and follow documented synthetic/disposable-server instructions. Never use Coon Media.
4. Prioritize real HLS/remux/transcode seek/resume, multiple track and Live TV fixtures, then category pagination/folder-name rules and admin editor usability. Add tests for each supported claim and update parity/report accordingly.
5. Build future features in tested milestones on this branch or a separately authorized follow-up branch, commit/publish regularly, and update this file at each checkpoint. Preserve backend namespaces, APIs, databases, plugins, licenses and upstream history.

## Active continuation: complete the master specification

The owner requested completion beyond the initial vertical slice. Continue the existing branch and PR; the retained full specification remains authoritative. Do not call the platform complete while parity rows or product requirements remain unfinished. Publish tested milestones and update this checkpoint throughout.

Current milestone: real HLS/remux/seek/resume and track/quality controls, then server-backed category pagination/layout revision history, full administration and Live TV/DVR controls, built-in skipper, and remaining account/personalization/platform requirements. Hardware/native-device and real-tuner claims still require their corresponding fixtures/devices. No Coon Media access, automatic merge or deployment.

### Continuation checkpoint: playback controls

Real HLS, seeking, bitrate negotiation, English/French audio switching and external subtitle loading now pass against the disposable server using a 90-second synthetic multi-track/chapter fixture. Added quality/source/chapter/absolute-timeline/next-episode controls and corrected exact-zero resume handling. See docs/verification-web-continuation.md. Layout history/rollback and server-side paginated folder-name category rules are being implemented next; those backend changes are not yet marked verified.

### Continuation checkpoint: categories, history and native administration

Implemented permission-filtered server-side category pages (metadata, physical/configured folder names, manual OR selection, exclusions and totals). Explicit selections retain allowed-library roots. Added administrator-only published revision history and draft restoration with optimistic concurrency, plus featured/artwork title pickers and category controls.

Added native administration sections for library creation/options/scans, users/policies/passwords/devices, encoding, general/network settings, tasks/triggers/logs, backup actions, plugins/repositories/catalog and tuner/XMLTV/channel mapping. Forms preserve unknown inherited configuration fields and expose advanced JSON; existing dashboard remains available. These screens are implemented, but not every mutating operation is verified: real tuner/DVR, hardware settings, plugin installation, backup restoration and restart/shutdown need their own isolated fixtures.

Verification: frontend typecheck/lint/build and 22 tests pass; 2 fixture browser tests include publish/restore isolation. Three real-server browser cases cover direct playback, HLS/tracks and persisted encoding settings plus administration navigation. API tests: 168 pass, 3 Windows-specific skips. Extended presentation HTTP authorization test passes. Actual disposable-server category checks pass for genre/folder/manual matching, exclusions, pagination and restricted-library permissions. No production data or server involved.

This checkpoint supersedes earlier limits for category pagination/folder names, revision history and administration links. Remaining work includes complete DVR/Live TV workflows, other media types, native skipper, registration/invites, personalization/notifications, presets/resource plans, creator/cloud/offline/native-client/plugin UI requirements and corresponding verification. The complete specification is **not finished**.

Next: publish this tested checkpoint on the existing branch, then implement and test native Live TV/DVR workflows with synthetic M3U/XMLTV; proceed through remaining requirements without replacing existing work. Keep publishing tested milestones; no automatic merge/deployment.

### Continuation checkpoint: native Live TV and DVR

Added six-hour guide date/channel selection and program pagination, recording options using server defaults, single/series schedule creation and editing, cancellation, recordings pagination/removal controls and live player channel switching. Existing server permissions remain authoritative; management controls respect the profile policy. Fixed new-timer defaults carrying a derived ID so creation uses POST /Timers rather than attempting to update a nonexistent timer.

Verified on the disposable loopback server with synthetic two-channel M3U and six-program-per-channel XMLTV: create single schedule, persist/edit padding, cancel; create/cancel series rule; play Castle One, switch to Castle Two and confirm own live-stream cleanup on switch/exit. Two actual-server Chromium tests pass. Frontend 25 tests, typecheck/lint/build pass. Latest normal API analyzer build: 0 warnings/errors. No production deployment or access.

Recording completion/files/deletion, long-duration DVR/timeshift, physical tuners, large channel catalogs/groups and hardware compatibility still need verification/work. browser/setup-live-fixture.py retains a guarded fixture setup helper (loopback, explicit disposable marker, refuses existing tuner/providers). Next: complete recording lifecycle fixture and the retained native skipper requirements, then other media/account/platform features. Full specification remains unfinished; do not merge automatically.

### Continuation checkpoint: native skipper

Implemented a built-in optional repeated-audio provider using configured FFmpeg, same-season corroboration and silence/flat-fingerprint guards. Detection yields review-only suggestions. Added central revisioned review state, manual correction/approval/removal, protected post-credit boundary, file-signature checks and profile default/manual/automatic/disabled modes. The existing scheduled extraction task includes the native provider; bounded single-process decoding defers during playback and kills only its own process on cancellation. Existing providers and the MediaSegments API remain intact. Deferred session-manager resolution avoids a startup dependency cycle caught by actual-server testing.

Native admin review/settings and player controls are implemented. Actual synthetic FFmpeg detection found intro 10–40s/outro 145–169.5s; suggestions were absent from central intervals until approval, and protected post-credit content remained outside the outro. Chromium manual/automatic/disabled playback modes pass. Guarded generator/verifier scripts are retained. See docs/CASTLUMORA_SKIPPER.md for exact limits, contracts and reproduction.

Latest verified checks before this checkpoint: 28 frontend tests; typecheck/lint/build; 177 API tests with 3 Windows-only skips; normal API build 0 warnings/errors; isolated executable build 0 errors; HTTP authorization/profile-isolation test passes. Complete integration suite: 130 pass, 3 existing skips. Actual-server browser cases for movie/HLS, administration, DVR, live transitions and native skipping pass; administrator review UI also passes. Both editor/mobile fixture browser cases pass. Native TV/mobile renderers/device behavior and broad real-series detection accuracy remain unverified. Stale sessions from abruptly terminated clients conservatively defer analysis until cleared. Replacing media requires existing metadata/segment refresh to update central intervals.

Next after publishing this milestone: complete remaining music/photos/books/collection/playlist and recording lifecycle coverage; invitations/registration/profile/preset requirements; personalization and notification workflows; optional quotas/business/creator/cloud/offline/plugin/native-platform requirements. Preserve this work and the full requirements; do not claim complete master-spec parity or merge/deploy automatically.

### Continuation checkpoint: disabled account provisioning

Added an additive IUserManager overload for creating an initially disabled account. The existing method retains its behavior; custom managers without the new capability fail closed for disabled provisioning. The disabled permission is persisted before account creation is published. This supports safe public registration without a blank-password/initial-policy exposure window.

Verification: all 64 user-manager tests pass, including persisted disabled state and unchanged legacy creation. Full Linux server/reference build with normal analyzers passes: 0 errors, the same 215 inherited warnings. Registration/invitation services, protected/public endpoints and frontend forms are being developed next; their working-tree code is not yet marked published or verified end to end. Continue that work rather than recreating the existing application.

### Continuation checkpoint: registration and invitations

Implemented admin-enabled closed/open/invite-only registration, expiring/revocable invitation links with successful-use caps, server-owned non-administrator policy snapshots, explicit library permission pickers, private audit/recovery and bounded signup attempts. Raw tokens are returned once and stored as digests; passwords stay in the existing account manager. Durable reservations and disabled provisioning prevent quota races and blank-password/policy exposure. Schedule templates are rebound to the new account. The public endpoint ignores supplied policy/administrator fields. Added native Users deletion (with confirmation/self guard) for normal account management/recovery.

The signup frontend confirms the target server, strips the invitation from the URL, submits secrets only in the body and requires ordinary sign-in after creation. Paired addresses reconnect automatically without persisting passwords or bypassing authentication.

Verified: 32 frontend tests plus typecheck/lint/build; 192 API tests pass with 3 Windows-only skips; all 64 user-manager tests pass. Full normal Linux server/reference build: 0 errors, the same 215 inherited warnings. Latest isolated normal API build: 0 warnings/errors. The real HTTP signup test passes, including blank-password denial, policy isolation, role boundaries, caps and throttling. Actual Chromium invitation → restricted signup → normal sign-in → remembered pairing passes and restores its disposable configuration. Full integration suite: 131 pass, 3 existing skips. All 8 actual-server browser cases and both editor/mobile fixture cases pass. Details and exact commands: docs/CASTLUMORA_REGISTRATION.md.

This does not complete the master specification. Next: finish native music/photos/books/collections/playlists and recording lifecycle coverage, then linked account/viewing-profile presets, personalization/notifications and optional quotas/creator/cloud/offline/plugin/native-platform requirements. Continue from these commits; keep the full requirements, legacy functionality, GPL attribution and castle assets. Do not merge/deploy automatically or contact Coon Media.

### Continuation checkpoint: native music and playlists

Added native albums/artists/tracks/search/paging, artist/album details, a queue that survives navigation, audio-only negotiation with direct/MP3-compatible playback, seeking, repeat/shuffle, persistent queue volume and own-session cleanup/progress reporting. Video playback clears background music. Added private playlist creation, permission-aware editing, track search/add, server entry-ID reorder/removal, rename without sharing-field replacement and deletion while retaining media.

Frontend typecheck/lint/build and 37 tests pass. Actual disposable-server music workflow passes: artist → album, playback/seek/background navigation/next track, compatible MP3, pause/stop and complete private playlist editing. Retained a guarded synthetic tagged-MP3 generator/indexer. Video coexistence passes: starting the movie stops only the background music session. All 10 actual-server browser cases and both editor/mobile fixture cases pass. See docs/CASTLUMORA_MUSIC.md.

Remaining: photo/book/collection screens and recording lifecycle, richer music metadata/lyrics/sharing/scale coverage, linked profile/preset and personalization requirements, optional resource/creator/cloud/offline/plugin/native-platform work. Continue existing code; no main merge or production/Coon Media deployment.


### Continuation checkpoint: photos, books and collections

Added native photo/album shelves, image navigation/zoom/rotation/slideshow; books and authenticated PDF/EPUB reading; profile-specific atomic reading positions/themes/font preferences; native collection creation, media add/remove and deletion retaining media. Fixed native administrator photo-library creation to use supported `homevideos` plus `EnablePhotos`. EPUB is sanitized before rendering, scripting disabled, archive sizes/path traversal bounded and external metadata resources rejected. PDF uses a bundled local worker. Extended distributed dependency notices without changing GPL/upstream attribution.

Frontend typecheck/lint/build and 41 tests pass. Normal API/executable builds pass with zero warnings/errors. API suite: 195 pass, 3 Windows-specific skips, including inaccessible/non-book denial, claims-owned persistence and submitted foreign profile-ID isolation. All three actual-server shelf cases pass on synthetic photos/two-page PDF/two-chapter EPUB and collections; reader positions survive reload and collection deletion retains its movie. See docs/CASTLUMORA_READER_AND_SHELVES.md and browser/setup-shelf-fixture.py. The latter requires loopback, a disposable marker and matching actual server data directory. A failed initial synthetic photo library retained its invalid identity when recreated with the same name; the final uniquely named fixture is created with the correct supported type from the start. No source workaround or production data change was needed.

Exact next steps: complete actual DVR recording lifecycle/playback/deletion and duration coverage; finish linked account/viewing-profile and editable kids/adult presets; implement interests/follows/local recommendations and profile notifications; then editable onboarding/resource presets, optional $0 quotas, creator/OBS, private cloud/offline, declarative plugin UI and native device requirements from the permanent specification. Rich music lyrics/metadata/sharing, comics/other book conversion, annotations/search/offline reading and broader real-library/device compatibility remain unfinished. Keep the existing implementation and castle assets, preserve GPL and legacy behavior, publish tested milestones and do not merge/deploy automatically or contact Coon Media.

Full browser regression after this milestone: **15 passed in one run** (2 editor/mobile API fixtures plus 13 actual disposable-server cases covering movie/HLS, administration, DVR schedules, live switching, skipper, signup/pairing, music/private playlists, video/audio coexistence and shelves/reader/collections).


### Runtime validator dependency checkpoint

Published shelf/reader milestone: `07f62f88ff8f3d35bed3dc13ec5443e2c4c1ddda` on the existing development branch, with an exact match between the tested local Git tree and the GitHub tree. PR #3 remains open against main, with no automatic merge or deployment.

A production-dependency audit then identified GHSA-mrhx-c7xc-m3w5 in inherited Zod 3.25.76. Updated to pinned Zod 4.6.7; `partialRecord` preserves the existing optional device-override contract (plain Zod 4 enum records would require all four device keys). Existing schema/default/unknown-reference tests, typecheck/lint/build and all 41 frontend tests pass; `npm audit --omit=dev` reports zero vulnerabilities. Dependency license notice updated. The first browser attempt used a stale Vite prebundle after the dependency major-version change and failed before login; restarting the development server with `npm run dev -- --force` rebuilt its cache. After cache rebuilding, 14 of 15 browser cases passed in the full run; the reader exceeded its five-second cold-module assertion while still loading. The assertion now allows a bounded 20 seconds and the complete PDF/EPUB resume case passes on rerun. All 15 workflows therefore pass across the full/targeted runs; the preceding shelf milestone also passed all 15 in one run. The earlier 195 API tests/3 platform skips remain valid because this dependency update changes only frontend validation.

For the next DVR milestone: extend the guarded loopback XMLTV fixture with a short synthetic program, snapshot/restore only its own guide and Live TV options, record through the native UI, verify recording appearance/playback/deletion and retain the original movie. `src/Jellyfin.LiveTv/DefaultLiveTvService.cs` copies program start/end into a newly created timer and existing single-timer updates only change padding; do not assume posting arbitrary edited dates shortens a guide-backed recording. Existing scheduling tests cover padding/cancel, not completed recording lifecycle. Guard all fixture mutations by marker, loopback URL and actual System/Info.ProgramDataPath. Keep config/credentials outside any served media directory and never target Coon Media.

## Phase 1.4 active milestone: installable Linux development preview

Main documentation through `68fd686d43` was merged without overwriting development files. Retained the Phase 1.4 addendum and mandatory importer/provider requirements. Usage status: **Not accessible** (no reliable account five-hour allowance/reset meter in exposed tools). Current branch remains development/castlumora-web-1.3, PR #3; latest published functional commit at start: `c98ee732eb7a6414ec5e294237b137fd654a6008`.

Completion for the next single task: reproducible Linux x64 server + compiled integrated web archive, fresh setup and isolated launcher, instructions/checklist/licenses/checksum/source SHA; actual extracted-package smoke tests; upload and verify a GitHub prerelease download; publish code and evidence. Do not start the importer or unrelated features before packaging is completed or specifically blocked. Legacy admin gaps and unavailable importer/provider must be disclosed. CLI GitHub authentication now returns 200 and repository push permission; repository Actions-settings read is denied, so prerelease delivery will be checked separately. No Coon Media or production data.

Packaging source checkpoint: native setup integrated, optional legacy-web path isolated, launcher/build/license collection scripts and owner installation/checklist prepared. Frontend typecheck/lint and 44 tests pass, including setup endpoint ordering, no password storage, interruption recovery and integrated reverse-proxy server discovery. Shell syntax/Python compile pass. Actual Release publish, extracted-package smoke and prerelease upload remain pending; do not call this source checkpoint final acceptance. Documentation merge published as `56e5e68f3d`.

Preliminary extracted preview smoke passed fresh setup → administrator login → native admin → logout and confirmed unauthenticated startup writes are denied after setup. It ran with DOTNET_ROOT unset and PATH excluding the SDK. First launch correctly rejected insufficient disk space; removed only re-downloadable NuGet archives, reproducible Debug outputs and duplicate package staging, then normal startup passed without lowering the 2 GiB requirement. Packaging review corrected tar timestamp capture outside the repository, retained framework/font licenses and isolated TMPDIR. Final archive rebuild and full packaged media regression are next; the first archive is not the release deliverable.

Delivery fallback: local final archive `/tmp/castlumora-preview-build-2/Castlumora-1.0-dev-8d591a4d58cc-linux-x64.tar.gz` passed checksum and actual package smoke tests. Workspace release uploads to uploads.github.com fail HTTP 401 while repository publishing/API release creation succeed; a tiny API-host upload returns 404. Draft release ID 439656187 has no assets and is not a usable download. Do not claim delivery yet. Automatic approval review rejected a persistent workflow with release-write permission on PR updates as too broad; replaced it with a contents-read-only workflow limited to PR #3/this development branch (plus explicit manual dispatch), no release publishing, and standard Actions artifact delivery. Actual workflow execution and artifact download verification are next before packaging completion. Usage: Not accessible; no unrelated feature started.

Read-only artifact workflow published successfully as `255b7616d9d53dc7da720db7bb30296d1508b5c9`; its first run started. Updated the install guide to explicitly explain Actions sign-in, outer ZIP extraction, inner archive checksum/extraction and 30-day retention. Enabled cancellation only of superseded preview runs so the corrected guide is bundled without wasting parallel builds. Current application code remains identical to the locally smoke-tested `8d591a4d58cc705de1668fb7dc63a0b013cc35e4`; only delivery/docs/browser harness changes follow it. Final successful run/artifact download verification still pending.
