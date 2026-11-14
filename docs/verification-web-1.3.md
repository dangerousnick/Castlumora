# Phase 1.3 verification

Verified in a disposable Linux workspace. No Coon Media access, official Jellyfin repository changes, merge or deployment. Node 24.22.1, npm 11.9.0, .NET SDK 10.0.403, system Chromium and FFmpeg 7.1.5. Project base: main at b06078bb7c, including Phase 1/1.1.

## Results

| Check | Actual result |
| --- | --- |
| `npm run typecheck` | Passed, including browser tests and tooling |
| `npm run lint` | Passed |
| `npm test` | **16 passed** (schema, API adapter, component behavior) |
| `npm run build` | Passed; lazy player/HLS chunk ~602 kB minified / ~189 kB gzip produces Vite's size advisory |
| `npm run test:browser -- browser/fixture.spec.ts` | **2 passed**: isolated API/MP4 flow and mobile login overflow |
| `npm run test:browser -- browser/real-server.spec.ts` with disposable test environment | **1 passed** against actual loopback server and synthetic H264/AAC movie |
| API regression suite | **164 passed, 3 skipped** (existing Windows-specific tests), including 7 new store/policy cases |
| Full server integration suite | **130 passed, 3 skipped**, including the new real HTTP authorization/persistence test |
| Full Linux server/reference build | **Passed, 0 errors, 215 inherited-code warnings; 0 warnings in new presentation files** |
| Isolated API and server executable builds against prebuilt references | Passed with 0 warnings and 0 errors |
| License/contributor comparison and diff check | LICENSE and CONTRIBUTORS.md byte-identical to main; `git diff --check` passed |

The full integration count includes the new authorization test; do not add its earlier individual run to the total. No claim is made that all server unit projects were rerun in this phase. The API and integration suites cover the modified paths and existing media/authentication/controller behavior.

## What was exercised

Unit/component tests verify schema defaults and rejection of unsafe colors, duplicate IDs, invalid category/device references, unknown component/version fallback, server-base-aware requests, authorization headers/errors, category OR/dedup/exclusion rules, independent resume hiding, legacy-root links and an actual options-menu preference call.

Store tests verify draft/publish separation, disk reload, optimistic conflict rejection, watchlist/resume isolation across users, profile filtering and required administrator policy attributes. Real HTTP tests additionally bootstrap the actual in-memory server: anonymous read is denied, admin save/publish succeeds, stale writes return conflict, ordinary-user writes/publishing return forbidden, and user preference lists remain isolated.

Chromium fixture flow: connect, sign in, browse, details, watchlist, direct MP4 (video time advances), progress/pause/stop requests, administrator save/publish and mobile editor screenshot. These API responses are intercepted fixtures, not proof of backend/tuner compatibility. Application runtime contains no mock catalog.

Actual-server test: fresh data/config/cache/log paths under `/tmp/castlumora-web-real-server`, loopback-only port 18096, randomly generated temporary credentials stored outside the repository, one 24-second synthetic H264/AAC clip, no production data. The real browser signed in, browsed a permitted movie, opened details, advanced video time through the server, and received successful start/progress/stop response codes. No response mocking/interception is used in `real-server.spec.ts`. The server used normal FFmpeg path/version validation and successfully probed the generated media.

## Commands

From `Castlumora.Web`: `npm ci`, `npm run typecheck`, `npm run lint`, `npm test`, `npm run build`. Browser tests require a separately running `npm run dev` at loopback port 5173 and Chromium (`CHROMIUM_PATH` can override `/usr/bin/chromium`).

Server commands, from repository root (use your .NET 10 `dotnet` executable):

```sh
dotnet build Jellyfin.Server/Jellyfin.Server.csproj --configuration Debug --runtime linux-x64 --no-restore --nologo -m:1
dotnet test tests/Jellyfin.Api.Tests/Jellyfin.Api.Tests.csproj --configuration Debug --runtime linux-x64 --no-restore --nologo -m:1
dotnet test tests/Jellyfin.Server.Integration.Tests/Jellyfin.Server.Integration.Tests.csproj --configuration Debug --runtime linux-x64 --no-restore --nologo -m:1 -p:BuildProjectReferences=false --filter FullyQualifiedName~CastlumoraPresentationControllerTests
dotnet test tests/Jellyfin.Server.Integration.Tests/Jellyfin.Server.Integration.Tests.csproj --configuration Debug --runtime linux-x64 --no-build --no-restore --nologo
```

`BuildProjectReferences=false` was used only after referenced projects/executable were built. The full server/reference build was also run without that switch. A new checkout needs restore first; don't assume this workspace's NuGet cache exists elsewhere.

Generate isolated browser fixture media:

```sh
mkdir -p /tmp/castlumora-web-fixtures
ffmpeg -hide_banner -loglevel error -f lavfi -i 'color=c=0x46266f:s=640x360:r=24:d=24' -f lavfi -i 'sine=frequency=220:sample_rate=44100:duration=24' -c:v libx264 -pix_fmt yuv420p -c:a aac -movflags +faststart -y /tmp/castlumora-web-fixtures/test.mp4
cd Castlumora.Web
CHROMIUM_PATH=/usr/bin/chromium npm run test:browser -- browser/fixture.spec.ts
```

For the real-server test, create a **new disposable loopback-only** server/database/config/cache instance using the normal server CLI and compatible FFmpeg. Finish setup and add only generated test media using the existing setup/library APIs or compatible legacy Web client. Let scanning/probing finish. Supply `CASTLUMORA_TEST_SERVER`, `CASTLUMORA_TEST_USER`, `CASTLUMORA_TEST_PASSWORD` through your private local environment, then run `npm run test:browser -- browser/real-server.spec.ts`. It refuses non-loopback hosts and skips if its environment is absent. Do not use production credentials. The source test never stores a real token or password in Git.

## Corrections and environment limits

Initial repository style/analyzer issues and the HTTP test's required cancellation tokens were corrected before passing checks. Initial unit discovery accidentally included Playwright files; dedicated Vitest configuration now limits unit discovery to src tests. Initial sandboxed .NET build failed without compiler diagnostics; approved outer processes allowed build-worker communication.

The first actual-server startup was blocked by the inherited 2 GiB free-space precondition. Only generated multi-platform build runtime copies were removed, retaining the Linux executable and all source; the unchanged check then passed with ~2.5 GiB free. No storage threshold was weakened.

The first real playback attempt failed because a test-only FFmpeg validation-skip setting also bypassed encoder/probe path initialization. The disposable server was restarted with **normal validation enabled**, the synthetic item was re-probed successfully, and the actual playback test then passed. No production code workaround was introduced.

## Explicitly unverified / remaining

Real tuners/M3U/XMLTV/group filtering/DVR, hardware acceleration, Windows/macOS/mobile/TV native behavior, multiple audio/subtitle variants, HLS/transcode/remux seek/resume timelines, source/bitrate/chapters/trickplay controls, large libraries/category pagination, reverse-proxy/HTTPS deployment and long-running stream transitions are **not certified by these results**. Direct MP4 success does not certify every HLS/codec path. Existing backend capabilities remain intact; advanced interfaces use the legacy fallback. No production migration, backup restoration or deployment was performed.

## Screenshots

- [Mobile login](screenshots/web-login-mobile.png)
- [Home — explicitly labeled API fixture](screenshots/web-home-fixture.png)
- [Mobile editor — fixture layout](screenshots/web-editor-mobile-fixture.png)

Screenshots show UI layout, not real catalog/tuner/admin parity. Their fixtures exist only under `Castlumora.Web/browser/`. The GPL and runtime dependency notices are included with the static build.


## Native shelf and reader continuation

Frontend typecheck/lint/build pass, with 41 passing unit/API/component tests. Normal isolated API and Linux executable builds pass with zero warnings/errors. API regression suite: 195 pass, 3 Windows-specific skips. Added claims-owned reader-store/controller tests cover persisted state isolation, inaccessible/non-book denial and ignored foreign profile IDs. EPUB tests cover executable/remote content removal and metadata-resource/traversal/size rejection.

Actual disposable-server Chromium: all three shelf workflows pass (synthetic photos/navigation/rotation; authenticated PDF page/theme resume and EPUB chapters/CFI resume; create/add/remove/delete collection with original movie retained). The guarded fixture script verifies loopback, a local disposable marker and the server's actual data path before creating data. Unsupported/encrypted/large books, additional ebook/comic formats, annotation/full-text/offline, real-library scale and broad assistive/native-device support remain unverified or unimplemented. PDF/EPUB code is lazy loaded; the EPUB/player bundles retain Vite's size advisory. No production/Coon Media access or deployment.

Full browser regression after this milestone: **15 passed in one run** (2 editor/mobile API fixtures plus 13 actual disposable-server cases covering movie/HLS, administration, DVR schedules, live switching, skipper, signup/pairing, music/private playlists, video/audio coexistence and shelves/reader/collections).

Runtime dependency follow-up: pinned Zod 4.6.7 fixes GHSA-mrhx-c7xc-m3w5; optional device records retain their existing schema contract via partialRecord. Typecheck/lint/build and 41 frontend tests pass; production-dependency audit reports zero vulnerabilities. Rollup removes two nonfunctional dependency comments with misplaced PURE annotations; the build succeeds.

Post-validator browser verification: 14 cases pass in the full run; the PDF/EPUB case passes on targeted rerun after its cold lazy-module wait is bounded at 20 seconds instead of five. The initial stale development prebundle was cleared with `npm run dev -- --force`. All 15 workflows are covered across these runs; the shelf milestone passed all 15 together before the dependency update.
