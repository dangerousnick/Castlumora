# Castlumora product roadmap

Castlumora 1.0 (development) is free, open source and self-hosted. [The complete product requirements](docs/CASTLUMORA_PRODUCT_REQUIREMENTS.md) are the source of truth. This roadmap separates working Phase 1.3 foundations from future milestones; a type or preview is not feature completion.

## Phase 1.3: independent streaming vertical slice

Implement and verify a separate React/TypeScript client using inherited authentication/media/playback APIs. Preserve all server capabilities and legacy Web access. Add a central, versioned presentation configuration with draft/publish, reusable categories, user visibility and responsive overrides. Verification and limitations are tracked in DEVELOPMENT_PROGRESS.md and FEATURE_PARITY.md.

## Next: production playback and complete media access

Broaden tested codec/container/browser combinations, source selection, bitrate quality, chapters/trickplay, seeking, automatic fallback/retry and Live TV channel changes. Verify multiple tuner types, M3U/XMLTV, group filtering, long-running live streams, concurrent DVR, guide updates and recording schedules. Implement first-class music/photos/books/collections/playlists/downloads screens while retaining inherited functionality and permissions. Add large category pagination/indexing, folder-name rules, full category/banner editing, complete device preview fidelity and white-label server logo/wordmark selection. Real media tests must never target a production server without explicit authorization.

## Central UI evolution

Add immutable revision history/rollback and schema migrations, approved personal rearrangement, configurable navigation icons and profile presets. Define a permission-checked plugin UI metadata registry with namespaced declarative components, validation and client capability negotiation; never execute arbitrary plugin JavaScript in native clients. Add native TV/mobile renderers with safe zones and directional focus using the same published schema. New components can require application updates. Do not promise arbitrary new rendering features without updating clients.

## Users, pairing and access

Maintain clear boundaries between accounts, viewing profiles, permissions and optional service tiers. Add secure paired-device refresh/revocation, kids/adult presets, admin-enabled registration and invitation lifecycle (creation/revocation, expiration, successful-signup caps, atomic counters, abuse protection). Never grant access merely because a public profile is selected. Device/session management and parental controls must remain enforced in the backend.

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

Native Castlumora capability, not a mandatory external plugin. Plan server-side repeated intro/outro detection with per-series/season safeguards, centrally stored timestamp intervals and administrator correction. Preserve inherited media-segment APIs while defining built-in scheduled analysis with resource limits. Add unobtrusive Skip Intro/Skip Outro controls, profile manual/automatic/disabled modes and admin defaults; respect post-credit scenes and unique content. Track detection, interval persistence, manual correction, player actions, auto-skip, Next Episode transitions and native-client support separately. **Not implemented or verified by Phase 1.3.**
