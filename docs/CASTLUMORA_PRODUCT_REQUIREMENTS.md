# CASTLUMORA — PHASE 1.3 MASTER DEVELOPMENT TASK
## Independent Web Platform, Full Server Compatibility & Cross-Device Foundation

**Repository:** `dangerousnick/Castlumora`
**Base branch:** `main`
**Development branch:** `development/castlumora-web-1.3`
**Project version:** Castlumora 1.0 (development)
**License:** Preserve the project's applicable open-source licenses and attribution.

---

# 1. PROJECT MISSION

Castlumora is a free, open-source, self-hosted streaming platform derived from Jellyfin.

The objective is to build an independent streaming platform with a modern interface comparable in usability to major commercial streaming services, while retaining Jellyfin's mature backend capabilities.

**Castlumora must be more than a renamed Jellyfin interface.**

Its defining feature is a **server-controlled, cross-device user interface**. Server administrators control the layout, navigation, categories, banners, themes, and presentation rules through one central dashboard.

Web, television, and mobile clients consume a shared versioned presentation configuration and render native, device-appropriate interfaces.

The server remains responsible for media storage, authentication, playback authorization, library management, streaming, transcoding, Live TV, DVR, plugins, and administration.

The platform must remain free and open source. Do not introduce mandatory paid features or artificial software limitations.

# 2. NON-NEGOTIABLE: PRESERVE ALL EXISTING CORE FUNCTIONS

Do not delete, disable, or unnecessarily rewrite existing server functionality.

Preserve the underlying capabilities, including:

- Movies, television, music, photos, supported books, and other existing media types.
- Libraries, folder monitoring, scanning, metadata, artwork, collections, playlists, and scheduled tasks.
- Direct Play, Direct Stream, remuxing, FFmpeg transcoding, audio/video codec selection, bitrate controls, and supported hardware acceleration.
- Subtitles, audio languages, chapters, trickplay, playback resume, and playback progress.
- Live TV, IPTV/M3U, XMLTV, electronic program guides, TV tuners, channels, channel groups, channel switching, and DVR where supported.
- Users, passwords, profile access, permissions, authentication, sessions, devices, and parental controls.
- Plugins, plugin configuration, repositories, extension points, and updates.
- Networking, server discovery, reverse proxies, HTTPS, remote access, and applicable DLNA functionality.
- Logs, scheduled tasks, server health, diagnostics, backups, and restoration mechanisms.
- Existing database schemas, configuration files, API contracts, authorization behavior, and compatibility-sensitive identifiers.

Keep existing backend functions accessible even if the new Castlumora Web interface has not implemented the equivalent screens.

Do not remove the legacy Jellyfin Web fallback until full feature parity has been tested and confirmed.

**Never sacrifice working server capabilities merely to remove upstream branding.**

# 3. BUILD CASTLUMORA WEB

Create an independently designed frontend named **Castlumora Web**.

For this phase, it may live in a well-isolated `Castlumora.Web` directory within the existing repository, allowing backend and frontend work to evolve together.

Use React, TypeScript, and Vite unless repository inspection reveals a materially better technical choice.

Organize the application into maintainable modules:

- API adapter and typed contracts
- Authentication and profile management
- Navigation and routing
- Design system and shared components
- Media discovery, browsing, search, and detail screens
- Playback and Live TV
- Administration
- Server-controlled presentation configuration
- Categories and content organization
- Notifications and watchlists
- Accessibility and responsive device layouts

Use actual server API responses for functional screens. Mock data may be used only in isolated previews or clearly labeled demonstrations.

# 4. CASTLUMORA VISUAL IDENTITY

Use the existing purple castle branding under `branding/`.

The new interface should feature:

- Dark, cinematic design
- Purple primary accents and subtle blue highlights
- Castlumora logo and wordmark
- Large featured artwork and banners
- Horizontally scrolling movie and television rows
- Modern detail screens with readable descriptions
- Smooth but restrained animations
- Accessible focus states and keyboard controls
- Responsive desktop, tablet, and mobile layouts
- TV-friendly spacing and artwork safe zones

Do not reproduce Jellyfin Web's visual layout as the final design.

Server owners must eventually be able to replace the default Castlumora branding with their own server branding.

# 5. SIMPLE USER NAVIGATION

Default navigation:

**Home | Browse | My List | Search**

Include a small **Surprise Me** dice action.

Do not populate the primary navigation bar with every library, collection, and content category.

Categories should appear naturally within browsing pages and homepage content rows.

Navigation must be configurable by the server administrator, including order, visibility, icons, and profile restrictions.

# 6. HOME SCREEN AND CONTENT DISCOVERY

Implement a configurable homepage supporting:

- Featured content banners
- Continue Watching
- Up Next
- Newly available episodes
- Recently Added
- Movies and TV Shows
- Administrator-defined content rows
- User-specific recommendations
- Featured categories
- Live TV recommendations where available
- Surprise Me / random selection

Avoid empty rows or unnecessarily repetitive content.

Allow the server to control default ordering while supporting approved personal preferences.

Personal recommendations should use watch history and explicitly selected interests, with some diversity and discovery. Do not require a cloud AI service.

# 7. CUSTOM CATEGORY SYSTEM

Content categories must be independent of physical media libraries.

A movie or show may belong to multiple categories without duplicating its underlying file.

Examples:

Kids Movies, Teen, Disney, Marvel, Princess, Christmas, Halloween, Animation, Anime, Family Movies, and any administrator-defined category.

Implement a category system supporting:

- Custom category names
- Icons, emoji, colors, and banners
- Manual inclusion/exclusion of titles
- Folder-name rules
- Metadata-based rules
- Content types
- Profile visibility
- Category ordering
- Reusable category definitions
- Server-side persistence

Categories should work across multiple libraries and should not require moving media files.

Changes must not damage original library organization.

# 8. SERVER-CONTROLLED UI — MOST IMPORTANT FEATURE

The **server is the source of truth for presentation configuration**.

Create a structured, versioned, extensible presentation schema that compatible web, TV, and mobile clients can understand.

Support:

- Navigation configuration
- Homepage layout
- Content rows
- Category placement
- Featured banners
- Theme colors and backgrounds
- Artwork selections
- Profile visibility
- TV, desktop, tablet, and mobile layout variants
- Device-specific responsive safe zones
- Plugin-contributed UI definitions
- Schema versions and client capability negotiation
- Defaults and safe fallback behavior

The client must render from published server configuration rather than relying exclusively on hardcoded UI layouts.

New unsupported component types must degrade gracefully on older clients.

Server owners must not be forced to manually modify each TV or mobile app to rearrange ordinary content.

However, new client component types may require an application update; do not promise that arbitrary new rendering capabilities can appear in existing native applications without one.

# 9. VISUAL UI EDITOR

Build the foundation of an administrator-controlled visual editor.

It should eventually support:

- Reordering navigation items
- Enabling/disabling categories
- Arranging homepage rows
- Selecting banners and featured items
- Changing colors, themes, and backgrounds
- Previewing TV, desktop, tablet, and mobile layouts
- Managing per-device overrides
- Draft, preview, save, and publish workflow
- Version history and rollback

Persist settings through server-side administrative endpoints with validation and appropriate authorization.

Do not implement a browser-only editor whose settings disappear after logout.

# 10. AUTHENTICATION, SIGNUP, INVITATIONS AND PROFILES

Preserve existing server authentication and permissions.

Develop a foundation for:

- User login and logout
- Profile selection
- Separate kids/adult presentation presets
- Server-paired client sessions
- Self-registration when administrator-enabled
- Administrator-only invitation creation and revocation
- Invitation expiration
- Maximum successful signups per invitation
- Account creation through the server's registration page
- User/profile restrictions
- Device management
- Secure session handling

Do not require ordinary users to choose their server repeatedly after initial pairing.

Distinguish accounts, viewing profiles, permissions, and service resource tiers.

Do not weaken authentication to simplify integration.

# 11. MOVIES, SHOWS AND VIDEO PLAYBACK

Implement functional screens for:

- Movies
- Series
- Seasons
- Episodes
- Episode descriptions and artwork
- Search
- Media information
- Watchlists
- Continue Watching
- Up Next
- Playback resume
- Audio and subtitle controls

The player must use the existing server's supported playback and transcoding mechanisms.

Support direct playback when possible and request compatible server playback/transcoding options when necessary.

Report progress and playback state correctly.

**Continue Watching requirement:**

Users should open a title's options menu and select **Remove from Continue Watching**.

This should remove the row entry without unnecessarily deleting watch history or making the title unwatched.

Following shows, maintaining watchlists, and hiding recommendations must be separate concepts.

# 12. LIVE TV AND DVR — FIRST-CLASS FEATURES

Live TV must not become a forgotten feature.

Build usable Castlumora Web foundations for:

- Live channel lists
- Channel logos
- Channel groups
- Electronic program guide
- Program descriptions
- Channel switching
- Live playback
- Stream/transcoding errors
- DVR recording management
- Recording schedules where supported
- IPTV/M3U and XMLTV administration

Preserve existing tuner and recording backends.

The player should handle channel transitions cleanly and not unnecessarily retain abandoned playback sessions.

Do not introduce unsafe process termination or unverified FFmpeg cleanup behavior.

# 13. COMPLETE ADMINISTRATOR DASHBOARD

Castlumora must ultimately replace the old management interface with a complete administrator experience.

Use logical sections and submenus rather than placing every setting on one screen.

Required coverage:

**Overview**
Server health, active sessions, activity and storage.

**Libraries**
Library creation, scanning, metadata providers, collections and organization.

**Live TV & DVR**
Tuners, M3U playlists, XMLTV, channel mapping, guides and recordings.

**Playback & Encoding**
FFmpeg, transcoding, hardware acceleration, bitrate settings and supported GPU encoders.

**Users & Access**
Users, profiles, permissions, invitations, devices and sessions.

**Plugins**
Repositories, installed plugins, configuration and updates.

**Presentation**
Castlumora UI editor, category rules, themes, banners and navigation.

**System**
Networking, reverse proxies, scheduled tasks, logs, diagnostics, backups and restoration.

**Resource Management**
Optional account tiers, bandwidth limits, stream quotas, transcoding capacity and storage allowances.

Do not fake settings that do not have working backend persistence.

If a setting is not yet supported by Castlumora Web, preserve access to the legacy administration interface until the replacement is implemented and tested.

# 14. THREE SERVER SETUP PRESETS

Support an eventual first-run choice:

1. **Family & Friends**
2. **Content Creator**
3. **Business**

These are configuration presets, not separate software editions.

All features remain available to everyone, regardless of chosen preset.

Presets may adjust suggested settings, layout defaults, onboarding, and enabled modules, but must never permanently lock a user out of features.

# 15. OPTIONAL RESOURCE TIERS AND MONETIZATION

Castlumora itself remains free.

Server owners may optionally configure free or paid service tiers.

Resource rules may include:

- Concurrent streams
- Per-stream bitrate
- Aggregate bandwidth
- Transcoding capacity
- DVR quotas
- Personal cloud storage quotas
- User/device limits
- Fairness policies

Every tier must be allowed to cost **$0**.

An administrator may offer several free tiers with different resource allowances, without requiring a payment system.

Paid subscriptions, payment processing, or advertisements must be optional owner-controlled integrations, not mandatory Castlumora features.

Do not impose a subscription model on family servers.

# 16. CONTENT CREATOR AND BUSINESS ROADMAP

The platform must be designed to accommodate:

- OBS-compatible live streaming
- Creator video uploads
- Creator pages and channels
- Viewer subscriptions and donations
- Optional advertising management
- Basic analytics
- White-label server branding
- Business-controlled service offerings
- Scaling to larger numbers of users

Do not attempt to implement an entire commercial platform in this phase.

Record these requirements in the product roadmap and avoid architectural decisions that make them unnecessarily difficult later.

# 17. DVR, PERSONAL CLOUD AND OFFLINE MEDIA

Plan support for:

- Server-recorded television
- Personal recording libraries
- Per-user DVR quotas
- Administrator-assigned cloud storage
- Permission-controlled offline downloads
- Storage limits and access policies
- Appropriate privacy boundaries between user storage allocations

Offline capability must respect content permissions and must not bypass DRM.

# 18. NOTIFICATIONS AND PERSONALIZATION

Plan for:

- Following television series
- New episode notifications
- New season notifications
- Recording notifications
- Watchlist updates
- User-selected interests
- Watch-history-based recommendations
- Profile-specific content visibility
- Optional notification center

Continue Watching and Up Next must remain logically distinct.

# 19. PLUGIN EXTENSIBILITY

Preserve the server's existing plugin architecture.

Design a path for plugins to expose UI metadata and supported component definitions through the shared presentation schema.

Compatible native clients should render supported plugin components without requiring each plugin to modify the app's source code.

Plugin-contributed UI must be permission-controlled and validated.

# 20. FULL FEATURE PARITY AND TESTING

Create `FEATURE_PARITY.md`.

Track each existing server feature with:

- Backend availability
- Castlumora Web availability
- Legacy-interface fallback
- Automated testing
- Manual verification
- Outstanding work

Run applicable verification, including:

- Frontend type checking and build
- Linting
- Component tests
- API integration tests
- Server build
- Authentication and authorization tests
- Media browsing
- Playback
- Live TV integration where test fixtures are available
- Configuration persistence
- Regression tests for modified backend functionality

Do not claim success for tests that were not run.

Do not delete mature server components simply because the new interface cannot render them.

# 21. DEVELOPMENT DOCUMENTATION

Create or update:

- `README.md`
- `PRODUCT_ROADMAP.md`
- `FEATURE_PARITY.md`
- Frontend development instructions
- API adapter documentation
- Presentation schema specification
- Configuration and migration notes
- Build/test verification report

Clearly distinguish implemented features from prototypes and planned features.

The README should remain Castlumora-first, while retaining appropriate upstream attribution.

# 22. IMPLEMENTATION PRIORITIES

This specification defines the long-term system, but Phase 1.3 should produce a **working vertical slice**, not a thousand unfinished features.

Prioritize in this order:

**Priority A — Working streaming foundation**
- Castlumora Web project
- Branding and navigation
- Real authentication
- Real library browsing
- Movie/series details
- Real video playback
- Playback progress

**Priority B — Essential server integration**
- Live TV playback and guide
- Profile permissions
- Complete feature-parity inventory
- Reliable access to existing advanced administration
- Subtitle/audio controls

**Priority C — Distinctive Castlumora architecture**
- Versioned server-controlled UI schema
- Server-backed configuration
- Basic visual layout editor
- Server-side category data model
- Categories and customized rows

**Priority D — Later milestones**
- Invitations and resource tiers
- Monetization, creator tools and business tools
- Full native TV/mobile clients
- Cloud storage, offline downloads and notifications
- Advanced recommendations and plugin-driven UI

If necessary, divide implementation into multiple small commits and documented milestones within the same development branch.

Never report a planned feature as finished merely because its data types or placeholder page exist.

# 23. SOURCE CONTROL AND DELIVERY

1. Inspect the repository and verify that the existing `main` branch contains Phase 1 and Phase 1.1 changes.
2. Create `development/castlumora-web-1.3` from `main`.
3. Implement the highest-priority working features.
4. Commit progress in logically organized changes.
5. Run available builds and tests.
6. Produce a clear implementation and feature-parity report.
7. Push the development branch.
8. Create a pull request targeting `main`.
9. Do not merge automatically.
10. Do not delete original upstream source history, licenses, or attribution.

If blocked by missing dependencies, environment restrictions, or unavailable testing infrastructure, explain the blocker and preserve working changes rather than fabricating results.

# FINAL ACCEPTANCE CRITERIA

Phase 1.3 is successful when:

- Castlumora Web has an independently designed interface.
- A user can authenticate against the actual server.
- Real media libraries can be browsed.
- Media details can be opened.
- Supported media can be played through the server.
- Live TV remains functional in the backend and has a working or explicitly documented frontend integration.
- Existing administrative features remain accessible.
- A server-controlled presentation configuration foundation exists.
- The code builds and the implemented features have meaningful verification.
- A full roadmap preserves the remaining Castlumora product vision.
- A pull request is ready for review without removing existing server functionality.

**Guiding principle: Build on everything Jellyfin already does well, then make Castlumora substantially more customizable, easier to navigate, and more capable.**


# Execution continuity requirements (user follow-up)

1. Commit completed, working changes regularly to the development branch.
2. Publish each substantial tested milestone to GitHub.
3. Maintain DEVELOPMENT_PROGRESS.md with completed features, unfinished work, known problems and exact next steps.
4. Keep this complete product specification as the permanent source of truth.
5. Never discard completed work or restart implementation unnecessarily.
6. Leave a recoverable repository checkpoint if a session is interrupted or reaches usage limits.
7. Do not merge into main until implementation has been tested and reviewed; do not merge automatically.

No running Coon Media service or official Jellyfin repository is part of this development environment or deployment scope.

---

# Additional persistent product vision retained from main

The following requirements were added to main during Phase 1.3 development. They remain part of the product backlog, including the built-in intro/outro skipper; inclusion here does not claim implementation in this vertical slice. Neither specification has been discarded.

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

## Requirement retention rule

**Do not silently remove a feature in this file when narrowing scope for a development phase.** Mark it `planned` or `deferred`, record constraints and dependencies, and preserve it for future work. Any deliberate removal or changed product requirement should be recorded explicitly in Git history with rationale and owner approval.

_Last consolidated from the Castlumora planning conversation on 2026-11-09._
