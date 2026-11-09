# Phase 1.3 verification (in progress)

Disposable Linux workspace only. No Coon Media access or deployment. Node 24.22.1, npm 11.9.0, .NET SDK 10.0.403, system Chromium and FFmpeg.

## Executed so far

| Check | Result |
| --- | --- |
| `npm run typecheck` | Passed |
| `npm run lint` | Passed |
| `npm test` | 16 passed (schema, API adapter, component behavior) |
| `npm run build` | Passed; lazy player/HLS chunk is ~602 kB minified / ~189 kB gzip and produces Vite's size advisory |
| `npx playwright test browser/fixture.spec.ts` | 2 passed: isolated API/MP4 fixture flow and mobile login overflow check |
| Real HTTP presentation authorization/persistence | 1 passed against in-memory actual server |
| API tests: `dotnet test tests/Jellyfin.Api.Tests/Jellyfin.Api.Tests.csproj --configuration Debug --runtime linux-x64 --no-restore --nologo -m:1` | 164 passed, 3 skipped (existing Windows-specific tests) |

Browser fixture flow: connect, authenticate, browse, details, watchlist, direct MP4 playback (video time advanced), progress/pause/stop requests, administrator draft save/publish, mobile screenshot. Those responses are **intercepted fixtures**, not evidence of real backend or tuner compatibility. No mock catalog is present in application runtime.

Initial checks caught repository analyzer/style issues; corrected before the passing API run. Initial frontend test discovery included Playwright files; a dedicated Vitest include config now separates browser and unit tests. Initial sandboxed .NET build failed without compiler diagnostics; builds use the execution environment's approved outer process so workers can communicate.

## Pending

Server executable build against prebuilt references passed with 0 warnings/errors; final full server/reference build and actual disposable-server browser playback verification are still in progress. HTTP authorization/persistence integration passed. Results will be updated before final delivery. Real tuner/EPG/recording/hardware/browser codec matrices and production proxy deployment are not verified. Do not interpret implementation or fixture tests as those claims.

## Reproduce browser fixtures

Run the frontend dev server in one terminal. Generate a synthetic test clip (requires FFmpeg) and run tests in another:

```sh
mkdir -p /tmp/castlumora-web-fixtures
ffmpeg -hide_banner -loglevel error -f lavfi -i 'color=c=0x46266f:s=640x360:r=24:d=24' -f lavfi -i 'sine=frequency=220:sample_rate=44100:duration=24' -c:v libx264 -pix_fmt yuv420p -c:a aac -movflags +faststart -y /tmp/castlumora-web-fixtures/test.mp4
cd Castlumora.Web
CHROMIUM_PATH=/usr/bin/chromium npm run test:browser -- browser/fixture.spec.ts
```

The real-server browser test is opt-in with `CASTLUMORA_TEST_SERVER`, `CASTLUMORA_TEST_USER`, `CASTLUMORA_TEST_PASSWORD`; it enforces loopback hosting. Use a newly created disposable server/database/library with only synthetic media. Never use production credentials. The test signs in, browses a movie, plays it, and checks actual server progress/stop response codes. It skips if its environment is absent.

## Screenshots

- [Mobile login](screenshots/web-login-mobile.png)
- [Home, explicitly labeled API fixture](screenshots/web-home-fixture.png)
- [Mobile editor, fixture layout](screenshots/web-editor-mobile-fixture.png)

Screenshots show UI layout, not proof of real server media or administration. Their fixtures are defined only under `Castlumora.Web/browser/`.
