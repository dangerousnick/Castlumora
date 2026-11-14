# Castlumora development preview test checklist

Use an isolated installation and copies/authorized fixtures. Record BUILD_INFO.json commit, OS/CPU/RAM/free space, FFmpeg/browser versions and test date. “Prior automated” means source/disposable-server tests before packaging; the release verification report identifies actual archive tests. No physical GPU/tuner/Mint manual testing is implied.

| Feature / location | Steps and expected result | Coverage and limits |
| --- | --- | --- |
| Fresh install | Extract; run start-castlumora; visit :28096/web; create admin; sign in | Actual packaged smoke required; local-only preview data, no service install |
| Branding/navigation | Purple castle; Home/Browse/My List/Search; open native admin | Prior browser checks; compiled UI must load from packaged server |
| Account security | Sign out/in; create limited user; deny admin routes/library not allowed | Inherited auth + prior HTTP/browser tests; linked profiles/presets unfinished |
| Libraries | Admin → Libraries; add copied Movie/TV/music/photo/book folder; scan; Browse | Prior actual synthetic catalog/browser checks; packaged report distinguishes newly rerun workflows |
| Metadata/artwork | Open movie/episode; verify title/artwork/tracks | Existing providers retained; online enrichment/artwork repair remains unfinished |
| Local creator metadata | Home videos library; retain matching filename.info.json; scan/repair; Browse → Creator & home videos; play | Native strict sidecar ID/type/size/security tests and actual existing-file/lock/conflict/browser/playback checks pass; no downloading or full online provider |
| Metadata Management | Admin → Metadata Management; choose library; toggle/reorder reader/save/reload; search item; correct title/description and lock; unlock/confirmed repair/reload | Native persistence/repair and denied-viewer API workflow pass; fresh complete DTO preserves unrelated fields; bulk/ambiguity/duplicates/artwork editing pending |
| Movie/episode play | Watch now; pause/seek/exit/resume; choose quality/audio/subtitles/chapter | Prior direct/HLS/track browser checks; actual package smoke; codec/device matrix unfinished |
| Music | Browse → Music; album/artist/track; background navigation; private playlist add/move/remove/rename | Prior actual-server tests; lyrics/sharing/large-scale compatibility pending |
| Photos/books | Browse → Photos/Books; rotate/next; PDF page/EPUB chapter; reload position/theme | Prior actual-server browser tests; more ebook formats/offline/annotations pending |
| Collections/My List | Create collection/add/remove/delete; original files remain; add/remove My List | Prior actual-server tests; no file movement/duplication |
| Resume/Up Next | Play part of title; options → remove resume; verify progress retained | Prior preference/menu tests; follows/notifications remain separate and unfinished |
| Live TV/DVR | Admin → Live TV; owned M3U/XMLTV; Guide; play/switch; schedule/cancel; complete/play recording; active Stop; remove vs delete | Actual synthetic complete/partial playback, viewer denials, retained-file removal/rescan and physical deletion pass; real tuners/groups/long duration/quotas pending |
| Skipper | Admin → Skipper enable/analyze/review/approve; play episode; manual/automatic/disabled skip | Prior synthetic detection/browser tests; no intervals until approved; post-credit boundary; broad real-series/native-device accuracy pending |
| Layout/categories | Admin → Presentation; draft/reorder/category/profile scope; publish; reload; restore history to draft | Prior HTTP/browser tests; package persistence smoke; advanced preview/plugin/native renderer work pending |
| Encoding | Admin → Playback/Encoding; inspect codecs/hardware settings | UI/API available; no supported GPU exists here, do not enable unsupported acceleration |
| Plugins/network | Admin → Plugins/System; inspect repos/catalog/network | Native screens retained; real plugin installation, discovery/proxy/TLS deployment pending |
| Logs/tasks/backups | Admin → System; view logs/tasks/triggers/backup controls | Inherited backend retained; destructive actions/restore unverified; back up preview before experimenting |
| Advanced legacy | Existing client opens help unless compatible compiled legacy assets are attached | Not bundled; see INSTALL_TEST_BUILD.md; no false parity claim |
| Managed importer runtime | Admin → Imports & Sources; install bundled executable; check health; reload | Native pinned/hash/version lifecycle implemented; Linux real-binary tests pass; macOS native CI required; Windows advisory; integrated job capability also requires initialized FFmpeg/FFprobe |
| Authorized direct video | Admin → Imports & Sources; choose one-folder Home Videos library; submit owned HTTPS H264/AAC MP4≤32MiB with rights; monitor/cancel/retry; reload/restart; browse/play imported item | Real TLS/native queue/publication/index/media-bytes/restart and role tests pass locally; packaged/UI evidence is recorded separately. Source queries stay private. Full YouTube/playlist/channel/bulk/schedule provider work remains mandatory unfinished work |
| Atomic multi-URL import | Imports & Sources → Import multiple URLs; submit two owned MP4s, then a batch with one private/unsafe URL | Valid batch persists all entries and scans completed media; invalid batch creates no partial jobs. Duplicate references preserve original rights/retries. See batch guide and per-source verification. |
| Opt-in transient retries | Imports & Sources; opt in for an authorized test source with a temporary network/rate failure, observe the scheduled retry time, cancel while waiting and restart | Three total attempts, persisted deadline and cancellation; other ready work proceeds. Permanent/security failures require review. Exact verification is recorded in DEVELOPMENT_PROGRESS.md; use only owned fixtures. |
| Import failure guidance | Imports & Sources; inspect failed owned job; correct cause before manual retry | Fixed redacted codes, persisted failure/reload and safe UI guidance are verified locally; final gate/native CI reported per source. Never disable TLS/SSRF checks or raise limits to bypass errors. |
| Restart/removal | Ctrl+C; restart same state; verify account/layout; stop and delete only preview folder/state | Package smoke required; do not touch original media/another server |

## Report a bug

Open a GitHub issue with exact commit/package/checksum, environment, numbered steps, expected/observed behavior, timestamp, relevant redacted log excerpt and optional screenshot. Include whether a feature was verified/experimental, media codec/container (not copyrighted files), and whether direct or compatible playback failed. Never include passwords, access tokens, private media URLs, databases or invitation links.

## YouTube single-video development workflow

Find Administration → Imports & Sources → Import a YouTube video. Install the
managed runtime and select an eligible Home Videos library first. Preview a video
you own/have permission to retain; verify creator/video identity, quality and size
budget, then confirm rights and queue it. Expect persistent history, cancellation,
bounded retries, creator metadata and library scanning/playback after success.
Changing the source/destination/resource choice requires another preview.

Security check: a loopback/private URL must be rejected, without a confirmation or
new job. This passed in the actual packaged Chromium workflow ate3298e68ce, including the
policy reason, safe guidance, no confirmation and no-job assertions. Native
policy/remux/queue/publication and owned-media API tests passed at application1ac;
public YouTube extraction/download remains unverified because workspace source
access failed/returnedHTTP403. Do not count that restriction as a successful test.
Thumbnails/subtitles, playlists/channels/synchronization and independent online
creator metadata remain unfinished. Report error code/job ID, logs, source
permission/availability, server/browser versions and reproduction steps; omit
passwords, cookies, tokens and private URLs.
