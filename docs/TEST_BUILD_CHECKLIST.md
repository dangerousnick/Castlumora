# Castlumora development preview test checklist

Use an isolated installation and copies/authorized fixtures. Record BUILD_INFO.json commit, OS/CPU/RAM/free space, FFmpeg/browser versions and test date. “Prior automated” means source/disposable-server tests before packaging; the release verification report identifies actual archive tests. No physical GPU/tuner/Mint manual testing is implied.

| Feature / location | Steps and expected result | Coverage and limits |
| --- | --- | --- |
| Fresh install | Extract; run start-castlumora; visit :28096/web; create admin; sign in | Actual packaged smoke required; local-only preview data, no service install |
| Branding/navigation | Purple castle; Home/Browse/My List/Search; open native admin | Prior browser checks; compiled UI must load from packaged server |
| Account security | Sign out/in; create limited user; deny admin routes/library not allowed | Inherited auth + prior HTTP/browser tests; linked profiles/presets unfinished |
| Libraries | Admin → Libraries; add copied Movie/TV/music/photo/book folder; scan; Browse | Prior actual synthetic catalog/browser checks; packaged report distinguishes newly rerun workflows |
| Metadata/artwork | Open movie/episode; verify title/artwork/tracks | Existing providers retained; creator sidecar/YouTube provider and native metadata repair unfinished |
| Movie/episode play | Watch now; pause/seek/exit/resume; choose quality/audio/subtitles/chapter | Prior direct/HLS/track browser checks; actual package smoke; codec/device matrix unfinished |
| Music | Browse → Music; album/artist/track; background navigation; private playlist add/move/remove/rename | Prior actual-server tests; lyrics/sharing/large-scale compatibility pending |
| Photos/books | Browse → Photos/Books; rotate/next; PDF page/EPUB chapter; reload position/theme | Prior actual-server browser tests; more ebook formats/offline/annotations pending |
| Collections/My List | Create collection/add/remove/delete; original files remain; add/remove My List | Prior actual-server tests; no file movement/duplication |
| Resume/Up Next | Play part of title; options → remove resume; verify progress retained | Prior preference/menu tests; follows/notifications remain separate and unfinished |
| Live TV | Admin → Live TV; authorized M3U/XMLTV; Guide; play/switch channels; schedule/edit padding/cancel | Prior synthetic guide/live/scheduling tests; completed DVR lifecycle, real tuners/groups/quotas unverified |
| Skipper | Admin → Skipper enable/analyze/review/approve; play episode; manual/automatic/disabled skip | Prior synthetic detection/browser tests; no intervals until approved; post-credit boundary; broad real-series/native-device accuracy pending |
| Layout/categories | Admin → Presentation; draft/reorder/category/profile scope; publish; reload; restore history to draft | Prior HTTP/browser tests; package persistence smoke; advanced preview/plugin/native renderer work pending |
| Encoding | Admin → Playback/Encoding; inspect codecs/hardware settings | UI/API available; no supported GPU exists here, do not enable unsupported acceleration |
| Plugins/network | Admin → Plugins/System; inspect repos/catalog/network | Native screens retained; real plugin installation, discovery/proxy/TLS deployment pending |
| Logs/tasks/backups | Admin → System; view logs/tasks/triggers/backup controls | Inherited backend retained; destructive actions/restore unverified; back up preview before experimenting |
| Advanced legacy | Existing client opens help unless compatible compiled legacy assets are attached | Not bundled; see INSTALL_TEST_BUILD.md; no false parity claim |
| Importer/provider | Consult feature inventory | Mandatory planned feature, absent from this preview; no production-ready claim |
| Restart/removal | Ctrl+C; restart same state; verify account/layout; stop and delete only preview folder/state | Package smoke required; do not touch original media/another server |

## Report a bug

Open a GitHub issue with exact commit/package/checksum, environment, numbered steps, expected/observed behavior, timestamp, relevant redacted log excerpt and optional screenshot. Include whether a feature was verified/experimental, media codec/container (not copyrighted files), and whether direct or compatible playback failed. Never include passwords, access tokens, private media URLs, databases or invitation links.
