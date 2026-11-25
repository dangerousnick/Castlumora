# Native music and private playlists

Castlumora Web now has native album/album-artist/track browsing, search and pagination, album/artist detail navigation, and a persistent audio queue. Browse → Music & playlists (also in the profile menu) opens the music library. Album/playlist Play these tracks queues the displayed page; paging does not silently imply that every track in a very large collection was loaded.

The queue continues across library/detail navigation. It supports previous/next, shuffle, repeat off/queue/track, volume and absolute seeking. Volume stays with the queue across track changes. Starting video clears music to avoid overlapping playback. Signing out/unmounting the application also closes the current audio session.

The player negotiates an audio-only browser profile through the existing PlaybackInfo API. Supported browser containers/codecs can play directly; Compatible audio requests the inherited HTTP MP3 fallback. Track/seek/source changes clean up only the negotiated play session. Existing start/progress/pause/stop reporting and server playback/transcoding permissions remain authoritative. Media URLs are restricted to the paired server/base path and carry the existing authenticated media token; no foreign source URL receives that token.

Private playlists use the inherited playlist API and permissions. New music playlists are private by default. Owners/editable shares can add searched accessible tracks, rename, reorder and remove playlist entries. Reorder/removal use the server-supplied PlaylistItemId, not a guessed media item ID; rename sends only Name and preserves sharing fields. Deleting a playlist removes its list, not its linked media files. Other inherited client interfaces remain available.

## Verification

Unit/API tests cover private playlist creation, non-destructive rename, playlist entry IDs, audio-only negotiation, queue continuity across navigation, own-session cleanup, exact-zero repeat and volume persistence. Actual disposable-server Chromium checks use two tagged synthetic MP3s and verify artist → album → tracks, direct playback/seeking, background navigation, track transition, MP3 fallback, progress/stop reporting and private playlist create/add/reorder/remove/rename/delete. Video coexistence is checked separately.

To reproduce after the documented disposable movie/server setup, set CASTLUMORA_TEST_SERVER/USER/PASSWORD and create the explicit `.castlumora-disposable` marker. The helper checks loopback and the server's ProgramDataPath before creating/indexing only synthetic music:

```bash
python3 browser/setup-music-fixture.py --fixture-root /tmp/castlumora-web-real-server
CASTLUMORA_TEST_MUSIC=1 npm run test:browser -- browser/real-server.spec.ts -g 'native music queue|starting video closes'
```

Vite must be running. Existing matching media is not overwritten; a conflicting fixture library path is rejected. Failed/interrupted browser runs may leave a synthetic playlist/session in the disposable server; inspect/remove only that fixture before repeating. Never target production or Coon Media.

## Remaining music scope

This milestone does not certify all codecs/browsers/devices, hardware behavior, long-running queues or huge libraries. Lyrics, richer metadata/artwork editing, playlist sharing controls, multi-disc/genre/composer ergonomics and native TV/mobile music renderers remain further work. No feature paywall, backend namespace/API rename, database migration or deployment is introduced.
