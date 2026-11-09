# Master-spec completion continuation

This report extends, rather than replaces, the initial Phase 1.3 verification. The platform remains under active development until the full retained requirements and parity inventory are satisfied.

## Playback milestone

Added selectable bitrate caps and source versions, an explicit absolute-position timeline (including exact zero seeks), chapter selection, next-episode action, and more accurate DirectPlay/DirectStream/Transcode reporting. Track and quality changes preserve position and paused state. Existing server-negotiated HLS remains the streaming path.

Actual loopback test server with normal FFmpeg 7.1.5, synthetic 90-second H264 video, English/French AAC tracks, external VTT subtitles and two chapters:

- Direct playback and start/progress/stop: passed.
- Direct seeking, server-negotiated HLS playback and cleanup: passed.
- Real bitrate-cap negotiation, audio-track switching and external VTT loading: passed.
- The browser checks successful PlaybackInfo responses, advancing video, loaded subtitle track and own-session cleanup, rather than only checking rendered controls.

Automated adapter coverage now additionally checks selected source ID, bitrate, tracks and exact zero start ticks. Actual alternate-version fixtures and broad hardware/codec/native-device coverage remain necessary; one synthetic browser profile is not universal certification.

No Coon Media access, production deployment or merge. Work continues through the retained master specification.

## Categories, revision history and administration

Server category selection now precedes pagination and retains the authenticated user's allowed-library scope even for explicit item IDs. Physical/configured folder names use the existing library manager's collection-folder expansion. Empty rules produce zero items. Actual isolated-server checks verified genre, folder and manual matches, totals/page offsets, exclusion and no results for a user with no allowed libraries.

Published snapshots retain the latest 100 revisions. Administrator restoration creates a new draft with a revision conflict check; it does not change the published layout until publication. Unit/HTTP and fixture-browser checks cover this boundary and administrator authorization.

Native administrator screens use authenticated server APIs for libraries, policies/devices, encoding, network/general settings, tasks/logs, plugins/repositories, backup actions and tuner/listing configuration. Existing config fields are retained when changing known settings. Real-server Chromium verification changed an encoding CRF, reloaded to verify persistence, restored it, and loaded libraries/users/logs. Frontend: 22 tests pass; typecheck, lint and production build pass; 2 fixture browser cases pass. API: 168 pass, 3 Windows-only skips. Extended HTTP presentation test passes.

Plugin installation, backup restoration, restart/shutdown, real tuner/channel mapping, hardware acceleration and complete parental-policy combinations are not certified by these checks. Those require additional fixtures. No production server was contacted.

## Native Live TV/DVR milestone

Actual synthetic two-channel M3U/XMLTV checks now pass for single timer creation, padding persistence and update, cancellation, series-rule creation/cancellation, live playback/channel switching and successful own-stream closure on switch and exit. Two Chromium cases require CASTLUMORA_TEST_LIVE=1; they remain opt-in so an unrelated server is never assumed to have tuners. The suite enforces a loopback URL. All 25 frontend tests, typecheck/lint/build pass. Latest normal API analyzer build: 0 warnings/errors.

To recreate the guide fixture after the documented disposable server/movie setup, set the same CASTLUMORA_TEST_SERVER/USER/PASSWORD environment variables used by real-server.spec.ts. Create a .castlumora-disposable marker in the temporary fixture root, then run:

```bash
python3 browser/setup-live-fixture.py --fixture-root /tmp/castlumora-web-real-server
python3 -m http.server 18097 --bind 127.0.0.1 --directory /tmp/castlumora-web-real-server/media
```

Serve **only media**, never the configuration/credential directory. Wait for the Refresh Guide task to complete, then in another terminal with the same test credentials and Vite running:

```bash
CASTLUMORA_TEST_LIVE=1 npm run test:browser -- browser/real-server.spec.ts -g synthetic
```

The helper refuses non-loopback hosts, missing disposable markers and any existing tuner/listing sources. Schedules are for a future synthetic guide program and are cancelled by the successful test. A failed/interrupted test may leave synthetic schedules: inspect/cancel them in the disposable server before repeating. Recreate the disposable configuration when the guide dates become stale. Do not use this against production.

These checks do not yet certify completed recording files, deletion, long-duration playback/timeshift, physical tuners or a large grouped channel catalog.

## Native skipper milestone

Implemented native optional repeated-audio analysis and central review/correction/approval, post-credit protection, existing scheduled extraction integration and per-profile browser manual/automatic/disabled behavior. Actual synthetic detection found 10–40s intro and 145–169.5s outro; unreviewed suggestions were absent from MediaSegments. Approval exposed exact bounded intervals; actual Chromium skip modes and administrator interval correction/restoration pass. The guarded verifier also passed on a second untouched synthetic episode. See CASTLUMORA_SKIPPER.md for algorithms, limits and commands.

Final checks for this milestone: frontend typecheck/lint/build and 28 tests pass; 2 editor/mobile fixture cases pass; all 7 actual-server browser cases pass across the complete run and targeted reruns. The expanded series fixture exposed an old test assumption that the first title was a movie; movie tests now select their explicit synthetic movie. API suite: 177 pass, 3 Windows-specific skips. Full integration suite: 130 pass, 3 existing skips. Normal API analyzer build: 0 warnings/errors. Isolated executable build succeeds. The native provider's session dependency is deferred; actual startup and HTTP tests verify the registration.

No real-series accuracy, physical tuner/TV/mobile or long-duration DVR certification. A stale session from an interrupted test conservatively postpones analysis; no production sessions were affected. API build outputs were refreshed only for the stopped disposable server. No production access, merge or deployment.
