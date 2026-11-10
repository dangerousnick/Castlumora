# Castlumora product roadmap

Castlumora 1.0 (development) is free, open source and self-hosted. [The complete product requirements](docs/CASTLUMORA_PRODUCT_REQUIREMENTS.md) are the source of truth. This roadmap separates working Phase 1.3 foundations from future milestones; a type or preview is not feature completion.

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
