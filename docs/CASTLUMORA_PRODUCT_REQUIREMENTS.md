# Castlumora — Master Product Requirements

**Status:** Living product specification / source of truth for future Codex tasks
**Project identity:** Castlumora, free and open-source, independent Jellyfin-derived platform.
**Version:** Castlumora project identity 1.0 (development; not a claim of shipped feature completeness).
**Repository:** https://github.com/dangerousnick/Castlumora

> **Instructions to future developers and coding agents:** Read this entire file before scoping any feature. Preserve every requirement unless the owner explicitly changes it. This is a product vision and backlog, **not** a statement of current implementation. Maintain a separate, evidence-based feature-parity/status tracker; do not mark features complete without functional verification. Prefer iterative builds that maintain all existing server capabilities.

## Non-negotiable product principles

1. Keep **all useful existing Jellyfin backend functions**, plugins, API and database compatibility, and admin access while adding new capabilities. Do not replace working functionality with placeholder screens.
2. **Castlumora has its own web UI**, rather than a cosmetic Jellyfin Web repaint. Continue supporting the inherited web UI as a fallback until equivalent Castlumora admin and playback functionality is tested. The main frontend may develop within this repo before a later extraction.
3. **Server is the source of truth for UI presentation**. Universal TV/mobile/web clients render a shared, versioned UI configuration using native/device-appropriate implementations. Server configuration can alter existing supported components without modifying each client; brand-new component capabilities may require client updates.
4. Simple enough for children and older family members; do not clutter the home screen or force users through server selection on every launch after initial pairing.
5. **All software features available to everyone**. Family/Friends, Content Creator and Business are setup presets, never paid product editions. Any account/resource tiers or monetization belong to individual server owners and may be completely free.
6. GPL and legally required upstream/third-party copyright, attribution, and source-history obligations remain. Preserve necessary internal Jellyfin/Emby/MediaBrowser identifiers until individually migrated and tested.
7. Keep honest distinctions among **implemented**, **prototype**, **planned**, **tested** and **unverified**.

## A. Streaming experience and discovery

- Minimal default top navigation: **Home | Browse | My List | Search**, optionally a **Surprise Me dice** icon. Administrator controls visibility, order and icons; don't make each library or collection a navbar item.
- Homepage: cinematic customizable hero/featured banners; Continue Watching; **Up Next/new episodes**; Recently Added; discovery/recommended rails; movies, shows, music, live TV and administrator-defined category rails. No empty rails. Responsive, keyboard/remote-friendly and accessible.
- Discovery engine uses explicit profile interests plus watch history, discovery/diversity and sensible defaults; no required cloud AI. Search across supported media with filters and useful sorting.
- **Surprise Me** button chooses from allowed content; filters for genre, age/profile, media type etc.; reroll and optionally family/group selection.
- Full detail pages: descriptions, artwork, ratings/metadata, seasons/episodes, audio, subtitles, chapters, queues and playback actions. Show episode artwork where available and offer sensible fallbacks where missing.
- **Continue Watching:** users open a title's **options menu** and choose **Remove from Continue Watching** without destroying watch history or playback progress. Don't clutter content rails with removal buttons. Following a show, hiding a suggestion, watchlist and Continue Watching are distinct states.
- Automatic Up Next when a new episode becomes available; separate Follow/Subscribe to Show preference and optional notifications for new episodes and seasons.
- Watchlists, playlists and recommendations per profile. Support music and other existing media types, not only video.
- Native-feeling web, television and mobile interfaces; right artwork sizes and focus states for remote controls; no unnecessary repeated server selection.

## B. Media organization and categories

- **One logical library with multiple virtual categories** rather than many top-level libraries. Users can browse categories such as Kids Movies, Teen, Disney, Princess, Animated, Adventure, Christmas, Halloween, Marvel, Family, Anime, TV Shows etc.; these are examples, not fixed requirements.
- Admin-defined categories driven by **physical folder names**, rules and metadata, collections, manual include/exclude, or combinations. A title can appear in many categories without duplicate media files or relocation.
- Admin controls category name, emoji/icon (including preconfigured sensible emoji/color matches by names such as Disney, Princess, Christmas), color, banner/artwork, sorting/order, profile permissions and home/browse placement.
- Works across media libraries/collections and on TV/Roku-style interfaces without hundreds of collections swamping the homepage.
- Retain all inherited library scanning, naming, provider metadata, artwork, collection, playlists and task capabilities.

## C. Server-controlled UI and visual editor — the signature feature

- **One visual administration editor** for desktop, phone, tablet and television presentation. Provide real previews/safe zones for banners (YouTube-like responsive crop safety), themes, rows, banners, backgrounds, navigation, content labels, profile/device overrides.
- Support server-published structured configuration with **schema version**, capability negotiation, defaults, validation, profile rules, authorization, publish/draft and rollback, migration and cache invalidation. Store on server, not solely local browser.
- Server is layout authority; client handles input, native rendering, media decoding/playback, device limits and responsive aspect ratios. Clients may choose platform-specific adaptations within the config contract.
- Custom logos, colors, branded onboarding, backgrounds, and eventually white-label server-owned branding on clients.
- Plugin-generated UI extensions described through safe common schemas that client applications support, with permissions and graceful degradation; don't allow arbitrary untrusted plugin code in clients.
- Keep primary navigation minimal; clean multi-category home screens and content rows with server-defined control.

## D. Playback, encoding, Live TV and DVR — preserve core parity

- Preserve existing **direct play, direct stream, remux, FFmpeg transcoding, codec/bitrate selection, hardware-accelerated encoding and decoding**, device profiles, subtitle/audio track switching, chapters, trickplay, progress, resume, captions and accessibility.
- Full **Live TV**: tuners, IPTV/M3U, XMLTV/EPG, channel numbers/groups/logos, guide grid, channel mapping, playback, channel hopping and recordings/scheduling/DVR where the backend supports them.
- Reliability: handle channel switches/session disposal and stale FFmpeg jobs correctly; avoid indiscriminate killing of shared FFmpeg processes. Expose logs, diagnostics, buffer/playback status and recovery options. Verify real device/stream behavior.
- Full administrative dashboard parity: Server overview, libraries, metadata, users, devices, playback, FFmpeg/hardware acceleration/transcoding, Live TV/DVR, plugins, networking, HTTPS/reverse proxies, remote access, diagnostics/logs, scheduled tasks, backups/restores and permissions. Keep legacy dashboard fallback for missing Castlumora controls.
- Preserve database/config migrations, discovery, DLNA or other preexisting integrations if supported, plugin contracts and install/upgrade workflows until replaced safely.

## E. Built-in intro and outro skipper — MUST NOT BE OMITTED

- **Native Castlumora functionality**, not a mandatory external plugin.
- Server-side automated detection of repeated series intros and recurring outros/credits, with sensible per-series/season/episode accuracy safeguards.
- Store timestamp intervals centrally so browser, TV and mobile clients can honor them.
- During marked segments display unobtrusive **Skip Intro** / **Skip Outro** actions. Offer **manual**, **automatic skip**, and **disabled** per-profile options; administrators can configure defaults and manually correct inaccurate segments.
- Handle episode transitions and optional Next Episode intelligently, including **post-credit scenes**; do not skip unique content based on unreliable detection.
- Optional, scheduled background analysis with limits/throttling so it does not interfere with streaming; admin can inspect, override and retrigger detection.
- Track implementation and testing separately: detection, data persistence, UI actions, auto-skip and cross-client support.

## F. Identity, profiles, registration and invites

- User accounts and multiple viewing profiles, distinct from service/resource tiers. Kids/adult presets, allowed libraries/categories, content age policies and profile-specific watch state/preferences.
- Admin can enable **self-registration from the server URL**, but **only administrators create/revoke invitations**. Expiring invite links, max successful registrations, admin-controlled access policies, optional account/profile presets and audit visibility.
- Persist pairing after initial device setup; no forced choose-server page on every launch. Preserve secure authentication, sessions, parental controls, account/device management and authorization boundaries.

## G. Optional quotas, business and creator modes

- Three onboarding presets: **Family & Friends**, **Content Creator**, **Business**. Same feature access in all modes; presets change suggested defaults and dashboard emphasis only.
- Server-defined concurrent-stream counts, bitrate caps, total bandwidth ceilings, transcoding/encoder allowances, DVR quotas and personal cloud storage quotas; fairness and observability for operators.
- Service tiers may be **$0**; payment/subscription portals optional. Never enforce monetization on personal/family setups.
- Creator features: OBS-compatible stream ingest, uploads, creator pages, channels, audience subscriptions, donations/tipping, optional server-side advertising and campaign reporting/analytics.
- Business features: branded portal/white-label direction, scalable viewers, subscriptions/payments optional, ad manager, analytics, role-based control.
- Operator can allocate personal DVR or cloud space, potentially in return for contributed storage/hardware, without forcing a payment model.

## H. Recording, downloads, notifications and extensibility

- Full DVR playback/recording/scheduling and per-user recording permissions/storage.
- Permission-controlled offline downloads with local storage quotas and entitlement rules; do not evade DRM.
- Followed-show notifications: new episode/season; optional recording notifications, alerts and notification center.
- Plugin compatibility and plugin-supplied native-UI component descriptions; schema validation and server/client capability negotiation.
- Backup/restore; permission-safe logs/diagnostics; repair/migration guidance; transparent feature parity.

## I. Implementation, repository and release policy

- Project brand **Castlumora**; existing purple crescent/castle/play symbol and wordmark; design language dark cinematic violet/blue. Existing source lives at `dangerousnick/Castlumora`, default `main`.
- Preserve source attribution, GPL, existing history and compatibility-sensitive names. Don't rename assemblies, package IDs, API headers, schema fields or deployment assumptions solely to erase mentions of upstream.
- Current project `VERSION` = **1.0** as project identity; no automatic claim of a released, feature-complete 1.0 executable. Version core binaries separately and deliberately after API/plugin/update compatibility analysis.
- New standalone Castlumora Web frontend is preferred, backed by the existing server API and new authorized configuration endpoints. Do not claim feature parity until measured.
- Keep `FEATURE_PARITY.md` and `PRODUCT_ROADMAP.md` current, mapping every requirement here to code, tests and milestones.
- Prioritize an end-to-end slice of **login → browse real media → detail page → working playback**, while ensuring inherited Live TV and administration remain accessible; then new web TV guide, complete admin parity, server-controlled editor/categories, and advanced functionality.
- Native TV/mobile clients render the shared layout schema in device-appropriate components rather than relying exclusively on an embedded webpage.
- Avoid fake functionality and mock data presented as production. Require build, automated tests and manual integration tests as feasible, disclose untested behavior, create reviewable PRs.
- Future design should accommodate both a dedicated home server and creators/business-scale scenarios without making them mandatory for ordinary families.

## J. Built-in yt-dlp ingestion and YouTube/creator metadata — mandatory production feature

- First-class, production-supported **yt-dlp import module** and **separate core YouTube-aware metadata provider**, without mandatory third-party plugins. Metadata identification must work for pre-existing files without downloading again.
- Three use cases: carefully curated child-safe local media libraries, creator-authorized archives/migrations, and business-use licensed trailers/promos. Same modules in Family/Friends, Creator and Business setup presets.
- One-shot/bulk URL/video/playlist/channel ingest where authorized; opt-in sync, job queue, retries, storage/quality limits, configurable destinations, provenance, deduplication and safe resource use.
- Preserve canonical video IDs, stable creator/channel IDs, original titles, descriptions, artwork, upload dates, playlists and available captions when rights/policies permit. No more matching named creators such as **Unspeakable** or **MrBeast** to unrelated movies/shows just because fuzzy titles match.
- Strong matching precedence, explicit video IDs, confidence display, manual fixes/locks, metadata provider priorities and safe refresh; maintain accurate creator pages and curated kid-accessible collections.
- A full Castlumora Web admin workflow for importing and repairing creator metadata; privileges, security and legal-source checks, history and verification. Use only lawful/authorized acquisition and rights-compliant metadata access; no DRM/security-circumvention features.
- Full specification and test acceptance criteria: **[Built-in imports and YouTube/creator metadata](CASTLUMORA_IMPORT_AND_CREATOR_METADATA.md)**. Future Codex tasks **must read this document** alongside this master specification. Do not leave these as optional add-ons or mark them complete without testing.

## Requirement retention rule

**Do not silently remove a feature in this file when narrowing scope for a development phase.** Mark it `planned` or `deferred`, record constraints and dependencies, and preserve it for future work. Any deliberate removal or changed product requirement should be recorded explicitly in Git history with rationale and owner approval.

_Last consolidated from the Castlumora planning conversation on 2026-11-09._
