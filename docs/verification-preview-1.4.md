# Delivered GitHub Actions preview

**Download:** https://github.com/dangerousnick/Castlumora/actions/runs/42702481467/artifacts/13712785582 (GitHub sign-in, 30-day retention).

**Exact delivered source commit:** `cc59df91a8c06364d6ca1e6944ba938031754bd7`.

**Archive:** `Castlumora-1.0-dev-cc59df91a8c0-linux-x64.tar.gz`.

**SHA-256:** `57dd13e5ad925d3f9c9baa0bfb0779f505f50e3c17e4a63dca984f5e709ff89a`.

GitHub-hosted **Ubuntu 24.04 x86-64** run 42702481467 succeeded in source checkout, pinned toolchain setup, native dependencies, clean frontend/tests/self-contained server/archive build, browser installation, checksum/extraction, actual packaged fresh setup/auth/admin/logout/security, normal restart/account persistence/native library creation/scanning/movie playback, CI report creation and artifact upload. Contents permission is read-only; it does not publish releases or merge/deploy. Exact source is checked out from PR head, not an untested merge commit.

Artifact metadata confirmed ID 13712785582, 79,471,478 bytes, not expired, expiry 2026-12-09T21:30:11Z. The workspace CLI download followed GitHub to Azure and returned 403; the connected GitHub app successfully downloaded the artifact. Retrieved ZIP bytes were inspected for unsafe paths, extracted and checked: **archive checksum OK**, manifest exact source commit match. This verifies an actual uploaded/downloaded build, not merely a source archive or CI log. The ZIP contains archive/checksum/BUILD_INFO/INSTALL_TEST_BUILD/TEST_BUILD_CHECKLIST/CI_SMOKE_REPORT.

Workspace release uploads had returned 401 from uploads.github.com; the empty failed unpublished draft was removed. Automatic approval review rejected persistent PR-triggered release-write permissions. The narrower read-only artifact delivery completed the authorized alternative. No source/features were discarded, no official Jellyfin repository was modified, and no Coon Media or production server was accessed or deployed.

The full local packaged media evidence below uses earlier application commit `8d591a4d58cc705de1668fb7dc63a0b013cc35e4`. Production frontend/backend sources are unchanged between that commit and the delivered CI commit; only delivery/docs/browser harness follow it. Broader 13-workflow checks below are local packaged tests, not falsely claimed as executed on GitHub. The delivered CI independently ran its two actual-package smoke cases. Current install/checklist and retained product requirements define remaining limitations.

---

# Castlumora 1.0 Development Preview — packaged-output verification

Application source commit: **8d591a4d58cc705de1668fb7dc63a0b013cc35e4**.
Working branch: development/castlumora-web-1.3. PR: https://github.com/dangerousnick/Castlumora/pull/3 (not merged).
Archive: `Castlumora-1.0-dev-8d591a4d58cc-linux-x64.tar.gz`.
SHA-256: `f5c861b1c0d202de0920d887d5a73d3a86ed4657d452b723d0cdbd5e4b52a5ad`.

## Actual build and environment

Built using packaging/linux/build-preview.sh from the clean exact source commit above. Node 24/npm, .NET SDK 10.0.403, Debian GNU/Linux 13 x86-64. Self-contained server includes .NET/ASP.NET Core 10.0.14; no installed .NET runtime or Node is needed to run it. Native ICU/OpenSSL/font libraries and compatible FFmpeg/ffprobe remain external and are documented. Normal FFmpeg 7.1.5 validation was used. Intended install target is Linux Mint 22.3/Ubuntu-compatible x86-64; a real Mint machine/VM was not available, so Mint-specific manual certification is pending.

Frontend npm ci/typecheck/lint/build and **44 tests passed**. Release self-contained linux-x64 publish succeeded with no C# warning/error lines in the build log. Shell syntax and license-collector Python compilation passed. Vite reports existing lazy chunk-size advisories and strips two misplaced PURE annotation comments in Zod; builds succeed.

The first preliminary archive revealed a tar timestamp lookup outside the repository; that script was corrected and the final archive rebuilt. The preliminary archive is not the release asset. Initial startup correctly rejected insufficient disk space; re-downloadable caches, reproducible Debug outputs and duplicate staging were removed, then startup succeeded with the unchanged 2 GiB free-space guard. Source and media were preserved.

## Tests on the actual extracted final archive

Launched `start-castlumora` with DOTNET_ROOT/DOTNET_ROOT_X64 unset and PATH=/usr/bin:/bin. The server served the compiled production Web assets itself; Vite was not used for these tests. Two isolated loopback data directories were used, never production/Coon Media.

1. **Fresh package workflow passed**: configuration files created; purple castle UI; integrated server pairing; administrator setup via existing Startup APIs; login; native administration; legacy-help disclosure; unauthenticated startup writes denied after setup; logout. Passwords were generated privately and are not distributed.
2. **Restart/library workflow passed**: normal termination/restart retained the administrator and completed setup; native Libraries UI created a new library; synthetic movie scanning; browse/details; actual movie playback and exit. Temporary files were verified beneath the preview's private cache.
3. **All 13 real-server browser workflows verified** using the final archive on the existing explicitly marked synthetic fixture. Twelve passed in the full run; the Live TV case initially failed because its separate loopback HTTP media source was not running (FFmpeg connection refused). Restoring only that media-directory fixture produced a passing targeted Live TV rerun, with no application change. These are not claimed as thirteen passing together in that attempt.

Those thirteen workflows cover: login/browse/details/direct playback/progress; compatible HLS, seeking, audio/subtitles and cleanup; persisted administrator encoding settings and library/user/log/task access; synthetic guide and single/series scheduling/edit/cancel; live channel playback/switch/own-session cleanup; skipper manual/automatic/disabled modes and administrator review; invitation/restricted signup/pairing and authorization; native music background queue/MP3/private playlists; video stops music; photos/rotation/navigation; PDF/EPUB reading/theme/position resume; collection create/add/remove/delete retaining original media.

4. Actual archive launcher checks passed: invalid port rejected; existing unmarked nonempty data directory rejected without altering its contents. Bundled libhostfxr/libcoreclr, compiled web, framework licenses and Noto Sans font license verified. Archive checksum passed before extraction.

The preceding backend API checkpoint had 195 passing tests and three Windows-specific skips, with earlier integration/user-manager coverage documented in the repository. Those backend suites were **not rerun** for this frontend/setup/packaging milestone; no backend C# source, API policy, database identifiers or migration code changed here. All claims above refer to actually executed package/browser/build tests or are explicitly labeled historical.

## Limits and feature status

Development preview, not stable/full Phase 1.4 completion. Mandatory built-in yt-dlp importing and independent YouTube/creator metadata are **not implemented** in this build. Existing providers remain available. Complete DVR recording lifecycle/files/deletion/quotas, physical tuners, long sessions, linked viewing profiles, recommendations/follows/notifications, resource tiers, creator/OBS/business/cloud/offline/plugin/native clients and full native metadata/admin parity remain unfinished or unverified. Hardware-acceleration settings remain available, but no GPU acceleration was tested.

Compiled legacy Web assets are **not bundled**. Upstream Web v12.4 has no downloadable release assets. Existing-client links open explicit help; INSTALL_TEST_BUILD.md documents attaching a compatible trusted compiled distribution. Native administration covers the implemented sections, but unfinished advanced inherited web controls require compatible legacy assets and cannot be claimed fully available without them. Backend APIs/plugin contracts are preserved.

See INSTALL_TEST_BUILD.md and TEST_BUILD_CHECKLIST.md for dependencies, extraction, startup, accounts/media, stopping/restarting, diagnostics and removal. Build artifacts include the source checkpoint's progress documents; this attached post-build report supersedes their pending verification/delivery notes. The GPL source and dependency notices remain, and the release commit provides corresponding source. No automatic main merge or production deployment.
