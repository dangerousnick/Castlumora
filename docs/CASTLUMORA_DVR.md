# Native DVR lifecycle

Castlumora uses the inherited tuner, XMLTV, timer, recording and FFmpeg services. It does not replace their storage model, authentication, scheduler or process ownership. Native Live TV shows Guide, Scheduled, Series and Recordings. Schedule/recording views refresh every ten seconds while visible; Refresh guide & recordings also refreshes manually. Timer status remains server-authoritative. An InProgress single timer displays **Stop recording**, with confirmation that recorded media is retained (empty output is removed); future timers display Cancel recording. Series cancellation retains its separate series/scheduled-recordings wording. The stop status message says the stop was requested rather than claiming asynchronous cleanup has already finished.

## Two distinct recording actions

- **Remove from library** calls the inherited `DELETE LiveTv/Recordings/{id}`. The file is retained and can return after a scan. Recording-management permission is required; the confirmation explicitly describes retained files.
- **Delete recording file** calls the inherited `DELETE Items/{id}`. It physically deletes through the library manager only when the backend's per-item `CanDelete` authorization allows it. The recordings query requests `Fields=CanDelete`; the button appears only when that returned value is true and the viewer can manage recordings. There is a separate explicit permanent-deletion confirmation. No new bypass or broadened server policy is introduced.

Viewers do not get recording-management buttons. Client controls are convenience only: the backend independently checks management/item visibility/deletion permissions. Deleting a recording must never delete its original live-source media.

## Synthetic lifecycle verification

The guarded helpers `Castlumora.Web/browser/setup-dvr-fixture.py` and `cleanup-dvr-fixture.py` require an explicitly marked loopback fixture and verify the server's actual ProgramDataPath. They accept only the fixture's own local M3U and XMLTV sources, snapshot original guide/options/policy, use a dedicated recording folder, temporarily enable deletion only for the synthetic administrator, and retain recovery state if interrupted. They never target Coon Media or a production server.

The XMLTV provider caches source files for one hour. The helper re-saves the same owned provider through the normal validation API to invalidate that cache before guide refresh; it does not change the server-wide caching policy. New timers copy programme dates from the guide; do not assume arbitrary posted dates can shorten a guide-backed recording. The fixture adds short unique programmes and must be run immediately after creation.

Run the existing disposable synthetic media source (serving only its media directory), backend and frontend, set the documented test server/user/password variables privately, then:

```bash
python3 browser/setup-dvr-fixture.py --fixture-root /tmp/castlumora-web-real-server
CASTLUMORA_TEST_DVR=1 npm run test:browser -- browser/dvr-server.spec.ts
python3 browser/cleanup-dvr-fixture.py --fixture-root /tmp/castlumora-web-real-server
```

Cleanup restores only the saved owned fixture guide/options/policy and removes its synthetic viewer; recordings are retained for inspection unless the test itself successfully deletes them. An interrupted fixture is identified by dvr-fixture-state.json; restore it before generating another. Never copy that private fixture state into release assets.

Executed on 2026-11-09: the complete browser case passed (1.9 minutes), Release library build passed with zero warnings/errors, and all 81 LiveTV tests passed on Linux. Frontend checks are recorded in the checkpoint. The fixture explicitly requests a library scan after registering its recording root, since initial indexing is asynchronous.

For a separate active-stop fixture, add `--active-stop` to setup and run `CASTLUMORA_TEST_DVR=1 npm run test:browser -- browser/dvr-stop-server.spec.ts`, then the same cleanup. This uses real-time input and a bounded programme; it checks denied viewer cancellation, administrator stop before scheduled end, a nonempty retained partial file, and actual partial playback.

The test covers real scheduling/cancellation, completed recording and media-file appearance, playback, denied viewer mutations, retained media after library-only removal, rescan and permission-checked permanent deletion, while verifying the original source remains. Executed results are recorded in DEVELOPMENT_PROGRESS.md after the workflow completes; the script's existence alone is not a passing result.

## Remaining work

This milestone does not implement per-user DVR quotas/storage tiers, certify physical tuners/GPU paths/long-duration recordings, or finish all channel-group/timeshift/native-device requirements. These remain mandatory tracked product work. The inherited media engine, recording-folder configuration and legacy interfaces remain available.

Active-stop workflow executed successfully on 2026-11-09 (1.2 minutes); frontend typecheck/lint/build and all 48 tests passed. Real recording stopped before guide end, partial output played, and ordinary-viewer stop was denied. FFmpeg may buffer before flushing output, so the fixture waits up to 45 seconds for actual nonempty media. No real tuner/GPU claim.
