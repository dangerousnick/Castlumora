# Castlumora — Built-in URL Import and YouTube Metadata Specification

**Status:** Mandatory production target, not yet verified implemented
**Priority:** First-class Castlumora media ingestion and metadata, for Family/Friends, Creators and Business presets
**Ownership:** Server-side core integration and Castlumora Web administration. Not a mandatory third-party Jellyfin plugin.

## Product intent

An administrator should be able to manage legitimate, locally hosted videos obtained from supported external sources without losing the original creator identity, title, cover, series/channel relationships, or permissions. This includes pre-existing files the user already holds and authorized imports via supported services. Import and metadata must be separate subsystems.

**Family/Friends:** Curated child-safe channels and local video collections presented in Castlumora, with parental permissions; no requirement to give children unrestricted YouTube access.

**Creator:** Owner-authorized migration or backup of a creator's own uploads and large archives, with bulk import, stable identity, provenance and continued availability independent of any single platform.

**Business:** Licensed promotional clips/trailers and owner-controlled libraries, with provenance, license/rights documentation and administrator-managed categories. Do not presume third-party trailers are free to download or redistribute.

The same functionality must be available in **all** server setup presets, without a software feature paywall.

## 1. Integrated yt-dlp import engine

- Package/support a maintained yt-dlp runner as a first-class, optional-at-runtime server module, with pinned/verified versions, updates, executable health checks, dependency/license notices and administrator controls. Installation/distribution methods must be production-ready for supported operating systems and architectures.
- One-shot imports from eligible URLs; selected videos/playlists/channels; batch jobs; resuming/interruption recovery; deduplication based on stable source IDs and existing file checksums; dry-run preview showing candidate titles/size/rights policy before import.
- Scheduled, opt-in synchronization for administrator-authorized sources, with configurable intervals, incremental discovery and an archive of imported source IDs; never auto-delete archived media merely because source content becomes unavailable.
- Admin-configurable destinations, storage quotas, naming templates, quality/format selection, subtitles/captions, thumbnails and info JSON when lawfully available. Respect source API usage, limits, terms and permissions.
- Job queue with concurrency limits, CPU/network/bandwidth throttling, retries/backoff, cancel/pause/resume where feasible, history, detailed logs, and failure reporting. Library scans and metadata refresh should be triggered only on successful import.
- Separate **source ingestion** from **metadata lookup**: the metadata provider must work for files already stored in a media library without importing anything anew.
- Admin-controlled URL input and source allowlisting; SSRF/private-address and redirect protections, filename/path sanitization, subprocess argument safety, safe archive extraction, rate limits, secrets handling, untrusted captions/info/HTML sanitization, account isolation and audit logs. Do not use hidden browser cookies or silently capture account credentials.
- Source-policy enforcement: only offer downloads/imports permitted by applicable terms, authorizations or rights. Provide supported alternatives for disallowed acquisition, such as creator exports/authorized uploads and local media import. No DRM circumvention, bot-detection evasion or credential harvesting.

## 2. Native YouTube-aware metadata provider

- Integrate a **YouTube/creator media metadata provider as a core Castlumora provider** with manual enable/disable, configuration, cache and refresh controls.
- Identify media from embedded source IDs, local `.info.json` sidecars, URLs, filename patterns and user-chosen matches; handle original title, video ID, stable channel ID, display name/handle, description, upload/publish date, duration, thumbnails, captions, tags, language, playlist/series relationships and original source link where legitimately available.
- **Preserve channel identity and exact source video title.** For a video known to be from Unspeakable or MrBeast, do not allow general film/TV fuzzy matching to silently replace its metadata with a loosely similar unrelated title.
- Prefer explicit source IDs and provenance over fuzzy names. Distinguish **creator videos**, **TV episodes**, **films**, **trailers** and **user-defined home video**; use provider priority and media-type policy to prevent cross-type misidentification.
- **Ambiguity handling:** Show candidate matches and confidence; require human confirmation for low-confidence cases; offer manual matching, locking metadata, per-item overrides and bulk corrections.
- Support channel/creator collections and playlists, ordered episodes/items when source ordering is meaningful, local videos with atypical titles, renamed files, punctuation differences (e.g., `and` versus `&`) and duplicate titles across creators.
- Implement refresh without overwriting locked/manual fields. Cache metadata with appropriate expiry/privacy/licensing rules, invalidation and offline fallback for existing saved metadata.
- Thumbnails and descriptions should be used only where their acquisition, retention and display are permitted; use local artwork/manual metadata if a service's API policy prohibits persistent caching or modifications.
- Integrate with search, homepage/category rails, kids restrictions, user profiles and the generic metadata editor. Do not require the admin to install an external Jellyfin provider or run yt-dlp by hand.
- Where allowed, export portable sidecar metadata so migration between servers does not sacrifice video provenance.

## 3. Castlumora Web administrator experience

- Dashboard area: **Media → Imports & Sources**. Source URL, destination/library, preview, authorized usage confirmation, quality options, scheduling, queue, detailed progress, cancellation/retry, history and safe cleanup of failed partial imports.
- Dashboard area: **Metadata → Creator/YouTube Provider**. Source linking, provider precedence, confidence matching queue, manual override/lock, refresh, duplicate detection and correction logs.
- Kids browsing shows curated, locally available content by creator/series/category with the server's profile permissions. Never require unrestricted platform browsing to play authorized locally hosted content.
- Creator setup supports controlled bulk catalog import, local upload and external creator-owned archive transfer. Business setup supports rights tracking and authorized trailers.
- Honor server resource quotas, account boundaries, creator ownership and existing Castlumora library permissions.

## 4. Implementation approach and validation

- Audit existing Jellyfin/MediaBrowser metadata and library entry points before changing them; prefer additive provider and job APIs, preserving existing plugin contracts, database migration safety, direct/Live TV playback, scheduled tasks, FFmpeg configuration and all other mature functions.
- Do not pretend the importer or provider is production-ready until tested across authorized single video, playlist, batch, cancel/resume, offline metadata, already-downloaded files, conflicting creator/movie names, malicious URLs, security restrictions and supported environments.
- Create a reusable internal abstraction for additional yt-dlp-supported sources where legally/technically permitted, while making YouTube-specific creator metadata accurate and first-class.
- Document subprocess/update pinning, packages/licenses, limits, rights validation, security model and test results.
- Update `PRODUCT_ROADMAP.md`, `FEATURE_PARITY.md`, and `DEVELOPMENT_PROGRESS.md` as work proceeds.
- Ship in reviewable milestones: (1) schema/provider and sidecar metadata tests; (2) secure authorized import runner and queue; (3) admin workflow and account/profile permissions; (4) large-scale syncing/recovery and production validation.

**Requirement permanence:** This feature is **mandatory for the full Castlumora production vision**, not necessarily completed within the current Codex task. No arbitrary omission or downgrading to a third-party plugin. Do not misrepresent planned behavior as shipped behavior.

## Implementation checkpoint — retained sidecars

The first native offline reader is implemented for explicitly generic Video items; see [CASTLUMORA_CREATOR_SIDECAR.md](CASTLUMORA_CREATOR_SIDECAR.md) for strict ID/type boundaries, actual verification, settings and limitations. This supplies existing-file identification and source IDs without downloading media. The complete provider, metadata dashboard and safe yt-dlp import subsystem above remain mandatory and incomplete.

## Managed executable prerequisite

Pinned platform bundle/install/health and administrator controls now exist; see CASTLUMORA_IMPORTER_RUNTIME.md. This does not implement URL ingestion, queue or download-job production safety. Those remain mandatory targets above, independent of already working local sidecar metadata.
