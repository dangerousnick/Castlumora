# Castlumora Web Phase 1.3 development progress

Branch: `development/castlumora-web-1.3`; PR targets `main`. Base main: `b06078bb7c6b280ea799ebf0737e6b9c3485a828` (contains Phase 1 branding and Phase 1.1 version/project documentation).

Permanent source of truth: [docs/CASTLUMORA_PRODUCT_REQUIREMENTS.md](docs/CASTLUMORA_PRODUCT_REQUIREMENTS.md). Remaining product vision: [PRODUCT_ROADMAP.md](PRODUCT_ROADMAP.md). Actual coverage: [FEATURE_PARITY.md](FEATURE_PARITY.md).

## Published milestones

1. `b30674348ab61176bc1193a819522e11177b5e3f`: independent React/TypeScript frontend, real API adapter, primary screens/player, permanent requirements and progress checkpoint; frontend checks and 15 tests passed.
2. `73b7960fdae6bb48af4f34722249cc3da743e480`: server presentation persistence, categories/preferences, editor, regression tests, roadmap/parity/spec/API docs and screenshots. API regression suite 164 passed/3 skipped; isolated API/executable builds passed. HTTP test execution was still pending at this checkpoint.
3. `7204fcfa248a8b1c9cbcb1aa131613dbe7b2411e`: HTTP test cancellation-token fix, verified authorization, legacy-root fallback and 16 passing frontend tests.
4. `f46230b5d738a227f286661e7805fbb640b0dbe9`: actual-server playback verification results, final reports/screenshots and bundled GPL/dependency notices.
5. Documentation merge incorporates concurrent main commit `7fae5086785978ac081113d364c5655e3f0b3a17`, preserving both complete requirement documents and explicitly tracking the built-in intro/outro skipper as planned. No runtime source changed in this merge. PR: https://github.com/dangerousnick/Castlumora/pull/3.

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
