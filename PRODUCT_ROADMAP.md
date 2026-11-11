# Castlumora product roadmap

## Persistent queue continuation — internal only

Private durable single-worker job lifecycle now implemented with rights/provenance,
opaque destination IDs, deduplication, progress, cancellation, bounded retries and
restart recovery. Native TLS queue/runner integration is tested as transport only.
No library publication, public URL jobs or importer production-readiness claim.
See [queue contract](docs/CASTLUMORA_IMPORT_QUEUE.md) and DEVELOPMENT_PROGRESS.md
for actual verification and exact next steps. Complete secure media inspection,
idempotent library promotion/scanning and elevated admin management next; all
playlist/channel/bulk and remaining1.0 requirements remain mandatory.


## Latest verified checkpoint — 8aa77c0cc9

Linux/macOS-required policy and native macOS importer executable prerequisites
are published and verified. Both required platforms passed Debug/Release and all
17 backend suites (4,365 passed/19 existing skips each); Mac ARM64 real-tool API
cases ran (295/3). Windows advisory also passed (294/4 API) without changing its
deferred release status. Full local gate and the new Linux packaged/browser preview
passed. See [verification and download](docs/verification-platform-runtime-1.4.md).

Persistent import queue/library integration remains the next task; public URL jobs
are off. No full Mac installer, x64 Mac execution, Mint hardware or NAS/Docker
certification is claimed. Main report-token adoption and branch-protection admin
verification remain outstanding. All Castlumora 1.0 feature requirements remain.
Historical pending/blocker entries below are retained, superseded by this checkpoint.

## Native macOS importer prerequisite (current increment)

Reviewed universal macOS executable selection/bundling/install paths are added for
x64 and ARM64. Required macOS CI now supplies the real tool and exercises existing
health/TLS/transport/cancellation/cleanup assertions. Official digest/checksum and
local byte/hash/Mach-O inspection agree; native execution awaits exact-source CI.
No complete macOS installer, public URL jobs, queue or production readiness is
claimed. Linux/Windows pins and inherited compatibility code remain unchanged.
Next: persistent authorized single-source queue and secure library integration.

## Current platform policy — supersedes historical Windows blockers

Linux/macOS/shared-code checks remain mandatory. Windows compatibility tests are
advisory for Phase1.4 and stable1.0, with failure logs retained and inherited code
preserved. Windows-specific staging cleanup stays tracked, not erased; shared
security implications still require investigation. No1.0 feature scope is reduced.
See [platform evidence](docs/CASTLUMORA_PLATFORM_SUPPORT.md). Required targets include
Mint/Ubuntu, Debian, Docker/Linux NAS and verified macOS; actual runtime coverage
is recorded separately from CI. Branch-protection inspection returned HTTP403;
repository-level required-check configuration is not verified.

The backup policy fix is already published at0293885c4b; ten real backup tests and
the full Linux gate pass without a5GiB executor prerequisite. Production reserve
remains unchanged. Resume bounded importer development after the workflow milestone
is validated/published; do not repeatedly retry Windows-only failures.

## Current CI correction before further feature work

Backup policy fix and internal staging work published at0293885c4b after a passing
full local gate. Linux/macOS CI, formatting, ABI, OpenAPI and the downloadable
Linux preview passed. Windows has five abort/file-lock cleanup failures in the
staging runner; fix and verify those before unrelated milestones. Main's legacy
OpenAPI Report also needs reviewed adoption of PR3's standard-token handler.
No automatic main merge, repeated failing pushes or full Phase1.4 completion claim.
See DEVELOPMENT_PROGRESS.md for exact jobs, recovery steps and preview checksum.

## Backup validation before importer continuation

Production retains its original approximately5GiB reserve. Integration tests use
an internal per-instance policy while executing real SQLite export and ZIP
creation; no global setting or production bypass is exposed. The pre-push gate
reports capacity without applying a production-backup threshold to builds; actual
failures remain fatal. Full local validation passed on190d5ea9ed (4,365 backend
tests passed/19 existing skips;58 frontend tests passed). Publishing and exact-source
CI verification follow the current DEVELOPMENT_PROGRESS.md checkpoint. Historical storage-blocked entries below are
superseded by this design. Continue the existing importer after verified publishing.


Castlumora 1.0 (development) is free, open source and self-hosted. [The complete product requirements](docs/CASTLUMORA_PRODUCT_REQUIREMENTS.md) are the source of truth. This roadmap separates working Phase 1.3 foundations from future milestones; a type or preview is not feature completion.


## Current recovery priority and next importer slice

Supervised direct-HTTPS staging is implemented locally and API-tested, with no
public jobs enabled. Restore sufficient disk for the unchanged full validation
gate, pass all required checks, then publish the existing checkpoint. Do not bypass
the gate or recreate completed work. Next implement one authorized persistent
single-source import lifecycle including rights/provenance, destination checks,
playable-container inspection, sanitized metadata, deduplication, durable history,
cancellation/retry/recovery and promotion/scanning before exposing URL jobs.
See [internal staging contract](docs/CASTLUMORA_IMPORT_STAGING.md). YouTube/channel
metadata, bulk sources and all other Phase1.4 requirements remain mandatory;
this local transport milestone and the older preview do not complete Phase1.4.


## Phase 1.3: independent streaming vertical slice

Implement and verify a separate React/TypeScript client using inherited authentication/media/playback APIs. Preserve all server capabilities and legacy Web access. Add a central, versioned presentation configuration with draft/publish, reusable categories, user visibility and responsive overrides. Verification and limitations are tracked in DEVELOPMENT_PROGRESS.md and FEATURE_PARITY.md.

## Next: production playback and complete media access

Broaden tested codec/container/browser combinations, source selection, bitrate quality, chapters/trickplay, seeking, automatic fallback/retry and Live TV channel changes. Verify multiple tuner types, M3U/XMLTV, group filtering, long-running live streams, concurrent DVR, guide updates and recording schedules. Extend implemented native music/private-playlist workflows (lyrics, richer metadata/sharing/scale/device coverage), and broaden the implemented native photos/books/collections workflows (additional ebook/comic formats, annotations/search/offline, full download ergonomics and real-library/device/accessibility coverage) while retaining inherited functionality and permissions. Optimize large-library category indexing beyond the implemented server pagination/folder-name rules; complete full category/banner editing, complete device preview fidelity and white-label server logo/wordmark selection. Real media tests must never target a production server without explicit authorization.

## Central UI evolution

Extend implemented revision history/rollback with schema migrations, approved personal rearrangement, configurable navigation icons and profile presets. Define a permission-checked plugin UI metadata registry with namespaced declarative components, validation and client capability negotiation; never execute arbitrary plugin JavaScript in native clients. Add native TV/mobile renderers with safe zones and directional focus using the same published schema. New components can require application updates. Do not promise arbitrary new rendering features without updating clients.

## Users, pairing and access

Maintain clear boundaries between accounts, viewing profiles, permissions and optional service tiers. Add secure paired-device refresh/revocation, kids/adult presets, further registration ergonomics beyond the implemented administrator-enabled invitation lifecycle (expiry/revocation, durable successful-use reservations/caps, audit/recovery and abuse protection). See docs/CASTLUMORA_REGISTRATION.md. Never grant access merely because a public profile is selected. Device/session management and parental controls must remain enforced in the backend.

## Personalization and notifications

Keep Continue Watching, Up Next, watched history, following, watchlists and recommendation hiding separate. Add interests and local history-based recommendations with diversity and discovery, without a mandatory cloud AI service. Add follows, new-season/episode, recording and watchlist notifications with profile-isolated notification settings and optional notification center.

## Administration and onboarding

Replace inherited dashboard sections incrementally with tested server-backed controls: health/activity/storage, libraries/scans/providers, Live TV/tuners/XMLTV/DVR, encoding/hardware, users/access/devices, plugins/repositories, networking/tasks/logs/diagnostics/backups. Keep working legacy links until each replacement reaches verified parity. First-run Family & Friends, Content Creator and Business choices are editable configuration presets, never separate editions or feature locks.

## Optional resource tiers

All software features remain available without payment. Owners may choose concurrent-stream, bitrate/aggregate bandwidth, transcoding, DVR/storage and user/device fairness quotas. Every tier may cost $0; several free tiers must work without any payment integration. Any subscriptions, advertising or payment processing are optional owner-controlled integrations. Quotas require permission-aware accounting, privacy boundaries and fair enforcement, not client-only restrictions.

## Creator and business extensions

Design OBS-compatible ingest, creator uploads/pages/channels, viewer follows/subscriptions/donations, optional ads/analytics, white-label branding, owner service offerings and scaling. Threat model uploads and process isolation, content moderation/permissions, retention and consent before exposing public endpoints. This phase does not implement a commercial platform.

## DVR, personal cloud and offline

Build on server recording libraries; add private per-user recordings and quotas, owner-assigned storage and access policies. Offline media/downloads must respect existing content permissions and DRM. Test revoked access, storage isolation, quota races, restoration and cleanup without deleting original media unexpectedly.

## Release gates

No automatic merge or production deployment. Require passing tests and human review, real playback and tuner fixtures for supported claims, permission/security review, reversible configuration migrations, licensing review and an updated feature inventory. Preserve upstream source history, attribution, namespace/API/database compatibility and the legacy fallback until tested feature parity is achieved.

## Built-in intro/outro skipper (retained requirement from main)

Native optional repeated-audio analysis, same-season corroboration, reviewed central intervals, administrator correction/protected post-credit boundary, existing scheduled extraction integration and browser manual/automatic/disabled modes are now implemented. Actual synthetic detection and browser review/skipping pass; see docs/CASTLUMORA_SKIPPER.md.

Remaining: broad real-series/language/codec accuracy validation, more configurable analysis heuristics/resource scheduling, season-level review ergonomics, explicit episode-transition behavior beyond the existing Next Episode action, native TV/mobile renderers and physical-device verification. Suggestions require review and approval before becoming skips; no mandatory plugin or cloud service is introduced. This milestone does not complete the overall product roadmap.

## Phase 1.4 retained priorities and runnable preview

Read docs/CASTLUMORA_PHASE_1_4.md and docs/CASTLUMORA_IMPORT_AND_CREATOR_METADATA.md. Finish one tested/published milestone at a time; usage allowance/reset is recorded only if reliably exposed. Reserve capacity for a downloadable, checksummed, exact-commit Linux x64 preview with integrated compiled web, fresh setup, isolated data, licenses, actual extracted-package smoke testing and install/feature checklist. Packaging/delivery completed through a verified GitHub Actions artifact (run 42702481467, artifact 13712785582, source cc59df91a8c06364d6ca1e6944ba938031754bd7). Actual extracted-package fresh/restart/library/playback tests and independent download/checksum/manifest verification pass. Future previews use the read-only workflow; artifacts expire after 30 days. The next active milestone is full DVR lifecycle and permission/removal coverage, followed by profiles/admin stabilization and native metadata/importing. Afterwards finish DVR lifecycle/profiles/admin, implement the mandatory separate native creator metadata/sidecar provider and safe authorized yt-dlp queue/admin workflow, then the retained personalization/layout/skipper/identity/extended platform requirements. All presets retain all free features.

### Latest verified increment: recorded-media lifecycle

Completed actual synthetic recording completion/playback, future schedule cancellation, retained-file library removal/rescan and permission-checked physical deletion. Preserve the intermediate preview; the next DVR increment verifies active stop/partial files. Then continue source-aware local creator metadata/provider integration and safe importer, alongside remaining profile/admin gaps. Quotas, real devices and the full Phase 1.4 specification remain unfinished; refresh the downloadable preview after verified source milestones.

Active DVR stop/retained partial playback is now verified. Next: separate source-aware local creator metadata provider, then secure importer queue and native administrator workflow, with remaining profile/admin gaps retained. Continue development beyond intermediate previews.

### Development runtime checkpoint

Standardize frontend development and preview builds on Node.js 24 LTS; replace deprecated Node action runtimes and missing-secret OpenAPI comments with scoped built-in token use. Validate workflow lint, comment handler and frontend checks on the existing branch. Apply reporting to the default branch only through reviewed merge; continue source-aware creator metadata and importer development afterwards. Existing inherited release/version tooling needs separate Castlumora-specific design and validation, not an unrequested release.

### Local source-aware metadata checkpoint

First native retained-sidecar provider and generic-video browsing are implemented and verified independently of downloading. Next coherent increment: native Metadata Management backed by permission-checked persisted settings and explicit item repair/lock controls, with provider/type/ambiguity handling. Continue online metadata/caching/channel relationships and safe managed yt-dlp queue in separate tested increments. Full importer and production provider remain mandatory. Future preview manifests distinguish local-sidecar support from a complete metadata subsystem.

### Native selected-item metadata controls

Implemented and verified native local-reader enable/order, source inspection and selected-item correction/lock/repair. Next milestone: managed authorized importer executable/health/queue foundation, beginning with safe runner and validated source/destination boundaries, persistent queue/recovery and tests before dashboard claims. Retain online provider/ambiguity/bulk/artwork/duplicate and profile/admin requirements for further coherent increments. Produce current preview with completed features and keep its exact commit/checksum and recovery checkpoint.

Verified downloadable preview now contains native local-sidecar identification and Metadata Management, with actual packaged creator/movie/DVR complete/partial playback and permission tests. Exact source 835716785add501a27d98e979fa67d411c63b570; see recovery checkpoint for download/checksum. Next importer milestone must be bounded, secure, persistent and end-to-end testable; do not discard these completed features or infer full Phase1.4 completion from the preview.

## Owner version/platform requirements synchronized from main

All previously agreed 1.0 features remain mandatory; previews are incomplete checkpoints. Deep independent core replacement plus plugin/ebook marketplaces belong to 2.0, not a reason to defer existing 1.0 work. Keep shared business/API logic and genuine Windows/Linux process, path, dependency and service adapters; preserve existing reliable FFmpeg/session behavior. See permanent requirements Sections L/M. Managed importer runtime installation/health is the next prerequisite to its queue.

### Managed importer executable checkpoint

Pinned offline bundle installation/health and native administrator controls are implemented as the queue prerequisite. Current runtime readiness does not enable downloading. Next: one complete authorized-single-source queue milestone with persistent lifecycle/history, safe validated destinations, per-request egress/DNS/redirect protections, bounded subprocess supervision, cancellation/retry and scan on success. Extend to bulk/playlists/channels/scheduling only after that core is verified; full 1.0 requirements remain unchanged.

Managed runtime preview584e58c4ed is delivered and independently verified, including actual Windows/Linux binary checks and packaged native installation. Continue the authorized-single-source persistent import queue/security/scan workflow; this is the next 1.0 milestone, not a core rewrite or marketplace task.

## Import queue security continuation

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

Publishing remains blocked: complete format and Debug/Release builds pass; full server suite4,341 pass/19 existing skips/1 environmental failure (unchanged backup requires5GiB free, executor has2.8GiB). No guard/test weakened or skipped; no new commits pushed. Need larger executor and full successful gate before publishing or resuming importer feature work.

Storage investigation: safe cleanup recovered1.59GiB, raising available space to4.23GiB. The unchanged5GiB backup prerequisite remains unmet; no retry, gate bypass or application change. Need additional capacity/build headroom before full validation and publishing. See docs/verification-storage-cleanup.md.
