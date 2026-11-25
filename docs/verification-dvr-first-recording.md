# First-recording indexing correction — verified development preview

Tested application: `82e78af324554110ec2709de607261ae23e947d3`.
Branch: `development/castlumora-web-1.3`; PR3 remains open, not merged.

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

## Complete gate and hosted checks passed

The normal pre-push gate passed before publishing source82e78af32455:

- .NET10.0.403, complete restored formatting2457files/0changes/no workspace warnings.
- Debug and Release builds:0warnings/0errors.
- All17backend suites:4622passes/20existing skips/0failures, including backup,
  migration, security, native importer and all86Live TV tests.
- Node24.22.1: typecheck/lint/108tests in28files/production build. The wrapper runs
  four Python Live TV guard cases. Hook/native/report-handler checks also passed.

Exact-source hosted Format45000102922, OpenAPI Check45000102920,
ABI45000102956, Tests45000102945/job137139619286 and
preview45000102921/job137139604733 all passed. Downloaded hosted backend logs
independently total17suites/4622passes/20existing skips/0failures.
Preview logs confirm108frontend tests/four Python guards and six real extracted-
package browser executions, retaining account/profile/password/device/media/recovery
and existing authorized-import regressions. No production backup reserve changed.

## NEW downloaded package independently tested

Download: https://github.com/dangerousnick/Castlumora/actions/runs/45000102921/artifacts/14745438125

Archive: `Castlumora-1.0-dev-82e78af32455-linux-x64.tar.gz`.
SHA-256: `eaefaace5329b7c3012100e6f8e3ac09d400c9ce6ad8165bfe2b32c2f11208f2`.
ZIP SHA-256: `653e5b2b3fab696c63f64a4bec963bb374644b72e7de0560f39409ae150fc9a7`.

Independently verified actual ZIP/archive hashes, exact source manifest, compiled
server/API/UI, bundled .NET and pinned Node binary/license hashes, startup executable
modes, sixteen branding files, GPL/other licenses and exact-source documentation.
No private profile registry/credential key is packaged. Installation instructions,
feature checklist and CI smoke report are in the delivered outer ZIP/archive.

The extracted archive itself ran on new marked Debian13 loopback fixtures with
stable system FFmpeg7.1.5, SDK runtime paths removed. Process maps confirmed its
bundled libcoreclr. Actual fresh setup/login/native library scanning/movie playback,
complete DVR lifecycle1.9minutes and active-stop partial playback1.2minutes passed.
The new aggregate-root fallback was observed in the package's own log. Original
movie preservation, viewer denial, retained file after library-only removal, explicit
rescan/re-index and real recording deletion remained asserted.

HLS initially failed a test precondition: the fresh account selected English
subtitles automatically, so playback already used HLS with a relative video time.
Its logical timeline exceeded9seconds while raw video time was only3seconds; the
unchanged test expects initial Direct Play before explicitly selecting compatible
streaming. Setting ONLY the disposable account's subtitle default to None supplied
that precondition; automatic-subtitle production behavior and assertions were not
changed. The unchanged HLS/quality/audio/manual-subtitle/loaded-track/encoding-
cleanup workflow then passed in7.1seconds. Four distinct additional browser
executions cover setup/movie, completed DVR, active stop and HLS.

The temporary restart driver initially authenticated after /health was live but
before API startup completed, receiving503, then interrupted initialization during
cleanup. /health is liveness, not full API readiness. Correcting orchestration to
wait for successful System/Info/Public readiness allowed the same retained fixture
to pass actual authentication, saved preferences, indexed retained partial media,
reconfirmed bundled runtime and clean native administrator shutdown. No mutation
was blindly retried and no test assertion was relaxed. Earlier logs were retained.
All owned test processes are stopped and temporary fixture passwords removed.

## Limitations and recovery

This is a development preview, not completed Phase1.4 or stable1.0. No physical Mint,
NAS, GPU/hardware-acceleration, real tuner or macOS/Windows support certification is
claimed. Actual runtime evidence is Ubuntu24.04 CI plus the Debian13 test above.
All remaining non-YouTube requirements remain. Production backup reserves stay in
force; real production-size backup creation on this small workspace is not claimed.

This report was completed after the build and lives in the repository/PR. The
archive remains reproducible from its exact source82e78af32455; its snapshot docs
precede final post-build results. Actions artifacts require GitHub sign-in and expire.
The separate main-only OpenAPI Report still requires reviewed adoption of the
existing development fix; it is distinct from the passing OpenAPI Check.

Resume instructions and the next account-owner/profile-session milestone are in
DEVELOPMENT_PROGRESS.md. No main merge, production deployment, official Jellyfin
change or Coon Media access occurred.
