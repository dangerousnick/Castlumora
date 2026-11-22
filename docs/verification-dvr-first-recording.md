# First-recording indexing correction — verification in progress

## Reproduced defect

The independently downloaded package at source
`ebab7567cfa7c42cbdac3ac438a236372c79427f` launched on Debian13 with its bundled
.NET runtime and stable system FFmpeg7.1.5. Fresh setup/login, native movie library
creation/scanning and actual Chromium movie playback passed. Process maps proved
libcoreclr came from the extracted package, with no workspace SDK runtime mapped.

Real DVR recording completed with FFmpeg exit0,389160bytes and22.73seconds of TS,
but LiveTv/Recordings stayed empty. The complete browser workflow failed; cleanup
restored only owned settings. SQLite showed the logical CollectionFolder registered
but no indexed physical recording root. TriggerRefresh returned without queueing
when no indexed ancestor existed. This was an application defect, not low disk.
The original downloaded archive remains unmodified and retains that limitation.

## Correction

For a nonempty server-generated recording directory, preserve the nearest indexed
ancestor when present; otherwise queue existing aggregate-root validation. Existing
RefreshPaths limit metadata refresh to the recording and its directories. Structural
validation may visit configured library roots on this uncommon first-recording
fallback. It does not create arbitrary media roots, change authentication, rewrite
history, lower backup reserves or alter FFmpeg/tuner policy. A missing directory
still returns null. The selection helper is internal, with no public API change.

The new focused regression failed before correction:1failure/2passes. All3passed
afterward. Fixed Release server build passed with0warnings/0errors.

## Real fixed-server workflows actually run

A separate new, marked loopback fixture on28101used the fixed compiled Release
server, unchanged compiled web from the original package, stable FFmpeg7.1.5 and
only owned synthetic media served on127.0.0.1:18097. It is not claimed to be a new
self-contained fixed build. Fresh setup/login/library/movie playback passed.

The unchanged DVR lifecycle workflow passed in1.9minutes:

- Schedule two programs, cancel one, record/complete the other.
- First recording appears automatically without an added manual scan.
- Viewer cannot remove/delete recorded media; administrator can play it.
- Library-only removal retains the file; explicit rescan re-indexes it.
- Confirmed file deletion removes only the recording, preserving original media.

The unchanged active-stop workflow passed in1.2minutes:

- Real recording reaches InProgress and writes nonempty data.
- Viewer cancellation is denied.
- Administrator stops before scheduled end, with retention confirmation.
- Partial file stays nonempty, is indexed and actually plays in Chromium.

The existing HLS browser workflow also passed in7seconds, including compatible
HLS200, quality changes, alternate audio, loaded subtitles and ActiveEncodings204
cleanup. Initial setup omitted the owned VTT sidecar; its first attempt correctly
failed on missing subtitle options. Copying/indexing the original owned VTT fixed
the fixture and all unchanged assertions passed. No production playback change
or relaxed expectation was needed for that fixture issue.

Both DVR workflows restored only fixture guide/options/policy and removed owned viewers.
The owned server/media processes are stopped; temporary plaintext fixture passwords
were removed, while non-secret test state/log evidence was retained.
Failure and successful fixture state/logs are preserved separately. No Coon Media,
production data, official Jellyfin or main branch was modified.

## Harness protections

DVR cases accept an explicit fixture root and integrated-web pairing while retaining
marker, loopback and actual ProgramDataPath checks. Live TV setup now also verifies
actual ProgramDataPath before configuration access/writes. Four Python regressions
cover missing marker, remote address, mismatched root and matching root retaining
existing-source protection. The ordinary Vitest command invokes that test file,
so normal pre-push/CI frontend tests cannot silently omit it.

## Pending publication checks

Complete restored formatting, Debug/Release builds, all backend suites and frontend
checks must pass through the normal hook. Exact-source Linux/shared CI and a NEW
self-contained preview must pass. Independently download/checksum that new archive
and rerun actual DVR workflows against it before claiming packaged correction.
Record exact final SHA/artifact/checksum in DEVELOPMENT_PROGRESS.md.
This is a development milestone, not full1.0acceptance, physical Mint/NAS/GPU or
real-tuner certification. Further YouTube expansion remains deferred; all other
requirements remain. Production backup limits are unchanged.
