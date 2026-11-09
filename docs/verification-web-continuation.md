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
