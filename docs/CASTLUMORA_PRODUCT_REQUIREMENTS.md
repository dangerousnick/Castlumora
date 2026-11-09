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
