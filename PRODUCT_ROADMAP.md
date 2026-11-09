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
