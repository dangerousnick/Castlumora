# Feature parity inventory — Phase 1.3

“Preserved” means this change does not alter the inherited implementation; it does **not** mean every backend feature was exercised in this workspace. See `docs/verification-web-1.3.md` and DEVELOPMENT_PROGRESS.md for actual executed checks. Legacy fallback requires the existing compatible Web distribution at `<server-base>/web/`; it may require its own login. No fallback is removed.

| Feature group | Backend availability | Castlumora Web | Legacy fallback | Automated coverage | Manual/real verification and outstanding work |
| --- | --- | --- | --- | --- | --- |
| Server connection, base URL | Preserved | Pair once; retain address/device | Existing server setup | URL/base-path adapter tests | Loopback connection/authentication verified; real reverse-proxy deployment pending |
| Accounts/passwords/auth/sessions | Preserved | Sign in/out; public profile suggestions; tab-scoped token | Full account/device management | API fixtures + server auth integration tests | No passwordless profile bypass; refresh/revocation UI pending |
| Parental controls and permissions | Preserved | User-specific existing media endpoints; admin UI guard | Full policies | Server global-write denial tests | Full kids/adult policy matrix pending |
| Movies and series | Preserved | Search, libraries, paging, details, seasons/episodes | Existing client | Adapter, component, browser flows | Real loopback movie browse/detail verified; large library/performance validation pending |
| Music, photos, books, other types | Preserved | Existing-client link | Full inherited interfaces | Existing backend suites, not newly covered here | Native Castlumora screens pending |
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
| Metadata/artwork/collections/playlists | Preserved | Existing poster/backdrop metadata | Full legacy interface | Browse/detail fixtures | Native collection/playlist editing pending |
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
