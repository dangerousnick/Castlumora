# Internal import media inspection

This is an internal security/validation component, not an enabled public importer.
The persistent queue, validated network tunnel and managed downloader remain the
existing foundation. No library destination, publication, scan or URL endpoint is
registered by this change; `downloadJobsAvailable` remains false.

## Accepted initial media

The first increment accepts bounded, non-fragmented, self-contained H264 MP4 with
optional standard AAC audio. It requires one file-type box first, one movie box,
one nonempty media-data box, bounded valid box lengths/nesting/counts, single sample
descriptions and explicitly self-contained data references. External URL/URN
references, alternate data-reference indexes, encrypted sample entries/protection
boxes, fragments and unsupported top-level structures are rejected before probing.
This intentionally rejects some legitimate MP4 variants; it does not implement
DRM, broader codec/container conversion, streaming manifests or playlist imports.

The existing32MiB staging limit remains. Structural inspection limits nesting to8
and boxes to4096, with bounded reads/seeks rather than allocating the media in
memory. Native FFprobe must report H264 video, optional AAC audio, at most8 streams,
positive dimensions no greater than4096 each and finite duration up to3hours.
Native FFmpeg must then actually decode the first video frame. Valid headers and
duration alone cannot complete inspection: the generated damaged-packet fixture
must fail at decoding. This checks the first frame, **not every frame or the entire
playback/codec/device matrix**.

## Process and file safety

Both child commands use the existing owned-process lifetime helper, fixed argument
lists, no shell, a cleared environment and a server-owned absolute input path.
The input format allowlist is `mov`, input protocol allowlist `file`; external MOV
data references and absolute external references are disabled. The structural
reference checks run before either child starts. Probe output contains selected
codec/dimension/duration fields only, not source URLs or raw downloaded sidecars.
The decoder writes to the null muxer with bounded progress output, requires a
reported first frame, and disables audio/subtitle/data outputs.

Each process has a30second deadline and16384character limits for each diagnostic
stream. Decoder/filter threads are1 and individual native allocations are capped
at16MiB. These are **not aggregate RSS/CPU/cgroup quotas or an operating-system
sandbox**. Keep supported native libraries patched. Full resource containment and
representative media/device testing remain required before production readiness.
No shared playback/FFmpeg process is killed or reconfigured; only the child owned
by the existing process helper is stopped on failure/cancellation/deadline.

Production integration must use the inherited server-configured EncoderPath and
ProbePath; no ordinary-user executable selector or environment override is added.
Tests use explicit `CASTLUMORA_TEST_FFPROBE` and `CASTLUMORA_TEST_FFMPEG` absolute
paths to real binaries. The mandatory local gate and CI prepare/check these native
fixtures, log their versions and refuse silent skips. CI uses existing runner
packages or official Ubuntu/Homebrew ffmpeg packages if absent. No new NuGet/npm
package or installer/update mechanism is introduced.

## Verification and remaining boundary

The committed synthetic purple-frame/tone fixture contains no third-party media.
Tests use its real container and native decoder, targeted mutations, malformed
probe properties and link/argument/environment checks. The combined queue/real
pinned-downloader/TLS/inspection cases require valid media before a private
completion receipt and reject transport-only bytes without retaining staging.
Tests do not assert that media was promoted or indexed: that remains the next
implementation boundary. See DEVELOPMENT_PROGRESS.md for exact results, failures,
source hashes and required local/CI validation status.

Next: resolve real authorized generic-video library IDs, validate destinations,
reconcile per-job publication receipts across crashes, sanitize portable metadata,
atomically publish complete local media, deduplicate content and trigger scanning.
Then test real indexing/playback and elevated native queue management before
public submission is enabled. All YouTube, playlist/channel/bulk/scheduling and
other permanent1.0 requirements remain mandatory.
