# YouTube source-adapter development checkpoint

YouTube importing is not yet publicly enabled. The working direct-MP4 queue,
rights confirmation, persistent recovery, SSRF controls and library publication
remain unchanged. This prerequisite adds an internal bounded metadata preview,
not a completed YouTube import feature.

## Reviewed dependencies

Linux x64 packages include Node 24.22.1 under
`server/tools/node/24.22.1/linux-x64/`, alongside its upstream LICENSE. The official
archive, extracted ELF executable and license are checksum-pinned in
`packaging/node/release.json`; the server embeds that same manifest. Both CI and
the mandatory local application gate prepare and execute the actual reviewed
binary. The bundler selects only regular binary/license archive entries, rejects
links/duplicates/checksum mismatches, refuses arbitrary redirects and never
replaces different cached bytes. No system Node installation is needed by users.

The existing pinned yt-dlp release includes its EJS solver assets. Extraction
uses its reviewed Node provider, which runs Node with `--permission`. Remote
components, plugins, ambient configuration and arbitrary JS runtimes remain
disabled. Preview processes clear inherited environment, use private directories,
a 256MiB Node heap limit, a 90-second deadline and bounded diagnostic/JSON output.
All outbound connections still use the existing authenticated SSRF-protected
CONNECT tunnel, public-address checks and normal HTTPS certificate validation.

Supported source identities are plain HTTPS watch/share/shorts/live/embed links
with an exact eleven-character video ID on approved YouTube hosts. Tracking and
playlist context are discarded; playlists/channels are never expanded here.
Encoded/ambiguous identifiers, credentials and unsupported parameters are rejected.
Metadata must identify the requested video and a stable creator channel; DRM/live
sources, conflicting identities and unreliable durations are rejected. Returned
metadata contains a fixed whitelist and safe numeric progressive-format choices,
never signed stream URLs or raw downloader output.

## Verification and authorized external test

Owned JSON/archive fixtures test identity conflicts, protected sources, duplicate
keys, environment isolation, archive corruption and unsafe output paths. Native
runtime tests execute the pinned Node binary and verify its filesystem permission
sandbox. Existing native FFprobe/FFmpeg/downloader tests protect process handling.

The owner explicitly authorized `https://youtu.be/4Xsn6oe-AEg`. Its real preview
was attempted on 2026-11-14 using the reviewed runtimes and unchanged network
policy. The attempt failed with `sourceNetwork` after approximately14seconds:
normal outbound source connections could not be established in this workspace.
This is not a successful metadata/download/indexing/playback test. No cookies,
authentication bypass, alternate network policy or certificate override was used.

The opt-in integration test remains reproducible in an environment with normal
public egress and permission for the selected video:

```bash
export CASTLUMORA_TEST_YTDLP_BUNDLE=/path/to/reviewed/system-root
export CASTLUMORA_TEST_NODE_BUNDLE=/path/to/reviewed/system-root
export CASTLUMORA_TEST_AUTHORIZED_YOUTUBE_URL=https://youtu.be/4Xsn6oe-AEg
dotnet test tests/Jellyfin.Api.Tests -c Release \
  --filter FullyQualifiedName~AuthorizedExternalSource_UsesManagedRuntimeAndNormalNetworkPolicy
```

Without explicit external-fixture authorization that test is skipped, separately
from the mandatory offline/native regression tests. It must fail if an opted-in
source cannot be previewed; failures are not silently treated as passes.

## Exact next milestone

Complete the YouTube download adapter on top of the persistent queue: preview/
rights/destination confirmation, stable video-ID deduplication, controlled format
and resource limits, secure audio/video staging and local muxing, safe creator/
thumbnail/provenance publication, scanning and playback verification. Recheck
real source availability through normal egress before claiming end-to-end success.
Only then expose the source type in the administrator UI. Playlist/channel/bulk
synchronization and independent creator metadata remain subsequent requirements.
