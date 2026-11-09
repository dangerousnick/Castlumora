# Phase 1 verification

Environment: disposable Linux workspace; official .NET SDK 10.0.403 installed temporarily. No production data or Coon Media host was used.

## Commands and results

The initial unrestricted `dotnet build Jellyfin.sln --configuration Debug` exhausted the 8 GB filesystem while copying native dependencies for every platform into test output. Generated test binaries were removed, freeing 3 GB. The SDK rejects a runtime identifier at solution level (NETSDK1134), so verification uses individual project commands for `linux-x64`. See results below.

## Server build and CLI smoke check

`dotnet build Jellyfin.Server/Jellyfin.Server.csproj --configuration Debug --runtime linux-x64 --no-restore --nologo` passed with **0 warnings and 0 errors**.

Running the compiled assembly with `--help` reports `Castlumora Server 12.4.0.0`, preserves the Jellyfin Contributors/GPL notice and displays Castlumora CLI descriptions. This command exits without starting a service.

The initial server test run passed **42/42**, including all three new branding cases. The final project-by-project suite results follow below.

## Asset and compatibility checks

- Parsed SVGs as XML and checked the Swagger copy matches the wide wordmark source.
- Decoded all ten square PNG exports and verified dimensions (16 through 1024 pixels).
- Verified all seven ICO sizes (16, 24, 32, 48, 64, 128, 256).
- Compared all 105 locale dictionaries to the base: all keys and values unchanged except the product token.
- Confirmed LICENSE and CONTRIBUTORS are byte-identical to the base.
- Added tests for actual startup rendering in loading/error modes, product metadata, preserved assembly identity and copyright attribution.
- Visually inspected the rendered icon and wordmark PNGs.
- `git diff --check` passes.

## Limits

No production deployment, live data migration, playback hardware verification, Windows shell icon check or mobile/TV certification was performed. Main browser/PWA rebranding needs the separate Web repository described in `branding.md`. Vector assets simplify the supplied cinematic reference for UI use; fine texture and film-strip details are not reproduced.

## Repository workflow review

Existing PR workflows run CI and OpenAPI reporting. The OpenAPI publishing workflow triggers only on `master` or version tags, with upstream-owner guards on publishing jobs. No workflow target for Coon Media was found. This change neither modifies workflows nor deploys a server.

## Final test results

All 17 projects were run with `dotnet test <project.csproj> --configuration Debug --runtime linux-x64 --nologo`. **4193 passed, 1 failed due to workspace disk capacity, 19 skipped**. Sixteen projects completed without failures.

| Project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Jellyfin.Api.Tests | 157 | 0 | 3 |
| Jellyfin.Common.Tests | 30 | 0 | 0 |
| Jellyfin.Controller.Tests | 219 | 0 | 0 |
| Jellyfin.Drawing.Skia.Tests | 28 | 0 | 0 |
| Jellyfin.Extensions.Tests | 164 | 0 | 0 |
| Jellyfin.LiveTv.Tests | 78 | 0 | 0 |
| Jellyfin.MediaEncoding.Hls.Tests | 18 | 0 | 0 |
| Jellyfin.MediaEncoding.Keyframes.Tests | 2 | 0 | 0 |
| Jellyfin.MediaEncoding.Tests | 103 | 0 | 1 |
| Jellyfin.Model.Tests | 748 | 0 | 0 |
| Jellyfin.Naming.Tests | 701 | 0 | 0 |
| Jellyfin.Networking.Tests | 147 | 0 | 0 |
| Jellyfin.Providers.Tests | 515 | 0 | 0 |
| Jellyfin.Server.Implementations.Tests | 1073 | 1 | 12 |
| Jellyfin.Server.Integration.Tests | 129 | 0 | 3 |
| Jellyfin.Server.Tests | 42 | 0 | 0 |
| Jellyfin.XbmcMetadata.Tests | 39 | 0 | 0 |

The only failure was `BackupServiceTests.CreateBackupAsync_WithCorruptKeyframeDataRow_SkipsRowAndCompletesBackup`: the existing 5 GiB free-space precondition rejected this workspace (about 2.8 GiB free). The test did not exercise the backup body. That precondition is unchanged; rerun on a machine with at least 5 GiB free after dependencies/build output.

Skipped tests are three Windows-specific API file-handle tests, one macOS sysctl test, twelve Windows filesystem cases and three controller tests already disabled upstream for flaky execution. Windows/macOS behavior therefore remains unverified locally.

One server-project rerun stalled without a result and was stopped. A fresh diagnostic run with `--blame-hang-timeout 90s --blame-hang-dump-type none --logger 'console;verbosity=normal'` completed successfully: 42/42, including all branding tests. The stopped attempt is not counted as a pass.
