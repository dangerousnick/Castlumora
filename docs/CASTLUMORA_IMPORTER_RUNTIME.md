# Managed yt-dlp runtime — completed prerequisite

This increment supplies an administrator-owned executable lifecycle, not the media-import queue. **No URL downloading, playlist/channel processing or scheduled imports are enabled yet.** Full authorized importing remains a required Castlumora 1.0 feature. Existing native creator sidecars/Metadata Management work independently and are unchanged.

## Owner workflow

Use a complete new Linux development preview, finish setup and sign in as administrator. Open **Administration → Imports & Sources**. The page reports the selected platform, reviewed version, integrity state and whether the bundled asset is present. Choose **Install bundled runtime**, confirm, then use **Check runtime**. Ready means the executable matches the pinned hash and returned the exact expected version; it does not mean importing, FFmpeg, JS challenge runtimes or source permissions are ready. URL/job controls are deliberately absent until implemented.

The preview carries the Linux x64 executable and its notices/source materials. Installation is offline and requires no separate downloader/plugin or hand-operated yt-dlp command. Development builds without a bundled asset show a meaningful missing-bundle state. Windows x64 uses the separately pinned official .exe; Linux x64 uses the glibc Linux asset. Other OS/architectures return unsupported without affecting existing server capabilities. Windows distribution packaging and broader OS support remain future work; CI checks the actual selected Windows/Linux binaries separately.

## Integrity, updates and storage

The reviewed release is **2026.10.26**, pinned in `packaging/yt-dlp/release.json`, embedded into the server assembly and used by the build bundler. Official API digests and release SHA2-256SUMS agreed:

- Linux x64 yt-dlp_linux: `767ba9dce7052aa196f8b12652f433d76de128ba168966fa84b6394d5e6c6795` (41,166,968 bytes).
- Windows x64 yt-dlp.exe: `f5389d3698ecde4ef06f4e6b1ce0294fe9ac1396228c8990b8d3416d6d177a7d` (23,575,744 bytes).

Only assets shipped under the application's `tools/yt-dlp/<version>/<platform>/` can be installed. No API accepts arbitrary paths, versions, flags or executable URLs. The service copies to the server DataPath beneath `castlumora/tools/yt-dlp/<version>/<platform>/`, verifies exact size/hash again, checks the candidate version, and atomically replaces the active file. Failed/canceled candidates do not overwrite the previous executable. Old version directories are retained for rollback with their matching application package; no automatic updater runs. A reviewed package/manifest change is the controlled update mechanism.

Managed/bundled path components reject symlinks/reparse points. Linux owned tool/temp directories and executable use owner-only permissions; Windows uses its platform file/loader behavior. Hash verification precedes **every** health execution. A status read never installs or downloads. Runtime state survives restart as verified files, without a database migration. A crash during copy can leave an ignored nonce .tmp file; a retry validates a fresh candidate, never executes an incomplete file. Cleanup of abandoned files after abrupt OS termination is a later housekeeping improvement.

Health invokes only `--ignore-config --no-plugin-dirs --version` with shell execution disabled, a clean child environment, private per-check extraction directory, a 15-second execution timeout, 4,096-character caps on each output stream and bounded cleanup waits. Cancellation/oversized output stops the owned process tree. It does not kill shared FFmpeg sessions or other processes. These narrow health checks do not claim to implement full long-running import/FFmpeg process-group/Job Object supervision; that remains required where appropriate for the download engine.

Endpoints `GET Castlumora/Imports/Runtime` and `POST Castlumora/Imports/Runtime/Install` require existing administrator elevation. Anonymous/viewer reads and writes are denied independently of the UI. `downloadJobsAvailable=false` is explicit. No source URL/cookie/DRM handling exists in this runtime prerequisite.

## Supply chain and licensing

Build-time `packaging/yt-dlp/bundle.py` uses only fixed official HTTPS release hosts, validates redirects, bounds streamed length, checks the reviewed SHA before promotion and never executes downloads. Unknown existing files are not overwritten; it does not remove another operation's temporary download. CI/preview packaging run five security tests for these boundaries.

The unmodified pinned executable, verified release source archive, original LICENSE, complete upstream THIRD_PARTY_LICENSES.txt and Castlumora NOTICE.md are shipped together. yt-dlp itself is Unlicense, but the PyInstaller standalone executable combines GPLv3+ and other components: do not mislabel it Unlicense-only. Preserve complete upstream notices/source references and source materials when redistributing. Existing Castlumora/Jellyfin GPL attribution is unchanged.

Developer preparation: `python3 packaging/yt-dlp/bundle.py --platform linux-x64 --system-root /path/to/server` (or win-x64 on Windows). The output is an application distribution input; administrators use the native installer instead. Updates change the reviewed manifest and package, not a live arbitrary-download setting.

## Verification and limits

Local Release API/server builds passed. API regression suite: **205 passed, 3 existing Windows-specific skips**, including ten new runtime cases and actual pinned Linux installation/reopen health. Full HTTP integration suite: **132 passed, 3 existing skips**; new test covers anonymous/viewer denial, missing-bundle failure and ignored malicious executable/URL/flag input. Frontend **58 tests/typecheck/lint/build** passed. Five bundler security tests passed. Targeted repository formatter passed with workspace-load warnings; GitHub checks remain the authoritative clean-workspace confirmation after publishing.

Actual compiled server + compiled web on the marked owned loopback fixture passed native install, version/hash readiness, reload/recheck and viewer denials. A rerun waits for the actual reinstall response before reload, avoiding a false positive from the previously ready executable. An excessive-output test found a temporary-directory cleanup issue; fixed by observing that exception before cleanup and rerun successfully. CI runs the real approved Linux and Windows assets through the same install/reopen test, and the Linux preview restart workflow installs/checks its bundled runtime after real library/playback testing. Record executed CI/package results in DEVELOPMENT_PROGRESS.md; configuring a job is not proof it passed.

Remaining importer work: authorized source policy, strict egress/DNS/redirect/SSRF protections for subprocess network access, destination/filename isolation, persistent queue/history/cancel/retry/resume, quality/subtitles/thumbnails, provenance/deduplication, scan-on-success, bulk/playlist/channel/scheduled sources and resource controls. Do not treat version readiness as production import readiness. No real Mint hardware certification or claim of full 1.0 acceptance.
