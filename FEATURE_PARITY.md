# Feature parity inventory — Phase 1.3

“Preserved” means this change does not alter the inherited implementation; it does **not** mean every backend feature was exercised in this workspace. See `docs/verification-web-1.3.md` and DEVELOPMENT_PROGRESS.md for actual executed checks. Legacy fallback requires the existing compatible Web distribution at `<server-base>/web/`; it may require its own login. No fallback is removed.

| Feature group | Backend availability | Castlumora Web | Legacy fallback | Automated coverage | Manual/real verification and outstanding work |
| --- | --- | --- | --- | --- | --- |
| Server connection, base URL | Preserved | Pair once; retain address/device | Existing server setup | URL/base-path adapter tests | Loopback connection/authentication verified; real reverse-proxy deployment pending |
| Accounts/passwords/auth/sessions | Preserved | Sign in/out; public profile suggestions; tab-scoped token | Full account/device management | API fixtures + server auth integration tests | No passwordless profile bypass; refresh/revocation UI pending |
| Parental controls and permissions | Preserved | User-specific existing media endpoints; admin UI guard | Full policies | Server global-write denial tests | Full kids/adult policy matrix pending |
| Movies and series | Preserved | Search, libraries, paging, details, seasons/episodes | Existing client | Adapter, component, browser flows | Real loopback movie browse/detail verified; large library/performance validation pending |
| Music, photos, books, other types | Preserved; profile reader state added | Native music/photos/books, PDF/EPUB reader | Additional inherited formats retain fallback | Synthetic music/photo/book browser checks and reader access/isolation tests | Rich metadata/lyrics, comics/other book formats, offline and device/scale coverage pending |
| Continue Watching | Preserved | Resume playback; independent server-backed hide preference | Existing resume remains unchanged | Filtering, isolation/persistence, menu tests | Legacy clients do not consume exclusions |
| Up Next | Preserved | Existing NextUp API row | Full inherited interface | Adapter/fixture coverage | New-season notification/follows distinct and pending |
| Watchlists/follows/interests | Additive watchlist file; inherited favorites unchanged | Separate My List | Existing favorites/collections | Adapter, preference and browser checks | Follows/interests/notifications not implemented |
| Direct Play | Preserved | Browser H264/AAC MP4 profile and native video | Existing client | Negotiation/URL/unit + browser MP4 fixture | Actual loopback server + synthetic H264/AAC playback passed; see verification report |
| Direct Stream/remux/transcoding | Preserved FFmpeg pipeline | Server-negotiated HLS via hls.js/native HLS; explicit compatible retry | Full player/quality settings | Fixture negotiation; inherited backend tests | Full remux/transcode/browser codec matrix pending |
| Audio/subtitle selection | Preserved | Re-negotiate selected tracks; external VTT and server burn-in paths | Full inherited player | API/profile contracts | Multilingual/image/ASS fixtures and cross-browser validation pending |
| Playback state/progress/resume | Preserved | Start/progress/pause/stop; cleanup only own stream IDs | Full inherited player | Browser event flow; inherited API tests | HLS timeline/seek edge cases require broader testing |
| Chapters/trickplay/source/bitrate controls | Preserved | Not yet surfaced | Full inherited player | Existing backend suites only | New frontend controls pending |
| Live TV channels/EPG | Preserved | Channel list, logos, next-three-hour program descriptions, play links | Full guide/groups | Client fixture/basic endpoint coverage | Real tuner/M3U/XMLTV fixture testing pending; group filter/full guide pending |
| DVR/recordings/schedules | Preserved | Existing-client link | Full Live TV/DVR | Existing backend tests only | New scheduling/manage/delete controls pending |
| Libraries/folders/scans/monitoring | Preserved | Read-only library browse | Full legacy administration | Existing backend tests; new views adapter | New create/scan/provider editor pending |
| Metadata/artwork/collections/playlists | Existing APIs preserved | Native collection and private music playlist editing | Advanced inherited metadata/artwork interfaces | Actual collection and private-playlist lifecycle tests | Rich metadata/artwork editing and playlist sharing pending |
| Category organization | Additive server configuration, no file moves | Metadata/folder-ID/manual OR rules, exclusions, types, profile restrictions | Original libraries untouched | Schema, adapter, filtering/persistence tests | Folder-name patterns, indexed pagination and full artwork picker pending |
| Central presentation | Additive v1 draft/published files | Consume server rows/nav/theme/device settings | Legacy Web unchanged | Schema/persistence/conflict/authorization tests | Full preview fidelity, revision history/rollback pending |
| Admin overview/users/sessions | Preserved | Read-only real server info/users/session counts | Full dashboard | Browser fixture + existing APIs | Storage/activity detail panels pending |
| Layout/nav/category editor | Additive admin-authorized API | Reorder, label/color, category/profile rules, device sizes/safe zones, JSON editor, save/publish | Existing server config preserved | Browser/HTTP/store tests | Navigation icons, branding replacement and full banner picker pending |
| Encoding/hardware acceleration | Preserved | Legacy dashboard link | All inherited settings | Existing backend tests only | Hardware/transcode tests not run on production |
| Plugins/repositories/updates | Preserved | Legacy dashboard link | All inherited settings | No plugin architecture changes | Declarative plugin UI registry/native renderers roadmap only |
| Networking/discovery/HTTPS/proxies/DLNA | Preserved where supported upstream | Server-base-aware requests | Full inherited settings/client ecosystem | Adapter URL tests | TLS proxy/discovery/DLNA hardware verification pending |
| Logs/tasks/health/diagnostics | Preserved | Basic overview + legacy dashboard | All inherited tools | Existing suites only | New complete system screens pending |
| Backup/restore/database | Preserved; no migrations | Legacy dashboard link | Existing backup/restore | No storage/schema changes | Include new castlumora config folder in backup strategy; explicit restore test pending |
| Signup/invitations | Existing auth preserved; new registration not added | Not implemented | Existing admin user creation | None for proposed invitations | Admin enablement, expiry, cap/revocation future work |
| Setup presets/native clients | Existing server startup preserved | Startup directs to existing setup | Full inherited setup | No feature locks introduced | Family/creator/business presets and TV/mobile renderers roadmap |
| Tiers/payments/creator/cloud/offline/notifications | Existing server permissions/downloads preserved | Not implemented | Existing applicable server tools | None for proposed platform features | Optional/$0 tiers and full roadmap in PRODUCT_ROADMAP.md |
| Built-in intro/outro skipper | Inherited media-segment API retained; native repeated-segment analysis not added | Skip controls/auto-skip not implemented | Existing configured plugin/client behavior is unchanged; no mandatory plugin chosen | No new detection/skip tests | Planned: built-in scheduled detection, central intervals, correction, profile modes, post-credit safeguards and cross-client verification |

## Continuation checkpoint overrides

The initial table records the foundation baseline. The following changes supersede its corresponding pending entries:

- Playback: quality/source/chapter/seek/next-episode controls; actual synthetic HLS, multi-audio and external subtitles verified.
- Categories: server-side pagination/totals, folder names, permission-preserving manual selections and artwork/title pickers implemented; actual restricted-library and synthetic catalog checks pass.
- Presentation: published history (100 snapshots), restore-to-draft and explicit publish implemented and tested. Navigation icon rendering and complete preview fidelity remain pending.
- Administration: native library/options/scan, users/policies/passwords/devices, encoding, network/general settings, tasks/triggers/logs, plugin/repository/catalog, backup and tuner/listing/channel-mapping screens implemented. Real encoding persistence and library/user/log loading verified. Plugin installation, backup restoration, tuner hardware and destructive system actions remain unverified.

All other unfinished product groups remain unfinished; this checkpoint does not establish full master-spec parity.

- Live TV/DVR continuation: native six-hour guide/channel/date filtering, paginated programs/recordings, single/series record options, schedule edit/cancel and player channel switching implemented. Actual synthetic schedule persistence/cancellation and two-channel playback/cleanup pass. Completed recording lifecycle, long-duration timeshift, physical tuners and channel groups remain pending.

- Native skipper continuation: optional built-in same-season repeated-audio detection, scheduled extraction integration, central review/correction/approval, post-credit guard and per-profile manual/automatic/disabled browser modes implemented. Real synthetic detection and browser skipping pass. Suggestions require review; real-series accuracy, TV/mobile renderers and device verification remain unfinished. See docs/CASTLUMORA_SKIPPER.md.

- Identity continuation: optional administrator-enabled signup, hashed expiring/revocable/capped invitations, disabled provisioning, private audit/recovery, server policy snapshots and explicit library pickers implemented. Actual HTTP/browser signup and remembered-address pairing pass. Linked multi-profile accounts, named kids/adult presets and device-refresh improvements remain unfinished.

- Music continuation: native artist/album/track browsing, persistent queue/direct-or-MP3 playback, transport/progress and private playlist creation/editing implemented; actual synthetic music/playlist checks pass. Rich lyrics/metadata/sharing/scale/device coverage remains pending. Native photo/book/collection foundations are covered by the following checkpoint.

- Shelves/reader continuation: native photo albums/all-photo browsing and image navigation/rotation/zoom/slideshow; book shelves, protected PDF/EPUB reading and profile-specific position/theme/font persistence; native collection creation/media add/remove/delete retaining files. Three actual-server synthetic shelf/reader/collection browser workflows pass. API tests verify permission-aware book access and claims-only state ownership; EPUB execution/resource/traversal safeguards are unit-tested. Comics/other ebook conversion, annotations/full-text/offline reading and broader real-library/accessibility/device coverage remain pending. See docs/CASTLUMORA_READER_AND_SHELVES.md.

## Phase 1.4 packaging milestone — verified downloadable artifact

Native first-run setup now uses the inherited Startup APIs and remembers the integrated server base, including reverse-proxy prefixes. No authentication or backend startup policy is changed. Packaging scripts produce self-contained .NET linux-x64 output plus compiled Castlumora Web, castle assets, dependency notices, an isolated loopback launcher and installation/checklist documents. Actual archive smoke tests passed; GitHub read-only Actions run 42702481467 rebuilt/extracted/tested and uploaded artifact 13712785582. The artifact was downloaded through the connected GitHub app and its inner checksum/source manifest verified. Source commit cc59df91a8c06364d6ca1e6944ba938031754bd7; see docs/verification-preview-1.4.md. This is not a stable release or whole-spec completion. Legacy compiled Web is not bundled; a help page and explicit compatible-assets attachment workflow disclose remaining native admin gaps. Mandatory yt-dlp/YouTube metadata features are retained in docs/CASTLUMORA_IMPORT_AND_CREATOR_METADATA.md and remain unimplemented, not optional plugins.

Owner install guide: INSTALL_TEST_BUILD.md; systematic feature/limitations checklist: docs/TEST_BUILD_CHECKLIST.md. The GitHub artifact is sign-in protected and retained 30 days. Mint-compatible target is x86-64; CI ran on Ubuntu 24.04, local tests on Debian 13; no physical Mint/GPU/tuner certification.

### Phase 1.4 DVR recorded-media lifecycle

Native Recordings now refreshes automatically while visible and separates retained-file library removal from confirmed physical file deletion using server CanDelete. The recordings API preserves that explicitly requested field; no deletion authorization is broadened. Real synthetic M3U/XMLTV schedule/cancel/complete/file/play/remove/rescan/delete and denied-viewer checks pass; 81 LiveTV regression tests pass. Active-stop browser coverage, physical tuners, long duration, groups and per-user quotas remain pending. See docs/CASTLUMORA_DVR.md.

- Active DVR stop continuation: explicit Stop recording for InProgress single timers, permission-denied viewer stop, retained nonempty partial file and actual partial playback verified in a separate Chromium/real-backend synthetic case. Frontend 48 tests/typecheck/lint/build pass. This does not certify quotas, real tuners or long-running DVR.

### Node.js 24 development/CI continuation

Node 24 LTS is declared in .nvmrc and frontend engines and consumed by preview CI. Clean install/typecheck/lint/48 tests/build pass; workflow lint and five OpenAPI handler tests pass. Missing upstream JF_BOT_TOKEN replaced with built-in github.token for OpenAPI comments. Live workflow_run rollout requires reviewed default-branch changes; old main notifications can continue until then. Release version-bump execution remains untested and has an existing missing build.yaml dependency. No application feature or license changes.

### Native local creator metadata (first increment)

Built-in .info.json reader for explicitly generic Video files: strict YouTube/source/filename IDs, channel IDs, title/date/escaped description, bounded offline reads, reader disable/order settings and inherited metadata locks. Actual existing-file repair, whole-item and field locks, unlock and conflicting-ID rejection pass. Browse/Search/My List include generic Video items; formatting-only safe descriptions and creator playback are tested. Full provider suite 532 pass (17 new), frontend 50 pass/typecheck/lint/build. Full online provider/dashboard/relationships/caching, bulk repair/duplicates and yt-dlp importing remain required and unfinished; no fuzzy movie/TV matching or silent type migration. See docs/CASTLUMORA_CREATOR_SIDECAR.md.

### Native Metadata Management (selected items)

Administrator reader enable/order, source ID/type inspection, title/description corrections, whole-item lock, confirmed source repair and reload now use persistent inherited APIs. Fresh complete DTO/library snapshots preserve unrelated fields/options. Frontend 56 tests/typecheck/lint/build pass; actual persisted settings/correction/lock/repair and denied-viewer item/refresh/library writes pass. No bulk repair, artwork editing, source-ID override, ambiguity queue, duplicate UI, online enrichment or safe importer is claimed. See docs/CASTLUMORA_METADATA_MANAGEMENT.md.

### Refreshed exact-commit preview verification

Source 835716785add501a27d98e979fa67d411c63b570: Windows/macOS/Ubuntu full server CI passes; Ubuntu 4,255 tests passed/19 skipped. Native frontend 56 and report-handler five tests pass; Format/ABI/OpenAPI Check pass. Actual downloaded package passed CI fresh/restart/library/playback plus five independent local compiled-web/server creator/metadata/movie/DVR workflows, including completed and retained-partial recording playback and authorization. Download/checksum/report in DEVELOPMENT_PROGRESS.md and docs/verification-node24-and-metadata.md. This is an intermediate preview, not complete master-spec acceptance.

### Managed importer runtime prerequisite

Native administrator status/install UI and protected endpoints manage only reviewed bundled yt-dlp assets. Linux/Windows x64 asset selection, hash/size verification before execution, atomic candidate installation, bounded clean-environment version checks and failure/cancellation cleanup implemented. Local API205/3 skips, HTTP132/3 skips, frontend58 and bundler5 tests pass; compiled real Linux native install/reload/health/viewer denials verified. CI actual Windows/Linux asset checks and preview installer smoke configured; report executed outcomes after publication. URL queues/downloads/egress protection/source scheduling remain mandatory unfinished work. See docs/CASTLUMORA_IMPORTER_RUNTIME.md.

Managed-runtime source584e58c4ed is now verified in clean Linux/Windows/macOS CI and a downloaded/extracted Linux preview. Actual Linux/Windows pinned asset health tests ran. Packaged native install/reinstall/readiness/permission checks and original movie playback pass. Download/hash in DEVELOPMENT_PROGRESS.md; runtime readiness still does not enable URL import jobs.

## Import connection safety prerequisite

Internal per-connection HTTPS/public-address validation and DNS-pinned numeric connections are implemented; API264 passed/3 existing skips (59 new policy cases). This is not yet connected to yt-dlp: downloadJobsAvailable remains false. Next is the bounded subprocess egress adapter with TLS/redirect/resource fixtures, followed by destination safety and the durable authorized single-source queue. See docs/CASTLUMORA_IMPORT_NETWORK_POLICY.md. Full 1.0 import requirements remain mandatory; the verified584e runtime preview is preserved.

## Bounded import tunnel prerequisite

Internal authenticated loopback CONNECT adapter now enforces per-target public-address policy and bounded headers/connections/traffic/lifetime, preserving TLS verification. API278 passed/3 existing skips;14 real loopback cases include wrong-hostname rejection. Not connected to yt-dlp yet; URL downloading remains disabled. Next: verified executable leases, fixed-argument bounded runner and actual pinned-executable fixture tests, then persistent authorized queue/destination/scan workflow. All full1.0 import requirements remain mandatory.

## Managed execution reservation and tunnel lifecycle

Runtime leases now reverify executable integrity/version and prevent replacement during active execution, with idempotent release and administrator409 conflict. Tunnel listener stop/accept cancellation race corrected. Full API279 pass/3 existing skips. No actual download runner yet; downloading remains disabled. Next is bounded fixed-argument subprocess supervision through the authenticated tunnel, actual pinned-executable owned-HTTPS tests, then the complete persistent authorized queue and native controls. No1.0 requirement removed.

## Actual downloader transport verification checkpoint

The real pinned Linux yt-dlp executable now passes owned-HTTPS transfer and untrusted-certificate rejection through the authenticated tunnel in tests; full API281 pass/3 existing skips. This is transport verification, not a production job runner or enabled importer. Intermediate cafc Linux preview uploaded/downloaded/checksummed and packaged smoke passed; link and exact SHA in DEVELOPMENT_PROGRESS.md. Next: bounded production subprocess supervisor using leases/tunnel, then persistent authorized queue/destination/scan/native controls. Full1.0 scope retained.

## Publishing verification correction

SA1508 closing-brace whitespace corrected. Mandatory full pre-push validation and tracked hook added; initial enforcement tests pass, real full formatting/Debug/Release/CI-equivalent test checks remain pending. No publishing after failed or incomplete checks; no CI weakening. Complete this correction before further feature pushes; importer runner remains the next feature.

Complete-publishing gate: full format and Debug build passed; Release was blocked by workspace ENOSPC, so nothing was pushed. Supported dependency hard-link copies and disk preflights added without skipping assets/tests. Full frozen-source validation and Windows fixture CI confirmation remain required before further feature publication.
