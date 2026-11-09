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
