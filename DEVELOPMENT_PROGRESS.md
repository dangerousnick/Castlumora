# Castlumora Web Phase 1.3 development progress

Branch: `development/castlumora-web-1.3`; PR targets `main`. Base main: `b06078bb7c6b280ea799ebf0737e6b9c3485a828` (contains Phase 1 branding and Phase 1.1 version/project documentation).

Permanent source of truth: [docs/CASTLUMORA_PRODUCT_REQUIREMENTS.md](docs/CASTLUMORA_PRODUCT_REQUIREMENTS.md). Remaining product vision: [PRODUCT_ROADMAP.md](PRODUCT_ROADMAP.md). Actual coverage: [FEATURE_PARITY.md](FEATURE_PARITY.md).










## Verified transient retries — recovery checkpoint (2026-11-13)

- **Last completed milestone:** opt-in persistent transient retry/backoff, including
  restart recovery, nonblocking ready jobs, durable cancellation and three attempts
  total. Safe persistent failure guidance is also complete.
- **Latest published application commit:** `57129188fac4288002e4240cd311bf89059085e9`.
- **Current branch:** `development/castlumora-web-1.3`; PR3 targets main and stays open.
- **Local verification:** full mandatory gate `/tmp/castlumora-prepush.mNTGxU3d`
  passed on that exact commit. Restored formatting checked 2,432 files with zero
  changes; .NET10 Debug/Release builds had zero warnings/errors; all17 backend
  suites passed 4,490 tests with19 existing skips. Node24 typecheck/lint/62tests/
  production build, hook/native fixture/bundler/report-handler checks passed.
  Real backup safeguards and operations remain tested; no production reserve changed.
- **GitHub verification:** Tests43529697998 passed required Linux132244038866 and
  macOS132244038736, plus advisory Windows132244038879. Format43529697780,
  ABI43529697766, OpenAPI43529697792 and Preview43529697761 passed.
- **Real workflow evidence:** owned HTTPS503 → durable backoff → queue restart →
  exact native-inspected media/library publication on attempt2. Two HTTP integration
  tests passed. Actual compiled-app Chromium verified opt-in, network backoff/time,
  duplicate preserving policy, safe diagnostics and cancellation; real server restart
  retained Cancelled/attempt1/no deadline. The disposable servers are stopped.
- **Download:** https://github.com/dangerousnick/Castlumora/actions/runs/43529697761/artifacts/14087106637
  Archive `Castlumora-1.0-dev-57129188fac4-linux-x64.tar.gz`.
  SHA256 `59cfa0f269649a6b0cd403edb2bd4221ddd0e561388688e1ebe654b0f43498d7`.
  GitHub sign-in required; expires2026-12-13. Three actual extracted-package browser
  workflows passed, including retry opt-in persistence and public owned import/hash/
  dedup/index/advancing playback. Independent download verified source, runtime,
  compiled UI/retry flag/guides, all16branding files and original GPL bytes.
- **Unfinished work:** no unfinished implementation in this checkpoint. Full YouTube/
  playlist/channel/bulk/scheduled imports, source Retry-After support, configurable
  resource policy, online creator metadata and the retained full1.0 scope remain.
  Direct MP4 preview limits are unchanged. Proxy-only cloud egress remains unsupported;
  no TLS/address-policy bypass. Mint/macOS desktop installation, GPU and real-tuner
  certification remain distinct from CI. Historical native cleanup/PID1 zombie
  observations are retained for process-lifetime review; this exact three-OS CI passed.
- **Usage status:** Not accessible; no exposed allowance/reset tool exists.

### Next exact development task

Implement bounded authorized multiple-direct-URL submission using the existing queue:

1. Accept at most25 explicit HTTPS MP4 entries with titles, the real destination
   library and an administrator-confirmed rights record; retain all existing policy.
2. Validate every entry before a single atomic queue-journal update; invalid batches
   must not leave partial submissions. Coalesce duplicates without changing existing
   job IDs, rights, retry policy or attempt budgets. Keep the1000-job/8MiB bounds.
3. Add native administrator batch entry/management without exposing paths, executable
   arguments or unsupported YouTube/playlist/channel functionality.
4. Test batch atomicity, duplicates, caps, authorization, unsafe URLs, persistence,
   cancellation and real owned native download/publication/indexing. Reuse the pinned
   executable and existing publication path; no parallel architecture or global overrides.
5. Run the full hook, publish to this branch, verify Linux/macOS/shared CI and a new
   preview, update these checkpoints/PR3. Then advance controlled resources and secure
   source/playlist/channel enumeration plus independent creator metadata. Full1.0
   requirements remain mandatory; the owner's future migration plan is preserved.

### Recovery instructions

`git fetch origin`; inspect `git status` and `git log -8` before changing files.
Read the permanent specifications and the newest checkpoint first. Preserve any
newer remote/local work; use the existing branch and install its Git hooks. The
published application baseline above is tested; documentation-only follow-up may
be newer. Verify exact current CI rather than rerunning old development or restoring
the retained publication stash. Never use --no-verify or merge main automatically.
Native fixtures: CASTLUMORA_TEST_YTDLP_BUNDLE, CASTLUMORA_TEST_FFPROBE and
CASTLUMORA_TEST_FFMPEG must identify reviewed local fixtures. See AGENTS.md and
scripts/validate-before-push.sh for complete commands. No Coon Media deployment.

Earlier pending sections below are retained history, superseded by this checkpoint.

## Transient-retry continuation — implementation under verification

Last completed milestone: safe persistent importer failure codes/admin guidance.
Latest published commit: `e271d70985a0fdba0b6270af12cb24c8a0439ef8`.
Branch: `development/castlumora-web-1.3`; PR3 open, main unmerged.

Exact baseline gate: /tmp/castlumora-prepush.oNwbGNiH; format2430files/0changes,
.NET10 Debug/Release0warnings/errors;17backend suites4464passed/19existing skips;
Node24 typecheck/lint/61tests/build plus hook/native/bundler/report tests.
GitHub Tests43524371080 passed required Linux132226345160/macOS132226345203
and advisory Windows132226344666; Format43524371199, ABI43524371110 and
OpenAPI43524371165 passed. The older Windows cleanup failures remain historical
process/handle-lifetime follow-up, not failures of this exact snapshot.

Verified preview: https://github.com/dangerousnick/Castlumora/actions/runs/43524371105/artifacts/14084662522
Archive Castlumora-1.0-dev-e271d70985a0-linux-x64.tar.gz;
SHA256 a23a977dd2b2954721b7023f472f93edbbb72861d395719570c2022de4fec56d.
All three extracted-package browser workflows passed, including public owned GPL
import/hash/dedup/index/advancing playback. Independent download verified source,
server/runtime, compiled Web/failure guidance, guides,16branding files and GPL.
Expires2026-12-13; GitHub sign-in required. No Mint/macOS hardware certification.

Current task: opt-in transient retry/backoff with durable deadlines, no worker
blocking, cancellation/reload/duplicate safety and a shared three-attempt budget.
Executed verification: 55 initial queue/policy cases, then 26 policy/native cases,
including actual owned HTTPS503 → persisted backoff/restart → native inspected
publication with exact bytes on attempt two. Two HTTP integration tests passed,
including opt-in/restart persistence, indexing/download, viewer/anonymous denial
and malformed-boolean rejection. Node24 typecheck/lint/62tests/build passed.
Restored whole-solution formatting checked 2,432 files, zero changes. A fixture
member-order analyzer error was corrected locally before the successful native run.

Actual compiled app/Chromium passed opt-in/defaults, durable network retry time,
duplicate preserving policy/deadline/attempts, safe guidance and cancellation before
retry. A real process restart retained Cancelled/attempt1/automatic flag/no deadline.
Both test-server processes stopped cleanly; no Coon access. Evidence:
/tmp/castlumora-retry-native-focused2.log, /tmp/castlumora-retry-integration.log,
/tmp/castlumora-retry-browser.log and /tmp/castlumora-retry-browser-result.json.

This increment is not yet published. Next: commit/push through the entire normal
gate, inspect exact required Linux/macOS/shared CI and updated three-workflow
package checks, independently verify the new downloadable artifact and updatePR3.
Then complete controlled resource/source expansion without reducing full1.0 scope.

Usage status: Not accessible. Recovery: inspect Git status/log and this checkpoint,
preserve uncommitted retry files, run the recorded tests/gate; do not restore the
retained earlier publication stash. Safe cleanup removed51old ignored coverage
folders (~438MiB), retaining latest2/project/logs/history/all preview packages;
free space2.5GiB. Two exited downloader zombies owned by containerPID1 were observed,
not active downloads; retain process-lifetime follow-up without weakening tests.

## Resume checkpoint — 2026-11-13, exact-HEAD validation pending

Current branch: `development/castlumora-web-1.3`; PR3 remains open, no merge into main.
Last completed implementation: actionable, redacted persistent import failures,
commit `b6a891a9ff910dc5fff780d212827c9c2ee8d2d6`. Full mandatory local gate passed
for that exact commit: restored solution formatting2430files/0changes, .NET10 Debug
and Release0warnings/errors, all17backend suites4464passed/19existing skips,
Node24 typecheck/lint/61frontend tests/build, native dependency/workflow/hook tests.
Evidence: `/tmp/castlumora-prepush.4GbeooA5`.

The subsequent push was rejected non-fast-forward, not for a failed check. Fetched
and preserved the owner's roadmap-only commit
`c91d90d084a4898c88fdd7c6ce1d9fcf2996146f` through a normal merge; no force push,
rebase, lost commits or migration implementation. The prior application preview
and required-CI baseline remains `dc3147c523357be4819d95a25fea3ade1c9417a3`;
the remote also contains the owner's roadmap commit. The merged exact snapshot
must pass the entire normal pre-push gate again before publication.

Next exact steps: push normally with .NET10/Node24/reviewed native fixtures, inspect
required Linux/macOS/shared checks and the three-workflow packaged preview for the
published merge SHA, verify the downloadable package/hash/branding/GPL/failure guide,
and update PR3. Then complete controlled retry/backoff before larger source expansion.
No uncommitted implementation remains at this checkpoint. Usage status: Not accessible.
Recovery: `git fetch origin`, inspect `git status` and `git log --all -8`, preserve
local work, read this file and the permanent specs, install repository hooks and
resume exact-HEAD validation; never reapply the retained publication stash.

## Actionable failure-reporting checkpoint — verified locally, awaiting publication

Baseline published dc3147c523357be4819d95a25fea3ade1c9417a3 remains verified with
required Linux/macOS/shared CI and real public-import/index/dedup/browser playback.
Current increment implements fixed persistent failure codes, safe API history
clamping, actionable administrator guidance and a non-reflecting internal tunnel
connection-failure flag. No DNS/TLS/auth/process/resource or attempt limits changed.

Executed: initial focused69backend cases; final policy/persistence50cases; final
tunnel/classification35cases; real TLS certificate/denial assertions retained.
Node24 typecheck/lint/61frontend tests/production build pass. Full restored format
2430files0changes before final tunnel addition; final Release build0warnings/errors.
A locally detected separator blank line was fixed before any push.

Actual compiled server: saved owned job ab40f751e0c8423d876f29327c5abb80 used its
last allowed retry, persisted sourceNetwork (attempt3) under the independently
verified cloud direct-egress refusal. Real Chromium then showed fixed network
help and jobID, no source URL/private path/proxy output, and no Retry button after
exhaustion. Logs: /tmp/castlumora-live-error-code-result2.log and
/tmp/castlumora-import-error-browser.log. Intermediate downloadFailed exposed only
safe generic output; authoritative connection flag corrected diagnosis without
returning raw errors or modifying policy. No production source/secret was used.
The marked loopback server is stopped before final full-gate validation.

Next exact steps: normal commit/push through full gate; inspect exact required
Linux/macOS/shared CI and three-workflow preview (including real public import);
verify new artifact/source/checksum/branding/GPL and update PR3. Then implement
controlled retry/backoff/resource/source enumeration milestones, preserving full
1.0 scope. Windows private staging cleanup remains advisory and retained for
future native handle/process-lifetime review. Usage status: Not accessible.


## Verified public import checkpoint and active failure-reporting milestone

Latest published/required-CI verified commit:
`dc3147c523357be4819d95a25fea3ade1c9417a3`.
Full local gate /tmp/castlumora-prepush.gjOEErLz passed; required Linux130968123933
and macOS130968124197, Format/OpenAPI/ABI and Preview43153546755 passed.
The extracted package actually imported the owner-authorized public GPL clip with
default production networking, exact11734bytes/SHA,deduplication, automatic native
indexing/H264-AAC metadata and advancing browser playback (8.6seconds). Three
packaged browser workflows passed. This resolves the previously pending positive
GitHub execution; cloud-local direct egress remains separately blocked/refused.

Verified download:
https://github.com/dangerousnick/Castlumora/actions/runs/43153546755/artifacts/13914787865
Archive Castlumora-1.0-dev-dc3147c52335-linux-x64.tar.gz.
SHA256:56bcf751ed9084105490e5e6d92330b4188ba68f2f306e93e36bc7bb75a66a71.
Expires2026-12-12; sign-in required. Independent download verified envelope digest
035ebb17fd3edb1eefcb56680b64f8eeb2794c42e4fc5ca09bcdcb4a42fdc638,
manifest/source, server/runtime, compiled Web/importer, GPL and all16branding files.
Report /tmp/castlumora-public-import-preview-verified.json. PR3 updated.

Windows advisory130968124111 reported four staging-cleanup assertion failures
following cancellation/oversized metadata. Reviewed logs: directory residue, not a
Linux/macOS failure; Windows hosted importing remains disabled/deferred. Preserve
failures/diagnostics and investigate native handle/process shutdown before claiming
Windows support. No tests or shared security checks were weakened.

Current coherent increment: fixed persistent failure codes and actionable native
admin guidance without raw exceptions/URLs/paths/stdout/stderr. Existing exception
types and limits remain. Completed focused run69cases plus final policy50cases;
Node24 typecheck/lint/61tests/build pass; full restored format2430files0changes
before the final tunnel flag addition. A local live retry correctly stored safe
but generic downloadFailed; improve diagnosis with the tunnel's fixed numeric-
connection-failure flag, never by reflecting remote errors or changing policy.
Tests/build for that final change are running; do not claim its live proof yet.
One locally caught missing separation blank line was corrected before publication.

Next exact steps: finish tunnel/classification tests and Release build; run the
saved failed owned job's remaining manual retry to verify sourceNetwork through
normal API and browser, then stop only the marked fixture server. Run complete
normal pre-push gate/publish; inspect required Linux/macOS and new preview; verify
artifact/update PR3. Continue safe retry/resource policies, broader authorized
source enumeration/metadata and the retained full Phase1.4/1.0 requirements.
Usage status: Not accessible. Preserve source/history/stash; no merge/deployment.


## Active packaged-import verification increment — 2026-11-12

Latest published/required-CI verified commit:
`3726c3314c6b53960a4d44519296181547169164`. Linux/macOS/Windows tests and
Format/ABI/OpenAPI/preview all passed (Tests43147576520, Preview43147576526).
Full local gate again4438backend passes/19platform skips/60frontend tests.

Ordinary compiled server/Chromium importer controls passed after safe recovery of
real disk space to2.6GiB (old coverage, latest2/project retained; unused Debug output).
No source/history/archives/native dependencies removed; no production threshold
changed. Corrected optional fixture for integrated-server autodetection, registered
library setup, accessible combobox selection and bounded native readiness waits.
Local positive public import failed: compiled production policy validated public
DNS then pinnedTCP443 was refused; cloud-proxied HTTP succeeds. No SSRF/TLS bypass.
The owned failed job ab40f751e0c8423d876f29327c5abb80 remains in the disposable state.

Next coherent milestone now in working tree: three-workflow extracted-package
browser coverage including an actual owner-authorized public GPL clip import,
persistence/hash/dedup/index/native codec metadata/browser playback. Typecheck,
lint and Playwright discovery pass; positive GitHub execution is pending and must
not be described as passed. See docs/verification-single-video-import-1.4.md.
Stop/restart commands and normal fixture are preserved; owned server was stopped
cleanly before full validation. Its state: /tmp/castlumora-web-real-server; credentials
remain private in its credentials.json (do not print them).

Next steps: run the complete normal hook/publish this test/doc increment; inspect
required Linux/macOS/shared CI and the actual public-import preview run; diagnose
failures before further pushes. Verify the new artifact's exact source/checksum/
branding/GPL, update PR3, then continue broader retained importer/metadata roadmap.
Usage status: Not accessible. Do not restart development or merge/deploy to main.


## Required macOS fixture correction — 2026-11-12

Published source d6d5299f040f05e717dc133b13964432b21e3a07 passed native Linux
(job130936398868), Windows advisory, Format/OpenAPI/ABI and verified preview.
**macOS failed** exactly one API test: publication fixture compared its raw
/var/folders path with production's correctly normalized /private/var/folders path
(ImportPublicationTests line90). Native Mac full-server import/index/media/restart
and other assertions completed; do not represent the overall required job as passed.
Failure: Tests43143678923, job130936398795,366API passes/3platform skips/1failure.

Normalize the fixture's verified Apple system-root alias at setup using the existing
strict mapping. Keep exact path equality, actual file bytes, provenance, metadata,
scan and every symlink/security assertion; no test skips or CI weakening. The
portable mapping tests already exercise approved/rejected aliases; native Mac CI
must verify the corrected fixture. No production behavior changed for this repair.
Rerun full normal pre-push gate and inspect exact Linux/macOS CI before starting
packaged-import expansion. The published Linux preview remains verified at its
exact source, but Phase1.4 milestone acceptance remains pending required Mac success.
Usage status: Not accessible. Preserve all commits/stash/source; no main merge.


## Verified downloadable integrated preview

Source: `d6d5299f040f05e717dc133b13964432b21e3a07`.
Download: https://github.com/dangerousnick/Castlumora/actions/runs/43143678890/artifacts/13910807536
Archive: `Castlumora-1.0-dev-d6d5299f040f-linux-x64.tar.gz`.
SHA-256: `428b968127cd5887ea3fe4d74ee3dd93b4ef31a9126fc3ac3253091ea669bf97`.
GitHub sign-in required; expires2026-12-12. Preview43143678890 passed real extracted-
package setup/auth/restart/library/movie-playback/runtime/queue-form browser checks.
Independent connector download verified outer digest63de6250c8e1c6cbf744c128bbfa908b52c86135e9db7588280042333cabe772,
inner checksum, exact source/manifest, native executable/runtime, compiled importer
UI, GPL bytes, import/installation/checklist docs and all16branding files. No extra
extracted copy was left on the constrained disk. Verification report:
`/tmp/castlumora-single-video-preview-verified.json`.

Format43143678898, OpenAPI43143678932 and ABI43143678877 passed. Required native
Linux/macOS tests43143678923 are still running at this checkpoint, not yet a pass.
PR3 title/body now describe the actual integrated workflow, verified download,
local gate, pending required checks and bounded/unfinished functionality.
Next: inspect required CI; only then extend normal packaged import/playback using
the pinned owned GPL HTTPS fixture identified below. Usage remains Not accessible.


## Published single-video milestone — exact checkpoint

Latest published commit: `d6d5299f040f05e717dc133b13964432b21e3a07`.
Feature commit: `fbea85f83c752a4ace288113b0b9c27bdfbdeaf8`.
Branch: `development/castlumora-web-1.3`; PR3 open; no main merge/deployment.
The complete normal pre-push gate passed on this exact corrected source:
2,428formatted files/zero changes/no workspace warnings; zero-warning/error
.NET10 Debug server/Release solution;17backend projects4,438passed/19existing
platform skips; Node24 typecheck/lint/build +60tests; hook/native-fixture/bundler/
report-handler checks. Real backup tests remain enabled and pass under the isolated
fixture policy; production storage safeguards are unchanged.
Logs: /tmp/castlumora-prepush.iw95p8MY and
/tmp/castlumora-single-video-fixed-push.log. Previous rejected attempt remains
recorded below; no known failing source was published.

Exact-source GitHub runs started: Tests43143678923; Format43143678898;
ABI43143678877; OpenAPI43143678932; Preview43143678890. Native Linux/macOS and
actual-package outcomes must be inspected before marking this milestone fully
verified or starting another feature push. PR3 comment records these results.

Next exact task after required CI passes: extend the extracted-package browser
workflow to create a Home Videos library, submit the repository's owned GPL
synthetic direct HTTPS clip through the normal elevated importer, observe durable
completion/indexing, and play it through Castlumora Web. No test-only production
network bypass. Existing public fixture (HTTP200,11,734bytes verified):
https://raw.githubusercontent.com/dangerousnick/Castlumora/36cf2b44a01c0f88c6f5c9ec979161790f03d8a1/tests/Jellyfin.Api.Tests/Fixtures/import-owned.mp4
Then verify the new artifact/checksum/source/branding/licenses and update PR3.
Retain all broader importer/metadata and Phase1.4/full1.0 requirements.
Usage status: Not accessible. Resume by fetching origin, inspecting this section,
current Git status and exact workflow outcomes; preserve existing commits/work.


## Required gate correction — 2026-11-12

Local feature commit: `fbea85f83c752a4ace288113b0b9c27bdfbdeaf8`.
Its first normal push was **blocked before publication** by restored full-workspace
formatting: SA1513/SA1516 missing method separation, SA1508 extra closing-brace
whitespace and SA1623 boolean-property documentation in the new controller/service.
The earlier no-restore formatter used a different analyzer set and missed these;
it is not evidence that the exact committed gate passed. Corrected the four issues
without weakening checks or changing behavior. Rerun the complete normal hook,
including builds/all tests, on the new exact commit. Original failure logs:
`/tmp/castlumora-prepush.ftHZFZ9p/format.log`. Published branch still36cf until the
new hook succeeds. Do not bypass it or repeat a known failing source push.


## Current single-video validation checkpoint — 2026-11-12

Branch: `development/castlumora-web-1.3`; PR3 remains open against main.
Latest verified published commit before this milestone:
`36cf2b44a01c0f88c6f5c9ec979161790f03d8a1` (Linux/macOS/shared checks passed).
All earlier checkpoints and the applied-once publication stash are preserved.

Completed source: durable authorized single-video host/API/native administrator
workflow, real container/decode inspection, secure idempotent same-filesystem
publication, safe NFO/private provenance, source/content deduplication, cancellation,
retry and startup recovery. Fixed automatic indexing for a destination without an
indexed ancestor by queuing, not cancelling, the inherited library scan. Recovered
jobs wait for core startup; native tool paths are resolved at execution time.

Verification already executed:74focused queue/publication/native/runtime tests
passed before the final startup-path regression addition; real HTTP import →
automatic index → exact media bytes → host restart/deduplication and administrator/
viewer authorization passed again after the core-readiness fix (2cases,27seconds).
Node24 typecheck/lint/build and all60frontend tests passed; full-solution format
applied zero changes across2428files before final readiness changes. Final exact-
commit pre-push gate and CI results must be checked before claiming publication.
Logs: /tmp/castlumora-import-final-focused2.log,
/tmp/castlumora-import-startup-http.log, /tmp/castlumora-import-format-fix.log.

Local browser attempt: actual executable startup was rejected by the unchanged
production2GiB data/cache safeguard (about753MiB available). This is an explicit
storage infrastructure limitation, not a passing browser run or an importer defect.
Only regenerable test Release bin outputs were removed; available disk rose to
about842MiB. Source/history/stash/archive previews and required dependencies remain.
The existing GitHub preview workflow tests the extracted real package on a larger
runner, now including queue/form capability. Verify its exact artifact before
claiming packaged/browser success. No GPU, real tuner or macOS installation claim.

Usage status: **Not accessible**. No remaining allowance/reset percentage claimed.
Next: finish final format/build/backend/Node24 gate through a normal push; inspect
exact Linux/macOS/shared CI, update PR3, verify a new downloadable Linux preview.
Then extend secure source-aware YouTube/playlist/channel metadata/enumeration,
bulk/resources/retry/admin controls and remaining Phase1.4/full1.0 scope. Initial
scope remains direct H264/AAC MP4≤32MiB,one worker,three manual attempts; no full
production importer or stable1.0 claim. Do not restart or reapply the old stash.
Recovery: fetch origin; inspect git status/log and this section; continue existing
implementation/tests. No automatic main merge, deployment or Coon Media access.


## Active required-CI repair — pinned native dependency fixture

- **Latest published code:**5fd129d4d34cdac5f4c4a7ec5e0093548e984186.
  Existing branch development/castlumora-web-1.3, PR3. Every prior commit preserved.
- **Completed inspection local gate:** full formatting2,422files/zero changes/no
  workspace warning; zero-warning .NET10 Debug server/Release solution builds;
  all17backend suites4,416passed/19existing platform skips;58frontend tests with
  typecheck/lint/build;10hook/6bundler/5handler cases. Logs:
  /tmp/castlumora-prepush.6Oy05Siq and /tmp/castlumora-media-inspection-push.log.
- **Exact-source CI blocker:** Tests43057723303 macOS job130651575717 passed.
  Linux job130651575298 stalled in native dependency setup for over16minutes:
  apt repeatedly could not resolve Ubuntu archive/security/azure mirror names.
  It never reached application builds/tests. Cancelled this stalled run to obtain
  logs; Linux is NOT verified for5fd129d4d3. Windows advisory lacked native tools;
  its setup failed. Do not retry identical apt failures or call this all-green.
- **Repair:** reviewed dated/hash-pinned CI-only Linux/Windows native fixtures;
  fixed-name, link/duplicate-safe bounded extraction, native version checks,
  preserved licenses/source links and setup deadline. Prepare after builds so
  advisory Windows fixture failure cannot erase compatibility compilation.
  No production dependency/update settings or required tests weakened.
- **Verification so far:** six fixture security cases and actionlint passed.
  Linux archive hash matches API and publisher checksum, native binaries run;
  full Debug API346passed/3existing skips using these exact binaries. Windows
  pins/checksums agree but native execution awaits CI. Logs:
  /tmp/castlumora-reviewed-native-media.log and
  /tmp/castlumora-pinned-native-full-api.log. Full final gate/CI repair pending.
- **Unfinished publication work preserved:** stash
  49a95fefbab944c6e230a6b1bd1d37ec745ec8db, message
  “Preserve tested internal publication boundary while repairing native CI
  dependency setup”. Contains ImportPublication.cs and ImportPublicationTests.cs.
  Twelve real-file/native cases passed, including provenance, cancellation,
  collision safety, authorization, content deduplication and post-commit recovery.
  Log:/tmp/castlumora-publication-tests2.log. No public service/endpoints enabled.
- **Next exact task:** finish/publish this CI repair through the unchanged complete
  gate; confirm required Linux/macOS tests. Then on a clean tree run
  `git stash apply 49a95fefbab944c6e230a6b1bd1d37ec745ec8db` ONCE, retaining stash
  until publication is tested/published. Extend actual library indexing/playback,
  publication fault/security cases, elevated queue API and native management.
  Never recreate or discard the saved source. Public importing stays off until
  the complete authorized single-video boundary is verified.
- **Usage/reset:**Not accessible. Previous session's automatic-review usage block
  interrupted only an artifact download command, not source work; owner resumed.
  No promise of background resume, stable1.0 completion, main merge or deployment.
- **Verified existing preview0d3:**
  https://github.com/dangerousnick/Castlumora/actions/runs/42980593743/artifacts/13837042618
  Castlumora-1.0-dev-0d3ea8c0bd9b-linux-x64.tar.gz; SHA256
  715f211697cf22fa4e99e2d98a478c678e708d7ebb7408b1d57565e286f25d4d.
  Independent outer/inner hash, source, GPL/runtime/server/Web/install/checklist
  and all16branding-file byte comparisons passed; verifier report
  /tmp/castlumora-queue-preview-0d3-verified.json. Initial image-only count assertion
  was a verifier error, not missing assets. Temporary ZIP removed; older previews
  preserved. This intermediate preview has no public importer capability.

## Current continuation — media inspection after published queue lifecycle

- **Latest published code:**0d3ea8c0bd9b90fbbe172b99f7010fa2d36ca951
  (includes5814fb023e), existing development/castlumora-web-1.3 and PR3.
- **Completed queue milestone:** full local hook passed:2,420files format/zero
  changes/no workspace warnings; .NET10 Debug server and Release solution zero
  warnings/errors;17backend suites4,392passed/19existing skips;58frontend tests
  with typecheck/lint/build;9hook/6bundler/5handler cases. Logs
  /tmp/castlumora-prepush.Nh7m0ZhX and /tmp/castlumora-queue-push-corrected.log.
- **Exact-source CI:** required Linux/macOS passed (Tests42980593398;
  jobs130389829007/130389829304), Format42980593570, ABI42980593501,
  OpenAPI42980593484 and Linux preview42980593743 passed. Windows advisory
  job130389829366 failed4owned-staging cleanup assertions after cancellation or
  failure; inherited platform-specific cleanup issue remains tracked, tests/logs
  retained, no assertion relaxed. No Linux/macOS counterpart observed.
- **Intermediate preview:** GitHub artifact13837042618 exists for exact0d3ea8c0bd;
  independent archive verification follows. No public URL import capability is
  claimed; UI still reports queues unavailable. Previous6f04preview preserved.
- **Active next increment:** bounded structural MP4/reference/protection checks,
  restricted native FFprobe properties and actual first-frame FFmpeg decoding,
  generated GPL test clip and real queue/TLS/decoder integration. No public jobs,
  library writes or scan wiring yet. See docs/CASTLUMORA_IMPORT_MEDIA_INSPECTION.md.
- **Preliminary inspection verification:**22focused Debug/native cases passed;
  ten mandatory-hook enforcement cases passed. Before decoder addition, combined
  run63passed/1existing runtime-health timeout while another build overlapped.
  This was not a passing run. Exact stall cause unproven; stop overlapping heavy
  builds/native tests, measure isolated probe health, then rerun affected suite
  without changing its15second production deadline. Async-I/O analyzer errors
  fixed, no suppression. After resume the isolated verified version probe returned
 2026.10.26 in1.586seconds. With no overlapping heavy work the entire Debug API
  suite passed346tests/3existing platform skips/0failures, including genuine
  first-frame decode, corrupt packets and queue/TLS/inspection integration.
  Logs:/tmp/castlumora-media-full-api-tests.log; full-solution formatter corrected
  one of2,422files without workspace warnings; actionlint passed. Required final
  pre-push gate and exact-source CI for inspection still pending.
- **Next exact task after inspection publication:** complete idempotent authorized
  library destination/promotion/scan lifecycle and sanitized metadata, content
  deduplication and restart receipts; then elevated API/native management and
  real imported-video indexing/playback tests. Do not enable unfinished jobs.
- **Usage/reset:**Not accessible. No automatic resume, stable1.0 or full Phase1.4
  claim. No main merge/Coon Media access/deployment. Production backup safeguards
  remain unchanged; preserve every ancestor and completed feature.

## Active continuation — persistent internal importer lifecycle

- Resumed exact clean6f04c2c885dabfa41f2a8a277391a220aa927cbe; remote fetch
  confirmed no newer development commits. Existing hooks installed and retained.
- Implemented internal versioned atomic private journal, exclusive ownership,
  one worker, source/destination deduplication, explicit rights/provenance,
  cancellation/progress/retry/attempt caps and restart recovery. Existing runner
  now reports sampled byte progress without exposing downloader output.
- Public URL jobs remain disabled; library validation/publication and elevated
  API/UI wiring are the next coherent milestone, not declared complete. See
  docs/CASTLUMORA_IMPORT_QUEUE.md for contract and security/recovery boundaries.
- Preliminary Debug API build passed. Sandbox formatting/test attempts could not
  run (proxy/MSBuild pipe/VSTest socket restrictions); equivalent checks moved to
  the approved outer executor. Initial executable cases32passed/1new fixture
  assertion failed (private restart URL confused with exception-only secret);
  corrected fixture separates those values. Analyzer ownership/order issues fixed
  without suppressions. Final focused Debug/native run34passed/0skips/0failures,
  including real TLS shutdown/restart recovery; log
  /tmp/castlumora-queue-native-tests7.log. Full formatter corrections ran across
  2,420 files. Full clean pre-push verification and exact-source CI follow.
- Initial local commit5814fb023e was NOT published: pre-publication review caught
  macOS root /var and /tmp aliases that could reject native temporary fixtures.
  Stopped only this push/validation process tree before publication. Its format
  (2,420files/zerochanges/no warning), Debug and Release builds passed, but the
  interrupted full gate is not a pass. Added exact verified OS-alias normalization
  plus portable mapping and ordinary-symlink rejection cases; full validation must
  rerun on the corrected commit. No user-controlled symlink exception was added.
  Corrected focused Debug/native verification41passed/0skips/0failures; log
  /tmp/castlumora-queue-native-tests-alias.log. Remote remained exactly6f04 after
  stopping the first push. Required final full gate/CI remain to be verified.
- Latest published commit at start6f04c2c885dabfa41f2a8a277391a220aa927cbe;
  normal full pre-push gate remains mandatory before publication. Production
  backup reserve, original tests, Linux/macOS requirements and GPL unchanged.
- Usage/reset: Not accessible. No automatic continuation or full1.0 claim.
- Next exact task: idempotent authorized library publication/media inspection,
  sanitized metadata, hash deduplication, crash reconciliation and scan, then
  elevated queue endpoints/native management only after the boundary passes.

## Completed session milestones — Linux/macOS validation and native Mac runtime

- **Last completed milestone:** pinned universal macOS importer executable support,
  including platform selection, private bundled installation/status and native
  health/TLS/transport/cancellation/cleanup CI. This is an importer prerequisite;
  persistent URL jobs and library promotion remain unfinished and disabled.
- **Latest published commit at this checkpoint:**
  8aa77c0cc9c46f3847907483154a141831503af3. Branch:
  development/castlumora-web-1.3; PR #3. Published commits include platform policy
  c9129c0aeb, native assets f70bc317ca, fixture isolation 8aa77c0cc9, and the saved
  earlier checkpoint 1d40c0c904. Preserve all ancestors, including 0ad2f94608.
- **Required verification passed:** full local gate on 8aa77c0cc9, format 2,418
  files/zero changes/no workspace warning, zero-warning Debug/Release builds,
  17 backend projects 4,365 passed/19 existing skips, 58 frontend tests with
  typecheck/lint/build, 9 hook/6 bundler/5 workflow-handler cases. Logs:
  /tmp/castlumora-prepush.3djp9pd4 and /tmp/castlumora-macos-runtime-push-corrected.log.
- **Exact-source CI:** Linux and macOS Debug/Release/tests passed, as did format,
  ABI, OpenAPI and packaged Linux preview. Tests run 42960456142; native Mac ARM64
  job 130325480859 installed verified osx-arm64 and passed all 17 projects (4,365
  passed/19 existing skips); API 295/3 confirms native cases ran. x64 asset mapping
  and universal header verified, but x64 execution/desktop installation not tested.
- **Windows remains deferred/advisory:** job 130325480811 passed this run (API
  294/4). Earlier five staging cleanup failures did not recur with native fixtures
  isolated from unrelated CPU-heavy tests; this is not Windows release certification
  or proof of complete process containment. Diagnostics retained in artifact
  13829466762. Platform-policy run 42952364673 previously confirmed Windows could
  fail visibly while the required Linux/macOS workflow still succeeded.
- **Real failure preserved:** the f70 attempt was blocked before publishing because
  a 15-second health probe timed out before TLS. No deadline/assertion/production
  guard was relaxed. SDK shutdown found mostly defunct servers, so no RAM-recovery
  claim is made. Controlled probes returned exact version in 1.317–1.457 seconds;
  exact earlier stall cause remains unproven. Collection/project scheduling changed,
  all native assertions stayed, and full API plus full gate and native CI passed.
- **Preview delivered and independently downloaded/verified:**
  https://github.com/dangerousnick/Castlumora/actions/runs/42960456033/artifacts/13829470962
  Archive Castlumora-1.0-dev-8aa77c0cc9c4-linux-x64.tar.gz; SHA-256
  c51fd507d6187ecd889c1b3ebacd7414f2c1f0b3c7f73d55ea01d03475d7b603.
  Expiry 2026-12-11; GitHub sign-in required. Outer/inner hashes/manifests, original
  GPL LICENSE, server/.NET runtime, branded compiled Web, launcher, platform guide
  and 16 branding assets verified. Actual extracted-package setup/authentication,
  restart/library/scanning/playback and tool-install workflows passed in CI.
  Retained ZIP/report: /tmp/castlumora-preview-8aa77c0-artifact/. Older protected
  previews remain intact; no extra expanded application copy was created.
- **Unfinished/known limits:** no full macOS installer/runtime/hardware certification,
  physical Mint/GPU/tuner certification or Docker/NAS deployment certification.
  Main's OpenAPI Report still runs the legacy missing JF_BOT_TOKEN action; its
  tested replacement in PR3 needs reviewed adoption on main. Branch-protection
  inspection denied (HTTP403); administrator must verify Linux/macOS required
  contexts. No main merge, required-check suppression or production deployment.
- **Next exact development task:** implement a bounded persistent authorized
  single-source import lifecycle on existing staging. First audit library APIs and
  secure destination resolution; persist private rights/provenance and queue state,
  cancellation/retry/recovery and deduplication. Add bounded playable-container
  validation with safe FFmpeg/protocol limits, sanitize sidecars, atomically promote
  only authorized complete media, then scan. Keep URL jobs off until this complete
  boundary and permissions are tested. Broader sources/metadata/DVR/profiles and
  every other retained 1.0 requirement remain mandatory; no feature scope cut.
- **Recovery:** fetch and fast-forward the existing branch (preserve any later local
  documentation checkpoint), read the permanent specs/progress/platform evidence,
  install hooks, use .NET10/Node24/Python3 and the verified platform fixture, then
  complete one task before the next. A normal push must pass the full hook; no
  bypass/cached pass or unnecessary restart. Usage/reset: Not accessible. No
  automatic resume is promised. Current documentation checkpoint will also be
  published through that gate; exact validated code/evidence above is 8aa77c0cc9.

## Active code milestone — native macOS importer prerequisite

- **Last completed milestone:** Linux/macOS-required CI and advisory Windows policy.
  Published c9129c0aeb1d1cac89fbc01c13418bdd52a67e80; full hook passed (4,365
  backend tests/19 skips,58 frontend tests,full format,zero-warning Debug/Release,
  nine hook/five bundler/five handler cases). Linux/macOS required jobs,format,ABI,
  OpenAPI and preview passed; Tests workflow42952364673 succeeded despite visible
  Windows failure. Windows TRX artifact13825348646 retained. No Windows retry.
- **Latest published commit:**c9129c0aeb1d1cac89fbc01c13418bdd52a67e80.
  Branch development/castlumora-web-1.3,PR3; local source work follows that checkpoint.
- **Implemented now:** reviewed2026.10.26 universal macOS asset, distinct osx-x64 /
  osx-arm64 selection and private bundling/install/status paths; real native macOS
  fixture preparation in required CI and local macOS gate. Existing Windows/Linux
  pins unchanged. Source archive/licenses retained; no new dependency installer or
  arbitrary executable/update endpoint. Public URL jobs remain disabled.
- **Supply-chain evidence:** API digest, official SHA2-256SUMS and downloaded file
  SHA256 all0f922c15ccc0b83eefa1c8bc9104e434318c4bf16e408e1ee93b7745a6efe0aa;
  size37,466,304; file inspection confirms universal x86_64/arm64. No Mac executable
  ran locally. Six bundler cases and nine hook enforcement cases pass; focused
  focused runtime/Linux regressions25/25 passed. Native Mac execution remains unverified
  until exact-source required CI passes; no graphical macOS install claim.
- **Shared-code investigation:** Linux post-test /proc scan found no surviving
  executables from owned runner/TLS fixture roots. This is an observation, not a
  proof of complete descendant containment; Windows abort/file-lock failures remain
  tracked and must not be hidden. Strengthened Mac native coverage will exercise
  original cleanup/TLS/cancellation assertions without new skips/deadline changes.
- **Other delivery updates:** platform evidence guide now ships in Linux archives;
  README points to completed c912 preview; installation guide distinguishes the
  production backup reserve from startup/test requirements. main push trigger now
  validates the actual base branch as well as retained master; PR triggers unchanged.
- **Verification/publishing:** run full clean-commit hook before a normal push.
  Local Mac execution unavailable; required CI must verify native behavior. main's
  legacy OpenAPI Report token error remains separately tracked; no automatic merge.
  Branch protection administration remains HTTP403; no remote settings changed.
- **Required-gate failure investigated before publishing:** f70bc317ca's full
  hook passed format/Debug/Release/bundler, but API294/3/1failed and other projects
  passed. untrusted source failed before TLS: AcquireAsync's native health probe
  timed out under the unchanged15-second limit. Logs:/tmp/castlumora-prepush.xvwoX0f6.
  No push occurred. Idle SDK services were shut down; most listed compilers were
  defunct, so no memory-reclamation/root-cause claim is made. Controlled native
  probe measurements are retained in session diagnostics; exact stall cause remains
  unproven. Native deadline fixtures now share a non-parallel xUnit collection,
  avoiding unrelated CPU-heavy test contention. Explicit within-test concurrency,
  all assertions, real binaries and production deadlines remain unchanged. CI
  serializes test projects (-m:1), matching the existing local gate; none excluded.
  Isolated probe1.457s; concurrent probes1.317/1.391s, all exit0/exact version.
  Full API after collection scheduling correction passed295/3existing skips:
  /tmp/castlumora-macos-runtime-api-isolated.log. The full gate must be rerun
  before publishing; earlier failed gate remains recorded as failed.
- **Next task after this increment:** persistent authorized single-source import
  lifecycle using existing staging; rights/provenance, secure destination policy,
  playable-media validation, sanitized metadata, deduplication, durable state,
  cancellation/retry/recovery and successful scanning before exposing public jobs.
  Preserve all1.0 requirements; no production/OS readiness inferred from this task.
- **Usage status:** Not accessible. No scheduled reset/automatic resume claim.
  Recovery: fetch existing branch; preserve local commits; install hooks; use.NET10,
  Node24 and verified platform bundle; run full gate and inspect required CI.

## Active milestone — Linux-first/macOS-required release policy

- Owner update2026-11-11 supersedes the earlier Windows release blocker. Continue
  existing branch/PR3 and all saved commits; Windows CI advisory, not removed.
  Linux/macOS and shared formatting/security/API checks remain mandatory.
- CI keeps stable Linux/macOS test contexts, adds explicit Debug/Release builds,
  uses Windows-only job continue-on-error, and retains Windows TRX diagnostics even
  on failure. No assertions/deadlines/compatibility implementation removed.
- Backup isolation fix already published at0293885c4b and remains intact. Ten real
  backup cases and full gate passed previously; rerun unchanged cases in this gate.
  Production reserve remains5,368,709,115 bytes; no general5GiB gate prerequisite.
- Branch-protection GET returned HTTP403 Resource not accessible by integration.
  Ruleset listing returned[] but does not establish branch protection. No remote
  security settings changed; exact administrator check contexts are documented in
  docs/CASTLUMORA_PLATFORM_SUPPORT.md. Main remains untouched.
- Windows staging cleanup failures remain deferred with shared-code investigation
  required before public importer exposure. Main OpenAPI Report still uses the
  legacy missing-bot-token action; PR3 contains the tested replacement, requiring
  reviewed adoption on main. Do not suppress this failure or auto-merge main.
- Verification/publication of this milestone pending. Latest published source at
  start:0293885c4bb5bee25a912d0903916eecaec31020; local checkpoint1d40c0c904
  preserved. Full gate must pass before a normal hook-protected push.
- Next meaningful task after publication: extend the existing managed importer
  prerequisite with a reviewed pinned macOS native executable and real macOS CI
  health/transport tests, removing the currently documented native-runtime gap.
  Then resume persistent authorized single-source jobs with secure destinations,
  media validation, rights/provenance, cancellation/retry and deduplication.
- Usage allowance/reset: Not accessible. Local host Linux, .NET10/Node24; macOS
  verification requires CI and is not claimed locally. Preserve all1.0 requirements,
  GPL notices, source/history, previous previews and commits including0ad2f94608.

## Published backup fix; CI correction checkpoint — 2026-11-11

- **Last completed milestone:** isolated per-instance backup reserve policy with
  real SQLite export/ZIP tests. Production reserve unchanged. Local implementation
  190d5ea9ed36b51044d218228b2524a6e8272681; verification documentation0293885c4b.
- **Latest published commit:**0293885c4bb5bee25a912d0903916eecaec31020.
  Branch development/castlumora-web-1.3; PR3 remains open. Normal push succeeded
  only after the complete mandatory hook passed. No main merge or deployment.
- **Local verification:** complete gate passed on implementation and final published
  HEAD, including full format2,417 files/zero changes/no workspace warning,
  .NET10 Debug/Release builds zero warnings/errors,17 backend projects4,365 passed,
  19 existing skips/zero failures,10 focused real backup cases,9 hook cases,
  5 bundler/5 workflow-handler cases, Node24 frontend58 tests/typecheck/lint/build.
  Logs:/tmp/castlumora-prepush.XfcHPStd, /tmp/castlumora-prepush.OUGDyPgy and
  /tmp/castlumora-backup-policy-push.log. Workspace needs no extra disk for this
  validation design; production backup safety is not reduced.
- **Exact-source GitHub:** Format42946892790, ABI42946892825, OpenAPI42946892951,
  Linux/macOS tests and packaged Linux preview42946892764 passed. Windows
  job130281739705 in Tests42946892971 failed five staging-runner cases:
  metadataLarge,streamLarge,unexpectedFile,cancellation leave staging directories;
  tiny-deadline/output test teardown cannot delete locked yt-dlp.exe.
  No backup case failed. Existing TLS fixture EOF failures did not recur.
- **Unfinished correction:** abort-time Windows subprocess/file-handle cleanup.
  Evidence is consistent with descendant teardown/deferred file locks, but the
  precise cause is not proven. No test skips, deadline relaxation, assertion
  removal or speculative unverified native-interoperability patch was applied.
  Linux cannot independently certify Windows process/file-lock semantics.
- **Separate main workflow blocker:** OpenAPI Report42947167543 executes
  main@f63d87faaaa29976148e271baea86deb86617ea2, not this development commit.
  Job130282668054 fails with "Parameter token or opts.auth is required" because
  main still uses secrets.JF_BOT_TOKEN. The standard github.token replacement and
  trusted handler already exist in PR3 and pass five local handler tests; adoption
  on main requires review. Do not automatically merge PR3 or alter main to evade
  this limitation. This is not a GitHub write-access failure; the normal push worked.
- **New intermediate test build:** verified download
  https://github.com/dangerousnick/Castlumora/actions/runs/42946892764/artifacts/13823357351
  (GitHub sign-in; expires2026-12-11). Archive
  Castlumora-1.0-dev-0293885c4bb5-linux-x64.tar.gz SHA256
  fef0ebd5e0d7385d9108b27738ebc1ff79dac99fef801ea3eb555a7e5a28feec.
  Downloaded outer ZIP matches GitHub digest97a206f9107aab0896a2b7abebd92dce6be494239d506455074285573a55c2ba.
  Outer/inner source manifests match published SHA; GPL LICENSE bytes match source;
  server/runtime, compiled branded Web, castle assets and launcher verified by
  streaming archive inspection. CI tested actual extracted fresh setup/auth,
  restart/library scanning/playback and bundled runtime installation. No local
  Mint/GPU/tuner certification. Metadata/import completeness flags remain honest.
  Retained at /tmp/castlumora-preview-0293885-artifact/github-artifact.zip; did not
  overwrite/delete older protected previews or create another expanded copy.
- **Next exact task:** inspect Windows job logs above; reproduce abort/output-bound
  and cancellation cases on Windows with owned-process/cleanup diagnostics (redact
  source/proxy secrets); ensure owned descendants and file handles are closed before
  cleanup/lease release. Preserve all existing assertions/time limits. Run all runner
  cases on Windows and Linux, then the complete clean-commit gate before publishing
  a correction. Separately arrange review of the existing default-main report fix.
  No repeated pushes of known failing source and no new unrelated features meanwhile.
  Then resume the persistent authorized single-source importer; URL jobs remain off.
- **Recovery:** fetch/fast-forward the existing development branch to published0293885c4b,
  preserve any later local documentation commit, install hooks and use .NET10/Node24.
  Existing verified tool fixture:/tmp/castlumora-ytdlp-runtime-bundle.
  Preserve0ad2f94608, all completed work and recovery bundles. No reset/restart.
  Usage allowance/reset: Not accessible. No automatic background resume is promised.

## Latest verified milestone — isolated backup capacity policy

- Production backup reserve remains exactly5,368,709,115 bytes and the existing
  public constructor/DI wiring is preserved. An internal per-instance policy allows
  real backup integration tests to use a1MiB reserve without global overrides.
- Tests cover production rejection below the reserve/unknown space, exact-boundary
  acceptance, isolated requirement enforcement and independence, invalid policy
  construction, real database ZIP contents and corrupt-keyframe recovery.
- Gate capacity readings are now informational; every actual build/test failure
  remains fatal. Nine private-remote hook enforcement tests pass, including ENOSPC
  during a build and a complete pipeline despite low reported free space.
- Targeted .NET10 backup verification passed:10/10 tests, actual ZIP contents
  inspected; Release test-project build passed with zero warnings/errors.
  Complete gate passed on190d5ea9ed36b51044d218228b2524a6e8272681:
  full-solution format2,417 files/zero changes/no workspace warning; Debug server
  and Release solution builds zero warnings/errors;17 backend projects4,365 passed,
  19 existing skips/zero failures;9 hook enforcement,5 bundler and5 workflow-handler
  tests; Node24 install/typecheck/lint,58 frontend tests and production build passed.
  Logs:/tmp/castlumora-prepush.XfcHPStd; focused backup log:
  /tmp/castlumora-backup-policy-tests-final.log. Frontend dependency/chunk-size
  warnings remain nonfatal and were not suppressed.
- Publishing: normal pre-push hook must repeat the complete gate on the final
  documentation checkpoint; do not bypass it. GitHub exact-source CI is not yet
  verified. The latest published SHA recorded below is the state before this push.
  No Windows/Mint/GPU/manual playback success is inferred from Linux tests. See
  [policy design and verification](docs/verification-backup-storage-policy.md).
- Earlier sections requiring a5GiB build workspace describe historical source.
  This refactor supersedes that requirement; production backup safety is unchanged.
- Latest published commit at checkpoint preparation:2b79ffb136f2c46dca52b1a8562c38f37da2f94e.
  Branch development/castlumora-web-1.3, PR3. Preserved all prior local commits,
  including0ad2f94608 and staging896b5fe4dc; no reset, deployment or main merge.
- Recovery: inspect clean HEAD, install hooks, set .NET10/Node24 PATH and the
  existing CASTLUMORA_TEST_YTDLP_BUNDLE cache, run scripts/validate-before-push.sh;
  fix every failure, publish through the mandatory hook, inspect exact-source CI.
  No additional disk is currently required for validation; the isolated policy
  resolved the fixture design issue without changing production safety.
  Then continue the authorized persistent single-source import slice described in
  docs/CASTLUMORA_IMPORT_STAGING.md. Usage allowance/reset: Not accessible.

## Latest local milestone — supervised direct-HTTPS import staging

- **Completed scope:** internal verified yt-dlp process supervision and private
  direct-HTTPS MP4 staging, fixed arguments, validating authenticated tunnel,
  output/resource bounds, cancellation, redacted failures and owned cleanup.
  No public queue/URL jobs, media promotion or UI enablement. See
  [staging contract and limitations](docs/CASTLUMORA_IMPORT_STAGING.md).
- **Verification:** .NET10 standalone API Debug and API-test Release builds passed
  with zero warnings/errors; complete API suite 295 passed/3 existing skips.
  Fourteen staging cases include actual pinned executable and owned TLS fixtures.
  Initial focused run was 11/12 because the existing runtime health probe timed out
  during fixture installation; isolated probe 4.44 seconds and subsequent full API
  suite passed without relaxing deadlines. Final full-solution formatting passed:
  2,416 files, zero changes, no workspace-load warnings; no full-gate pass is claimed.
  Logs: /tmp/castlumora-import-runner-format-final.log,
  /tmp/castlumora-import-runner-debug.log,
  /tmp/castlumora-import-runner-tests-build.log and
  /tmp/castlumora-import-runner-api-tests.log.
- **Current branch:** development/castlumora-web-1.3. Local milestone commit is the
  896b5fe4dca7b550839f763873376319ce2a7f4c. Preserved ancestors include 0ad2f94608 and
  storage audit4606f8e976. **Latest published commit remains exactly
  2b79ffb136f2c46dca52b1a8562c38f37da2f94e.** PR3 is unchanged.
- **Publishing blocker:** about3.95GiB free after rebuilding, below the unchanged
  5GiB backup-test/full-gate preflight. Gate on896b5fe4dc passed all nine hook
  enforcement tests, then exited1 at disk preflight:4,243,009,536 bytes free versus
  5,368,709,120 required. Logs:/tmp/castlumora-prepush.vHF7854G and
  /tmp/castlumora-staging-final-gate.log. It did not retry the backup test.
  The latest continuation request was used
  for this bounded local importer task only, not as permission to bypass validation
  or publish. No gate weakening, push, production deployment or main merge.
- **Unfinished work:** production import queue, rights/provenance, playable-container
  validation, destination permission checks, sanitized metadata, promotion/scanning,
  recovery, global quotas and full creator sources remain unavailable. Windows,
  real-media/Mint and full regression verification remain unverified for this source.
- **Exact next task:** obtain at least5GiB free plus build headroom; keep these local
  commits; install hooks with `./scripts/install-git-hooks.sh`; set PATH to .NET10
  and Node24 and CASTLUMORA_TEST_YTDLP_BUNDLE=/tmp/castlumora-ytdlp-runtime-bundle;
  run `./scripts/validate-before-push.sh` on clean committed HEAD. Fix all failures,
  then publish through the hook and inspect exact-source CI before exposing jobs.
  After that complete the single-source persistent queue slice in the staging doc.
- **Usage status:** Not accessible. No automatic reset/resume mechanism is claimed.
  The earlier recovery bundle remains preserved; current working Git history is
  authoritative for the new local checkpoint. Existing downloadablecafc preview
  remains an intermediate older build and does not contain this new staging work.

## Storage cleanup investigation — validation still blocked

Preserved0ad2f94608 and all completed application work. Safe cleanup reclaimed
1.59GiB: free space 2.64→4.23GiB.
Removed only regenerable outputs, obsolete preview copies, temporary test data and
unused/download caches; retained source/Git/docs, backup-test binaries, current
required dependencies, SDK, validation logs, recovery bundle and both protected
previews (archive checksums reverified). Application/test/gate code unchanged.

Free space remains about0.77GiB below the unchanged5GiB requirement,
so the backup test was not retried and no validation success or publishing
permission is claimed. Additional capacity is required under the current setup,
with build headroom beyond5GiB; roughly2GiB extra is practical. See
[storage audit](docs/verification-storage-cleanup.md) and the detailed deletion
manifest at /tmp/castlumora-storage-cleanup-report.json. After capacity is added,
rerun the backup test and full mandatory gate, then inspect exact-source CI before
resuming importer development. No push, deployment, main merge or automatic resume.
Usage allowance remains Not accessible.

## Current blocked checkpoint — no new commits pushed

- **Latest published source:**2b79ffb136f2c46dca52b1a8562c38f37da2f94e (known CI failures investigated, not retried). **Local corrections:**32b67f9039 (SA1508/gate),9f5c9478df (frozen snapshots/TLS fixture key import),005a2f1cd7 (storage-safe dependency hard links and hook protection), plus the following final resource-check/documentation commit. Branch development/castlumora-web-1.3; PR3 unchanged.
- **Passed locally on005a source:** full solution format,2,413 files/zero changes/no workspace warning; .NET10 Debug server build and Release solution build, zero warnings/errors;9 isolated pre-push enforcement tests. Full CI-equivalent17-project test run completed:4,341 passed/19 existing skips/1 environmental failure. API281/3 and HTTP132/3 passed (subsets, not additional totals).
- **Independent remaining checks:** Node24 npm ci/typecheck/lint/build and58 frontend tests passed;5 workflow-handler tests and5 bundler tests passed;9 gate enforcement tests passed. These do not substitute for the failed mandatory gate. Logs:/tmp/castlumora-validation-web-*.log and /tmp/castlumora-validation-handler.log.
- **Blocking failure:** existing backup corruption unit test calls production static StorageHelper, which requires5GiB of free space; current8GB filesystem hasabout2.7GiB free after hard-link recovery. Backup guard and test remain unchanged. No exclusions/skip flags/analyzer changes/hook bypass. The full gate exited1. Full gate logs:/tmp/castlumora-prepush.anj2jNud. Therefore no push is permitted; no claim of complete validation.
- **Gate:** installed repository-local core.hooksPath=.githooks. Full formatting, Debug ABI build, Release build/all tests/real platform bundle, handler/frontend checks; immutable HEAD checks each stage,9 bare-remote enforcement cases, no successful-result cache. SDK dependency hard links retain all bytes/assets. Preflight now requires5GiB so the known resource block fails early. Source/content test fixtures are not hard-linked. Installer preserves other hooks.
- **Windows:** old CI job129965793527 had four TLS EOF fixture failures. Shared DefaultKeySet PKCS12/server-auth fixture and observed server-task failures prepared; Linux tests pass, Windows is unavailable locally and remains unverified until a future fully validated push/CI run. Do not report Windows success.
- **Recovery bundle:** /workspace/Castlumora-ci-validation-unpublished.bundle contains only unpublished commits and requires published base2b79ffb136f2c46dca52b1a8562c38f37da2f94e. On the existing clean development branch, fetch that bundle's HEAD and fast-forward FETCH_HEAD; do not create another branch or publish before validation.
- **Exact next recovery steps:** use an executor with5GiB free plus build headroom; preserve/import the unpublished local commits, install hooks, set PATH to .NET10/Node24 and CASTLUMORA_TEST_YTDLP_BUNDLE if reusing verified cache, run scripts/validate-before-push.sh in a clean committed tree. Fix every failure before publishing through the hook; inspect exact-source CI. Only then resume bounded importer subprocess supervision and persistent authorized queue. Do not regenerate completed functionality or modify backup thresholds to evade the gate.
- **Preserved:** all source, SDK/global packages, logs, saved build archives and latest verifiedcafc preview. Only stopped generated duplicate extractions/failed bin directories and redundant NuGet HTTP download cache were removed. No Coon/main changes or deployment. **Usage:** Not accessible; no auto-resume promise.

## Active storage recovery for complete validation (not published)

Required gate on9f5c9478df passed complete format (2,413 files, zero changes) and
Debug server build (zero warnings/errors), but Release solution build failed
ENOSPC/Bus error on the8GB workspace. The gate exited1 and no push occurred.
Full tests/frontend stages were not reached; do not represent this as a pass.
Logs: /tmp/castlumora-prepush.v1RcuxL8. Cleared only stopped generated extractions,
failed bin outputs and redundant NuGet HTTP download cache. Preserved all source,
logs, package dependencies and saved build archives; no production process touched.

Standard SDK dependency hard-link copy support was verified. The gate now enables
CreateHardLinksForCopyLocalIfPossible (same file bytes, no omitted assets/checks),
checks disk before builds/tests and refuses to shadow existing default hooks.
Nine isolated enforcement tests pass. Next: freeze this source, rerun the entire
gate after storage recovery, then publish only after complete success and inspect
exact-source CI, especially the proposed Windows Schannel fixture correction.
Current last published source is2b79ffb136f2c46dca52b1a8562c38f37da2f94e; branch
and PR remain unchanged. No CI weakening or hook bypass. Usage Not accessible.

## Continuing CI correction — frozen source and portable TLS fixtures

- Local full-solution formatting of32b67f9039 finished with zero changed files/diagnostics after removing SA1508; unlike the earlier scoped check it loaded the full solution and ran Debug analyzer references. Further source edits require rerunning the full gate; no partial pass authorizes publication.
- Investigated Windows test job129965793527: four TLS fixtures failed with unexpected EOF (including trusted fixtures), consistent with Schannel/private-key handling. Added a shared DefaultKeySet PKCS12 fixture import with server-auth usage and observation of server-task exceptions. Kept all certificate/hostname/payload assertions; no skips or CI weakening. Windows execution is unavailable locally and remains explicitly unverified until CI.
- Hardened gate snapshot checks before each stage; six isolated bare-remote enforcement tests pass, including source mutation during checks. Real validation of this final source is pending; no push until all required checks pass. Current latest published source remains2b79ffb136f2c46dca52b1a8562c38f37da2f94e, branch development/castlumora-web-1.3, PR3. Further local commits preserve corrections but are not publishing authorization.
- Next: complete full formatting/Debug/Release/CI-equivalent tests on frozen committed source, investigate every failure and report environment blockers rather than push. Only then publish through the enabled hook, inspect exact-source CI and resume importer runner. No Coon/main deployment or merge. Usage allowance Not accessible.

## Active correction — mandatory complete pre-push validation

CI Format42855077268/job129965793560 and ABI42855076911/HEAD job129965792607
both failed SA1508 at YtDlpRuntime.cs392 (blank line before closing brace).
Removed the blank line. Earlier scoped formatting/Release checks missed the full
Debug analyzer failure; they are not accepted as proof of a clean full checkout.

Added scripts/validate-before-push.sh, tracked .githooks/pre-push and installation
helper; activated the local hooksPath. The gate requires clean committed HEAD,
.NET10/Node24, full-solution restore/format (workspace warnings fail), Debug ABI
server build, Release solution build/full tests with CI coverage and real pinned
platform fixture, workflow-handler/frontend checks. No skip/cache/bypass is used.
Five isolated bare-remote tests pass: format/workspace/build/test failures block
publishing, and simulated successful checks publish only validated HEAD. These
mock-tool tests validate enforcement only, not application build success.

Current actual verification is pending. Initial full formatter emitted workspace
warnings, so it was stopped for a restore/diagnostic rerun rather than called a
pass. No push is authorized by this checkpoint until the entire real gate passes.
See docs/PRE_PUSH_VALIDATION.md. Latest published source remains2b79ffb136f2c46dca52b1a8562c38f37da2f94e;
current branch development/castlumora-web-1.3, PR3. Usage allowance Not accessible.
Next: finish all real checks, investigate any failure, then publish once with the
hook enabled; inspect exact-source GitHub results before resuming importer runner.
No Coon access, deployment or main merge.

## Latest continuation checkpoint — actual pinned executable HTTPS verification

- **Last completed milestones:** bounded authenticated tunnel528f0750190ecd4d6171a4a7ee4b4207788bcc5f; verified runtime leases/administrator409/shutdown race correctioncafc4590a804631a30399eb4d76c8b881ef72cba; real pinned executable trusted/untrusted owned-HTTPS transport tests in the following test-only commit.
- **Latest published tested application commit:** cafc4590a804631a30399eb4d76c8b881ef72cba, development/castlumora-web-1.3, PR3 open against main. Test/documentation checkpoints follow; inspect Git log. No uncommitted application implementation is required for recovery.
- **Verification:** API/test Release builds zero warnings/errors; full local API281 passed/3 existing skips. Actual pinned Linux tool successfully transfers exact owned HTTPS fixture bytes and sidecar through authenticated tunnel, rejects untrusted certificate with no media file. Certificate verification remains enabled.14 tunnel cases/59 policy cases/actual health included; five earlier focused tunnel repeats70/70 pass after shutdown-race correction. Scoped API formatter and new-test whitespace formatting pass. Physical Mint, hardware and new executable-test Windows run not claimed. See docs/CASTLUMORA_IMPORT_NETWORK_POLICY.md.
- **Verified downloadable intermediate preview:** https://github.com/dangerousnick/Castlumora/actions/runs/42853221613/artifacts/13781693082 . Exact cafc source; packaged CI startup/setup/restart/library/play/runtime-install smoke passed. Actual download/checksum/manifest/server/web/tool verified. Archive Castlumora-1.0-dev-cafc4590a804-linux-x64.tar.gz, SHA256 33f3a924efa5557985f4d3ff4ac99f350bac87b4f98a2ea9dd643bdd35c5a67f; expiry2026-12-10T16:22:39Z. Retained at /tmp/castlumora-preview-cafc-artifact. Prior archives preserved. New test-only changes are not in the package manifest; application behavior is cafc.
- **CI:** explicit existing preview workflow dispatched and verified. Automatic PR checks for recent source were not reported by the PR-filtered app during inspection; workflows active and PR mergeable. Do not claim full multi-platform CI for cafc or the later test-only commit. Prior8b checks passed. No rerun of old source is used as proof for new code.
- **Unfinished/next exact task:** implement fixed-argument bounded native downloader supervision using verified AcquireAsync lease and ImportTunnel; private server-owned staging, clean environment, bounded output/time/file/traffic, owned-process cancellation and safe errors. Reuse actual executable HTTPS fixture and add cancellation/excess-output/size/recovery tests. Then durable authorized queue, server-selected safe library destinations, provenance/dedup/history/cancel/retry/restart/scan and native URL controls. downloadJobsAvailable=false; Phase1.4 remains incomplete. Do not regenerate earlier work or declare final acceptance from this intermediate build.
- **Usage status:** Not accessible; no verified account allowance/reset or automatic resume. **Recovery:** inspect clean Git status/log/remote and permanent specs, preserve latest published source/artifacts, continue existing branch/PR only; use local fixture credentials privately and never target Coon. No main merge or deployment.

## Current continuation — verified runtime leases and tunnel shutdown fix

- **Last completed milestone:** execution leases revalidate pinned bytes/version and prevent executable replacement until all owners release; idempotent disposal, active-execution status and administrator409 conflict. Fixed the tunnel listener stop/accept race found by a subsequent full suite run.
- **Current branch:** development/castlumora-web-1.3; PR3 open/mergeable against main. Latest published adapter commit528f0750190ecd4d6171a4a7ee4b4207788bcc5f; this corrected lease checkpoint follows it. No incoming origin/main commits at the last fetch.
- **Verification:** API/test Release builds zero warnings/errors; full API279 pass/3 existing skips. Lease regression covers multiple reservations,409,repeated disposal,tampering and canceled acquisition. Existing actual pinned Linux health,14 tunnel cases and59 network-policy cases included. Five focused tunnel reruns all passed14/14 (70 additional executions); targeted API formatter verify-no-changes and whitespace checks pass. No production download claim.
- **CI limitation:** latest528f adapter push has only a skipped Merge Conflict Labeler run42852097373; Tests/Format/ABI/OpenAPI/preview have not appeared. Workflows report active, OAuth identity is dangerousnick, PR open/mergeable, main has no incoming change; reason not established. Do not claim new source CI passed or rerun old source as proof. Prior8b checkpoint has all checks green and preview42779834003 artifact13745797052 uploaded (not independently downloaded here); verified584e package remains preserved.
- **Unfinished and exact next task:** fixed-argument bounded native downloader supervision using AcquireAsync and ImportTunnel, private server-owned staging, clean environment, output/time/file/traffic limits and owned-process cancellation. Test the actual pinned executable against owned HTTPS fixtures with certificate/redirect/authorization/error cases. Then complete persistent authorized queue/destination/history/cancel/retry/recovery/dedup/provenance/library scan and native UI. downloadJobsAvailable=false remains truthful; full Phase1.4 incomplete.
- **Usage status:** Not accessible; no usage percentage/reset verified or automatic resume promised. **Recovery:** inspect Git status/log, read all permanent specs and this checkpoint, continue existing branch/PR, retain prior archives/logs/fixtures and server compatibility. No main merge, Coon access or deployment.

## Current continuation — bounded import tunnel

- **Last completed milestone:** internal per-job authenticated loopback HTTPS CONNECT adapter, fresh secret, pinned public-address validation on every target, four active/32 total connections,4KiB headers/five-second handshake,30-second inactivity,ten-minute lifetime and64MiB shared traffic budget. TLS remains end-to-end. Not yet connected to a downloader endpoint.
- **Branch:** development/castlumora-web-1.3, PR3. Previous published checkpoint8b3ba5a692ccc88d8d51330ed0c9c4f0ba5c3265; this adapter commit follows it. Prior checkpoint passed Tests/Format/ABI/OpenAPI and preview42779834003 uploaded artifact13745797052; newer artifact has not been independently downloaded here. Verified584e preview remains preserved.
- **Verification:** API/test Release builds zero warnings/errors; full API278 passed/3 existing skips, including14 real loopback tunnel cases, TLS hostname rejection/encrypted traffic/concurrency/bytes/cancellation, plus pinned actual Linux runtime regression. Broad formatter caused a compiler process exit; scoped formatting and sequential single-worker rebuild succeeded. Rejected-TLS fixture cleanup corrected and rerun. No actual yt-dlp download, frontend or physical Mint test claimed for the adapter.
- **Unfinished:** downloader supervision and managed executable leases, source/destination validation, durable jobs/history/cancel/retry/recovery/dedup/rights/scan and URL controls remain unfinished. downloadJobsAvailable=false is intentional. Full Phase1.4 is incomplete.
- **Next exact task:** add a verified executable lease that prevents runtime replacement during a job; a fixed-argument native downloader runner with private staging/clean environment/bounded output/time/size, owned-process cancellation and mandatory tunnel; exercise actual pinned yt-dlp against authorized owned HTTPS fixtures before wiring persistent jobs and native controls.
- **Usage:** Not accessible. **Recovery:** inspect Git status/log and permanent specs; preserve current preview/source; continue existing branch/PR only. No main merge, production deployment or Coon access.

## Current continuation — import connection safety prerequisite

- **Last completed milestone:** internal HTTPS/public-address import policy with bounded DNS/connect, mixed-answer rejection and numeric endpoint pinning on every connection. No enabled download endpoint or change to the verified runtime preview.
- **Current branch:** development/castlumora-web-1.3, PR #3. Latest published connection-policy commit: 657ec2d4eddadcc3112adb5ebaf2111b877adaa1; tested downloadable runtime preview: 584e58c4edced3c5addcb92bd060748ab6591dd6. Documentation-only checkpoints may follow.
- **Verification:** Release API and API-test builds, zero warnings/errors; full API264 passed/3 existing skips, including59 new connection-policy cases and actual bundled Linux runtime health. Targeted formatter and whitespace checks pass. Clean-source CI remains pending: Tests42779588325, Format42779588285, preview42779588051. PR progress comment https://github.com/dangerousnick/Castlumora/pull/3#issuecomment-6762226806. No real download, proxy/TLS integration, streaming or Mint certification claimed for this internal prerequisite. See docs/CASTLUMORA_IMPORT_NETWORK_POLICY.md.
- **Unfinished:** egress adapter not connected to yt-dlp; queue/destinations/history/cancel/retry/recovery/dedup/rights/scan and native URL UI remain unfinished. `downloadJobsAvailable=false` remains accurate. This is a smaller prerequisite checkpoint, not completion of the promised single-source queue or Phase1.4.
- **Next exact task:** implement bounded authenticated loopback subprocess CONNECT egress using ImportNetworkPolicy for every target; maintain original TLS hostname validation and validate redirect/resource connections; test private redirects, DNS rebinding, malicious headers/auth, cancellation and byte/connection limits. Then complete server-selected destination validation and persistent single-source job lifecycle before enabling imports.
- **Usage status:** Not accessible. **Recovery:** preserve clean checkpoints; read permanent requirements/progress/parity/roadmap; inspect Git status/log before continuing existing branch. Verified downloadable584e preview and local extracted package remain intact. No automatic resume, main merge or production access.

## Latest verified managed-runtime checkpoint

- **Last completed milestone:** native pinned yt-dlp runtime installation/health and Linux distribution; controlled platform assets and original license/source materials, no arbitrary executable/URL/flags or auto-update. Native Imports & Sources page is functional for runtime management, not URL jobs.
- **Latest published tested application commit:** 584e58c4edced3c5addcb92bd060748ab6591dd6 on development/castlumora-web-1.3, PR #3 open against main. Documentation-only checkpoints may follow.
- **Verified download:** https://github.com/dangerousnick/Castlumora/actions/runs/42749466169/artifacts/13732643307 . Archive Castlumora-1.0-dev-584e58c4edce-linux-x64.tar.gz, SHA-256 a229ad90f93de09276cbb77d6f1e1d54257a5ff0725dbcadf4f01664b9b14e9e; expires 2026-12-10T03:29:22Z. Actual download/checksum/manifest/tool digest/licenses/source/branding verified. Prior previews preserved.
- **Verification:** API205/3 existing skips, HTTP132/3 existing skips, frontend58/typecheck/lint/build and bundler5 passed locally. Clean CI Format/ABI/OpenAPI Check and full server suites Linux/Windows/macOS pass. Linux4,266 pass/19 skips; Windows4,257 pass/28 skips; actual platform-specific pinned asset health tests ran. CI actual package startup/restart/library/play/runtime installation passed. Exact independently downloaded package also passed native install/reinstall/reload/health/viewer denials and existing movie play/progress. Report: docs/verification-managed-ytdlp-runtime.md.
- **Unfinished:** URL jobs/source authorization/egress/DNS/redirect/SSRF/destination policy, persistent queue/history/cancel/retry/resume/deduplication/scan-on-success and broader 1.0 requirements. Runtime and importer readiness are explicitly distinct. No half-implemented download endpoint exists at this checkpoint.
- **Next exact task:** implement one complete authorized-single-source queue increment using the managed executable, stable server state and native admin UI. Define completion/security boundaries before coding; validate every outbound connection and destination, persist job transitions and provenance, test cancel/retry/restart/dedup/viewer denial and actual authorized synthetic import/scan before enabling downloads. Preserve all finished metadata/DVR/runtime code and previews.
- **Usage:** Not accessible. **Recovery:** inspect Git status/log/remote and permanent requirements; fetch/fast-forward this existing branch only when clean. Current actual package lives in /tmp/castlumora-runtime-preview-extracted; actual bundled tool fixture in /tmp/castlumora-ytdlp-runtime-bundle. Loopback fixture root/credentials remain private under /tmp/castlumora-web-real-server; never target Coon. Rebuild large generated bin outputs as needed; archives/source/logs retained. No automatic resume or main merge promised.

## Current managed-runtime milestone — ready for tested publication

- **Completed scope:** pinned yt-dlp 2026.10.26 Linux/Windows x64 asset selection; verified build bundle with upstream license/source materials; offline administrator install/status; atomic verified candidate replacement and persisted file state; bounded clean-environment health; native Imports & Sources runtime controls. No arbitrary tool path/URL/flags or automatic updater. `downloadJobsAvailable=false`; URL queue remains unfinished.
- **Branch:** development/castlumora-web-1.3, PR #3. Previous published checkpoint ac5432656543f66fccfac355bad71a2d0fd323f2 includes synchronized origin/main requirements through f63d87faaa. This runtime implementation commit follows it; exact source/build evidence is recorded after publication.
- **Local verification:** Release API/server builds pass; API205 passed/3 existing Windows skips, including ten runtime tests and real pinned Linux install/reopen; full HTTP integration132 passed/3 existing skips; frontend58/typecheck/lint/build and bundler5 pass; targeted formatter exit0 with workspace-load warnings. Real compiled server/web native install/reload/recheck and viewer GET/POST denial passed. Excess-output cleanup bug fixed and rerun; reinstall test waits for actual response rather than stale Ready state. CI will test the real pinned Windows/Linux binaries, and preview smoke now installs the bundled runtime. Do not claim CI/package pass until observed.
- **Unfinished:** entire authorized downloader queue/source/destination/egress/SSRF policy, retries/resume/history/bulk/schedules/scan-on-success and all other retained 1.0 work. This is one complete runtime prerequisite, not a production importer or full Phase1.4 acceptance.
- **Next:** publish this tested checkpoint, verify clean CI and a new downloadable/extracted preview including runtime installation. Then implement one bounded authorized-single-source queue milestone using this managed asset lifecycle and existing metadata/admin/library services. No architecture restart.
- **Usage:** Not accessible. **Recovery:** preserve work, read current Git status/log/remote and permanent requirements; source assets are pinned in packaging/yt-dlp/release.json, no binaries committed. Local actual bundle is /tmp/castlumora-ytdlp-runtime-bundle; compiled disposable service is /tmp/castlumora-ytdlp-server against the marked loopback fixture only. Generated large test/server bin outputs were cleared after passing suites; source/logs/current preview archives retained. Old verified downloadable 835 preview remains available until a newer upload is verified. No Coon/official Jellyfin change, production deploy or main merge.

## Continuation — synchronized main requirements and managed importer runtime

Merged origin/main through f63d87faaa (requirements only; no application replacement). Complete 1.0 features remain required; deep inherited-core replacement and optional marketplaces belong to 2.0. Windows/Linux adapters are required where OS behavior differs. Next bounded task is pinned managed yt-dlp runtime: verified platform asset, bundled Linux dependency, safe installation/health lifecycle and administrator controls, with tests before queue/download endpoints. The existing working preview and completed metadata/DVR features are preserved. Usage allowance/reset remains Not accessible.

## Latest published tested application checkpoint — Node24, native metadata and refreshed preview

- **Last completed milestones:** Node24 LTS/Actions/authentication; native source-aware retained creator sidecars and safe browsing/descriptions; native Metadata Management settings/corrections/locks/repair; refreshed packaged owner checklist/manifest and actual-package DVR verification.
- **Latest published tested application commit:** `835716785add501a27d98e979fa67d411c63b570`. Current branch `development/castlumora-web-1.3`; PR https://github.com/dangerousnick/Castlumora/pull/3 remains open against main. Documentation-only recovery commits may follow; inspect Git log and preserve work rather than resetting.
- **Download:** https://github.com/dangerousnick/Castlumora/actions/runs/42741231279/artifacts/13730390071 . Successful build run 42741231279. GitHub sign-in required; artifact expires 2026-12-10T02:26:34Z. Archive `Castlumora-1.0-dev-835716785add-linux-x64.tar.gz`; SHA-256 `030ffa8840f267032b1fe9a6073f2a75b4c0520c2d6ac7041ba1c0ad10ce01ca`. Actual download, safe paths, checksum and exact manifest verified independently through connected GitHub app. GPL/branding/compiled server+web/guides confirmed. Prior previews retained.
- **Verification:** Node24 clean install/typecheck/lint/build, 56 frontend tests and five Node comment-handler tests passed. Exact-source full server CI passed on Windows/macOS/Ubuntu; Ubuntu 17 projects: 4,255 passed, 19 skipped, zero failures. Provider 532/LiveTV 81/API 195 with 3 skips/integration 131 with 3 skips are subsets, not extra totals. Format/ABI HEAD/BASE/Difference/OpenAPI Check generation pass after a precise SA1513 whitespace fix. CI actual extracted package fresh/setup/authentication/restart/library/scan/play passed before upload.
- **Additional actual-package verification:** downloaded 835 package's server and compiled web (no Vite) passed creator browsing/escaped description/playback, persistent metadata admin settings/corrections/lock/repair plus viewer denials, existing movie play/progress, full synthetic DVR complete/remove/rescan/delete, and active-stop/retained partial playback (5 workflows). Original fixture settings/policy/item restored; temporary viewers removed; no pending metadata/DVR recovery snapshot. Full report docs/verification-node24-and-metadata.md. Physical Mint/GPU/tuner/long-duration/codec-matrix certification not claimed.
- **Unfinished work:** full Phase 1.4/master specification remains incomplete. Managed yt-dlp importer/queue/scheduling/dependency health/recovery, complete online provider/relationships/caching, bulk/ambiguity/artwork/duplicates, linked profiles and other retained platform requirements remain mandatory. Local sidecar + selected-item admin are implemented subsets. Compiled legacy Web is still separately attached as documented. No unfinished application changes at this checkpoint.
- **OpenAPI rollout limitation:** the fixed workflow uses built-in github.token instead of missing JF_BOT_TOKEN. workflow_run executes from default main; old main email failures can continue until reviewed merge. No auto-merge and no claim that the future main comment workflow has already passed. Release/version-bump execution not invoked; its inherited missing build.yaml remains separate work.
- **Next exact development task:** one complete, bounded safe importer milestone, reusing existing configuration/library/admin/provider foundations: define authorized single-source job lifecycle and destination/source policy, managed pinned yt-dlp health/version, persistent queue/history/recovery, safe subprocess arguments/egress/redirect/path/resource controls, scan on success, and owned-fixture/authorization/security tests before broader playlists/schedules. Do not restart metadata architecture or present unfinished importing as production-ready. Assess execution resources before beginning this larger security-sensitive task.
- **Usage status:** **Not accessible**; available-tool discovery exposes no five-hour allowance/reset meter. No automatic reset/background continuation is promised.
- **Recovery:** read permanent requirements, import metadata spec, progress/parity/roadmap and Phase1.4 doc; inspect git status/log/remote. Fetch and fast-forward only this existing branch when clean. Source/build is recoverable from exact 835 commit; run packaging/linux/build-preview.sh on a clean checkout with Node24/.NET10 or existing read-only Actions workflow. Owner install/checklist is INSTALL_TEST_BUILD.md and docs/TEST_BUILD_CHECKLIST.md. Temporary caches/generated duplicate test/extraction outputs were removed only for disk headroom; source, installed dependencies, preview archives and fixture evidence retained. No production/Coon access, official Jellyfin modifications, deployment or main merge.

## Current milestone — native Metadata Management

- **Last completed feature:** native administrator local reader enable/order, source ID/type inspection, selected-item title/description correction, lock, explicit confirmed repair and asynchronous reload. Uses inherited endpoints and fresh complete item/library DTOs to retain unrelated fields, provider IDs, ratings and existing field locks. Backend authorization remains independent of UI gating.
- **Latest published implementation before this milestone:** `9a89e239d4f9d2852b5d938fb1c204f8c5f58d70` (native retained sidecars) on `development/castlumora-web-1.3`, open PR #3. This dashboard checkpoint follows it; exact published SHA is recorded after publication. Node24/auth checkpoint is `dc26e0aedb53d105ad721b55972117082ea8eb55`.
- **Verification:** frontend typecheck/lint/build and 56 tests pass. Actual Chromium/isolated backend reader disable/re-enable/order persists after reload; corrections/lock save; unlocked repair restores source; provider IDs retained. Ordinary viewer directly denied item-update, refresh and library-options writes. Original fixture settings/item restored and synthetic viewer removed. Browser label issues (wrapped select/textarea) corrected with explicit label associations and rerun successfully. No new backend code in this UI increment; previous provider suite 532/532 remains recorded. See docs/CASTLUMORA_METADATA_MANAGEMENT.md.
- **Preserved preview:** creator source 9a89e239d4 completed GitHub build/extracted fresh/restart/library/playback checks; download https://github.com/dangerousnick/Castlumora/actions/runs/42738590880/artifacts/13727578752 . It precedes this dashboard. New complete source push generates the updated preview; verify actual upload/download/checksum before claiming delivery. This remains an intermediate preview, not full Phase 1.4 acceptance.
- **Unfinished:** managed yt-dlp importer/queue, online provider/enrichment/cache/relationships, ambiguity/bulk/artwork/duplicates/source overrides, account/profile separation and remaining platform requirements. This selected-item dashboard is not full metadata parity. Legacy compiled Web remains separately installable, not bundled.
- **Next exact task:** safe first-class importer foundation. Inspect current Castlumora configuration/job APIs; define one bounded executable-health/authorized-single-source queue milestone, validated library destinations/source permissions, SSRF/redirect/path/argument/resource protections and persistent recovery. Pin/verify managed yt-dlp dependency, no shell command construction or unauthorized access bypass. Test owned local legitimate fixtures plus malicious sources/permissions before building broader playlist/schedule/admin features. Preserve this native metadata reader/dashboard rather than restarting architecture.
- **Usage status:** Not accessible. No automatic reset resume. **Recovery:** inspect Git status/log/remote, read all permanent requirements, preserve changes before fetch/fast-forward on this branch. Current metadata browser helper stores private recovery JSON if interrupted; cleanup-metadata-fixture.py restores only marked owned loopback data. Do not target production, merge main or regenerate finished functionality. Before another feature, finish the updated preview/checkpoint and assess available execution capacity.

## Latest recoverable milestone — native retained creator sidecars

- **Last completed feature:** built-in offline YouTube .info.json metadata reader for explicitly generic Video files, strict stable/source/filename identity, channel IDs, source titles/date/escaped description, bounded/cancellable input, reader disable/order options and inherited manual locks. Browse/Search/My List now include generic Video; details use a formatting-only sanitizer without network-bearing elements/attributes. No downloader/plugin or silent Movie/Episode reclassification.
- **Branch:** `development/castlumora-web-1.3`, open PR #3 targeting main. Latest published prior milestone: `dc26e0aedb53d105ad721b55972117082ea8eb55` (Node24/authentication). This creator implementation checkpoint follows it; exact published hash is recorded after commit/push. Preserve all existing history/previews.
- **Verification:** Release provider build zero warnings/errors; all 532 provider tests passed, including 17 new identity/unsafe/malformed/disabled/oversize/symlink/cancellation cases. Node24 frontend typecheck/lint/build and 50 tests passed (two new sanitizer cases). Actual backend fixture identified a previously scanned video after adding its sidecar; explicit repair, global/title-field locks, unlock and conflicting filename checks passed. Actual Chromium creator browse/details/escaped description/playback plus existing movie authenticate/browse/play/progress passed (2/2). No external source fetch, real YouTube media or production data used. Normal refresh retains existing title; explicit replacement repair was tested without changing global merge semantics. See docs/CASTLUMORA_CREATOR_SIDECAR.md.
- **Node24 preview verified:** run 42736742687 succeeded including clean compile and extracted-package fresh/restart/library/playback workflows; artifact https://github.com/dangerousnick/Castlumora/actions/runs/42736742687/artifacts/13727097335 . It predates this creator provider. The updated branch preview will include this milestone; do not call any intermediate preview full Phase 1.4 completion.
- **Unfinished:** complete online provider/language/playlist/channel relationships/cache, metadata ambiguity/bulk repair/duplicate UI, managed yt-dlp executable/queue/recovery/schedules/admin workflow, linked accounts/profiles, remaining categories/layout/skipper/personalization/admin/creator/business/cloud/native-client work remain mandatory. Ordinary film/TV matching was not changed. Legacy compiled Web is still a documented separate dependency, not falsely bundled.
- **Next exact milestone:** native administrator Metadata Management: inspect existing item metadata editor/refresh/library options APIs; implement authorized provider enable/order and selected-item repair/lock forms with clear replacement confirmation, expose reliable source IDs and ambiguity/unsupported-type status, test permissions and actual refresh/locks. Publish that coherent increment before beginning managed import queue. Do not recreate this local reader. Preserve full requirements in permanent docs.
- **Usage status:** Not accessible; no verified account allowance/reset or automatic resume mechanism. **Recovery:** read all permanent specs, git status/log and remote branch; preserve changes before fetch/fast-forward. The synthetic creator helper supports explicit --resume only with its marker and verifies actual loopback ProgramDataPath. Generated provider-test bin outputs were removed after 532 passes to preserve server startup disk headroom; rebuild from source if needed. Source, logs, fixture/results and published archives retained. No Coon/official Jellyfin/production changes or merge.

## Current milestone — Node.js 24 LTS and OpenAPI reporting

- **Completed:** repository `.nvmrc` selects 24; frontend/lockfile require Node >=24 <25; preview setup-node reads that file. Existing official checkout/setup/artifact actions were already Node24-compatible. Replaced Node20 OpenAPI comment action and Node12 release helpers with pinned Node24 github-script and pinned digest-backed Docker yq. No application/server code or preview artifact was discarded.
- **Authentication fix:** OpenAPI Report uses the built-in github.token instead of missing JF_BOT_TOKEN, with job-scoped PR write and artifact/content read. Metadata discovery has PR-read permission, skips non-PR runs and emits no fake null PR. Posting skips closed/superseded PRs, updates only its own marked bot comment, bounds untrusted artifact text and never executes it. The privileged job checks out only the trusted workflow SHA, never PR code.
- **Verification:** Node v24.22.1 clean npm ci, typecheck, lint, 48 frontend tests and production build passed. All workflow files passed actionlint v1.7.12. Five report-handler tests passed (create/update, stale/closed, invalid PR/oversized artifact, bounded text); preview packaging runs those tests before compiling. Actual GitHub workflow validation/build evidence is recorded after publication. Release publishing/version bump was not triggered; its existing upstream build.yaml dependency is absent in this fork and release redesign remains separate unfinished work.
- **Important rollout limit:** workflow_run executes from the default branch. The old main workflow can still emit missing-bot-token failure emails until this fix is reviewed/merged there. Do not merge automatically or claim that the new default-branch reporting run has already passed. Branch workflow and handler validation are distinct from that future live run.
- **Latest published application:** `56e8e83b5a6e0743fab040e63cb428d30fa33bca` on `development/castlumora-web-1.3`, open PR #3. Active-stop preview run 42709255805 succeeded; artifact https://github.com/dangerousnick/Castlumora/actions/runs/42709255805/artifacts/13715686059 remains available. This Node/CI checkpoint follows it; inspect git log for exact SHA. New previews are intermediate, not Phase 1.4 completion.
- **Next exact development task:** resume the native source-aware local creator metadata/provider increment described below, retaining all importer/profile/admin requirements. No implementation has been regenerated.
- **Usage:** Not accessible. **Recovery:** read permanent requirements/progress; inspect Git status and remote SHA before fast-forwarding this existing branch. Preserve unfinished work. No production/Coon access, deployment, main merge or automatic background resume.

## Latest milestone — active DVR stop and partial playback

- **Completed:** InProgress single timers offer Stop recording with retained-media/empty-file confirmation; future and series cancellation remain distinct. Stop request wording does not imply synchronous process completion.
- **Latest published implementation before this checkpoint:** `c9a8be35dbb2e9f52e27d496c4f068e8b2b9bb66`; current active-stop commit follows it on `development/castlumora-web-1.3` / open PR #3. Inspect git log for its exact SHA.
- **Verification:** frontend typecheck/lint/build and 48 tests passed. Separate real Chromium active-stop case passed (1.2 minutes): schedule actual M3U/XMLTV programme, observe InProgress and nonempty output, deny ordinary-viewer stop, confirm administrator stop before scheduled end, verify persisted Completed status/retained file and play partial recording. Original source survives; saved fixture settings/policy restored and synthetic viewer removed. First run's five-second file-flush assertion was too short; bounded 45-second wait passed. Previously completed DVR case and 81 backend tests remain recorded above/below; backend unchanged by this UI increment.
- **Intermediate preview:** c9a8be35db build succeeded, including extracted-package smoke tests; uploaded artifact https://github.com/dangerousnick/Castlumora/actions/runs/42708373873/artifacts/13715621126 . This precedes active-stop wording and is not a final Phase 1.4 build. Earlier preview remains preserved. New source commits trigger newer previews.
- **Unfinished:** quotas, channel groups, real tuners/GPU, long-running DVR and broad platform requirements remain. No production-readiness claim.
- **Next exact task:** native source-aware creator metadata increment: inspect existing provider/merge/lock interfaces; add bounded local YouTube .info.json metadata for explicitly generic Video items with reliable IDs, no network/path use from untrusted sidecars and no fuzzy movie matching; test valid/conflicting/malformed/unsafe inputs, real existing-file refresh and manual metadata locks. Then extend provider/admin/import queue in further complete increments.
- **Usage:** Not accessible. **Recovery:** fetch and inspect this existing branch; preserve working changes; read permanent requirements. No automatic merge/deployment/background resume. Produce an updated final preview after remaining completed milestones; an intermediate artifact is not project completion.

## Current checkpoint — native DVR recorded-media lifecycle (Phase 1.4)

- **Last completed milestone:** native recording refresh and distinct library-only removal/permanent file deletion; recordings API retains the requested server-calculated CanDelete field. Expected FFmpeg stop/duration exits follow the normal cancellation path after the owned process exits; unsolicited failures remain errors.
- **Branch:** `development/castlumora-web-1.3`, PR #3 targeting main. Latest published implementation commit: `c9a8be35dbb2e9f52e27d496c4f068e8b2b9bb66`. Active-stop work follows this checkpoint.
- **Verification:** Frontend typecheck/lint/production build and 48/48 tests passed. Release LiveTV library build passed with zero warnings/errors; LiveTV suite 81/81 passed on Linux. Real Chromium workflow passed against an isolated actual backend and local owned M3U/XMLTV media: schedule, cancel future recording, record to completion, play output, deny ordinary-viewer mutations, retain file on library-only removal, rediscover on scan, physically delete with per-item permission, preserve original source. Fixture settings/policy restored afterwards. Initial failures exposed an unindexed new library, XMLTV cache and stripped permission field; corrected and rerun. See docs/CASTLUMORA_DVR.md.
- **Unfinished:** active-recording stop needs its own browser coverage; real tuners/GPU/long-duration recording, channel groups and DVR storage quotas remain unverified or unimplemented. The master specification remains incomplete; metadata provider/importer, account/profile separation, personalization and extended platform work remain required.
- **Next development task:** add and verify explicit active-recording Stop behavior and partial-file retention using the owned real-time synthetic source; publish before moving to source-aware local creator metadata integration. Preserve all existing preview assets.
- **Usage status:** Not accessible. No reliable remaining five-hour/reset data is exposed.
- **Recovery:** inspect status/log before fetching and fast-forwarding this branch; preserve unfinished work. Use the guarded DVR fixture helpers only on the explicitly marked disposable loopback root. Do not target production. Existing read-only Actions workflow generates intermediate previews after source pushes; a preview does not complete Phase 1.4. Continue milestones and rebuild the final preview from the final tested commit. No main merge or deployment.

## Latest recoverable checkpoint — Phase 1.4 runnable preview delivered

- **Last completed milestone:** self-contained Linux x64 package with compiled integrated Castlumora Web, native fresh setup, isolated launcher/temp storage, licenses/install checklist and read-only GitHub Actions build/extract/browser/artifact delivery.
- **Latest published tested build commit:** `cc59df91a8c06364d6ca1e6944ba938031754bd7`. Documentation-only checkpoints may follow this immutable application/build commit; inspect `git log` and remote branch before continuing.
- **Branch / review:** `development/castlumora-web-1.3`, PR https://github.com/dangerousnick/Castlumora/pull/3, targets main, not merged.
- **Download:** https://github.com/dangerousnick/Castlumora/actions/runs/42702481467/artifacts/13712785582 (GitHub sign-in; retained 30 days). Run: https://github.com/dangerousnick/Castlumora/actions/runs/42702481467 . Outer ZIP contains archive/checksum/manifest/install/checklist/CI report.
- **Archive:** `Castlumora-1.0-dev-cc59df91a8c0-linux-x64.tar.gz`; SHA-256 `57dd13e5ad925d3f9c9baa0bfb0779f505f50e3c17e4a63dca984f5e709ff89a`.
- **Verification:** local frontend 44 tests/typecheck/lint/build; Release publish; local extracted-package fresh setup/security/logout and restart/library create/scan/play; all 13 broader package browser workflows across full/targeted runs. GitHub Ubuntu 24.04 workflow succeeded at every step, independently rebuilt/extracted and passed both fresh and restart/library/playback browser cases before artifact upload. Downloaded through the connected GitHub app, inspected safe ZIP paths, verified inner checksum and exact manifest. Full report: docs/verification-preview-1.4.md. Windows/Mint physical hardware, GPUs, real tuners and completed DVR lifecycle are not certified. Prior backend tests are explicitly historical, not falsely rerun.
- **Unfinished work:** no unfinished application changes; master specification remains incomplete. Mandatory yt-dlp importer and independent creator/YouTube metadata are absent. Full DVR lifecycle/storage, linked profiles/presets, personalization/notifications, extended creator/business/cloud/offline/plugin/native clients and advanced admin gaps remain tracked.
- **Next single task:** Priority A DVR lifecycle. Add a short authorized synthetic guide program on the marked loopback fixture, snapshot/restore its own guide/options, schedule through native UI, verify actual recording completion/file and playback, cancellation/removal semantics and permissions. Existing DefaultLiveTvService copies guide dates into new timers and only updates padding on single-timer edits. Existing DELETE LiveTv/Recordings removes the library entry with DeleteFileLocation=false; do not claim physical deletion or change that compatibility behavior without explicit design/tests. After DVR, continue account/profile/admin stabilization and mandatory source-aware metadata provider then safe importer, per permanent requirements.
- **Usage status:** **Not accessible**; no verified five-hour allowance/reset information is exposed. No background continuation or scheduled reset resume is promised.
- **Recovery:** read all five permanent specifications/progress files plus docs/CASTLUMORA_PHASE_1_4.md; inspect `git status` and preserve any new uncommitted work. Fetch origin, use this existing branch, and fast-forward only if clean (`git merge --ff-only origin/development/castlumora-web-1.3`). Never hard-reset completed work. Rebuild with `packaging/linux/build-preview.sh` from a clean exact commit, or rerun the existing successful Actions run to refresh an expired artifact. INSTALL_TEST_BUILD.md covers owner download/extraction/start, no Node/SDK needed on the test computer.

The workspace uploads.github.com credential returned 401; the empty unpublished draft was removed. Automatic approval review rejected a broad persistent release-write workflow; delivery was completed through a narrower contents-read-only artifact workflow instead. Workspace CLI Azure artifact download returned 403, but connected GitHub artifact download succeeded and the retrieved bytes passed checksum/manifest validation. These are documented tooling-route failures, not claims of missing published assets. No main merge, official Jellyfin modification, Coon Media access or production deployment occurred.

## Current continuation status

The master specification is **not complete**. Tested continuation work now includes real HLS/track/quality controls, permission-filtered category pagination/folder names, layout revision rollback, native administration, Live TV/DVR schedules/channel switching the native reviewed intro/outro skipper, registration/invitation workflows, native music/private playlists, and native photo/book/collection workflows. Historical foundation limits below are superseded by the continuation checkpoints at the end of this file. See those checkpoints for the current exact next steps and unverified features.

## Published milestones

1. `b30674348ab61176bc1193a819522e11177b5e3f`: independent React/TypeScript frontend, real API adapter, primary screens/player, permanent requirements and progress checkpoint; frontend checks and 15 tests passed.
2. `73b7960fdae6bb48af4f34722249cc3da743e480`: server presentation persistence, categories/preferences, editor, regression tests, roadmap/parity/spec/API docs and screenshots. API regression suite 164 passed/3 skipped; isolated API/executable builds passed. HTTP test execution was still pending at this checkpoint.
3. `7204fcfa248a8b1c9cbcb1aa131613dbe7b2411e`: HTTP test cancellation-token fix, verified authorization, legacy-root fallback and 16 passing frontend tests.
4. `f46230b5d738a227f286661e7805fbb640b0dbe9`: actual-server playback verification results, final reports/screenshots and bundled GPL/dependency notices.
5. Documentation merge incorporates concurrent main commit `7fae5086785978ac081113d364c5655e3f0b3a17`, preserving both complete requirement documents and explicitly tracking the built-in intro/outro skipper as planned. No runtime source changed in this merge. PR: https://github.com/dangerousnick/Castlumora/pull/3.

Continuation publications:

- `5ac61c1159eec98292a43d40f078f2f52ba09858`: built-in reviewed intro/outro detection and profile skip modes.
- `45b81ca2dcc38fe9801f71a6e847a259b0359f7d`: tested initially-disabled account provisioning.
- `559bcb95566bf3f8777a2958ae47a6714cb15e04`: invitation registration and pairing.
- `6206b4e5c4f7bfe102135bf57892baaad068ba6c`: native music/background queue/private playlists.

- `e1786a8f7bedd074e493e43d5e2e2fdc2961e2ac`: real HLS and track/quality/source/chapter/seek controls.
- `8541ce71c7568c1b1de7d2538fd26c4740405745`: permission-scoped category pages, layout rollback and native administration.
- `9f4bcb6be4091b2e08a3670d21d7abdd67346f08`: native guide, DVR schedules and live channel switching.

Publishing uses the connected GitHub API because the terminal GH_TOKEN is invalid. Connector-created commits have different metadata/SHAs from local checkpoints; each uploaded Git tree is verified against the corresponding local committed tree. The code is not regenerated during publication. Local/remote history is synchronized after publication.

## Implemented and verified foundation

- Independent dark purple responsive web client with existing castle branding, simple navigation, Surprise Me, keyboard/focus styles, loading/errors, no empty/identical home rows, and real media imagery when available.
- Server connection remembered; existing login/logout/public-profile suggestions; profile switching requires credentials. Tokens are tab-scoped, passwords never persisted. Existing server permissions remain authoritative.
- Actual permitted library retrieval, movie/series search and paging, details, seasons/episodes; separate Resume/NextUp rows and watchlist.
- Conservative direct MP4 browser profile, server-negotiated HLS foundation, audio/subtitle selection, start/progress/pause/stop reporting and cleanup of negotiated sessions only.
- Live channel/three-hour program guide screens and channel play links; existing Live TV/DVR backend retained. No real tuner configured or verified here.
- Additive schema v1 server configuration; administrator-only validated drafts/publishing with optimistic revisions, profile filtering and device overrides. Atomic JSON storage beneath server configuration; no database migration.
- Reusable category rules (metadata, folder IDs, manual inclusion/exclusion, types), no media movement/duplication; ordered home/category placement and profile visibility.
- Working administrator overview and layout editor: navigation/row ordering, theme colors, category/profile rules, device sizing/safe zones, preview, advanced JSON editing, separate save/publish.
- Existing-client root links preserve advanced administration and other media interfaces without assuming a particular legacy router version. Existing compatible Web distribution must remain installed and may require separate sign-in; choose Dashboard/Live TV there.
- Complete product spec, parity inventory, roadmap, frontend instructions, adapter/schema/persistence documentation and verification report. GPL/upstream copyright/contributors preserved; static distribution includes runtime license notices.

## Verification completed

- Frontend typecheck, lint, production build pass. 16 unit/API/component tests pass.
- Chromium isolated API/media fixtures: 2 pass; mobile overflow checked. Screenshots are labeled fixture screenshots.
- API regression suite: 164 pass, 3 Windows-specific skips, including 7 new store/policy cases.
- Full server integration suite: 130 pass, 3 existing skips, including real HTTP administrator/ordinary-user authorization, revision conflict and preference-isolation test.
- Full Linux server/reference build: passed, 0 errors, 215 warnings in inherited code; no warnings in new presentation files. Isolated API and executable builds also passed with 0 warnings/errors.
- Actual disposable loopback server and synthetic H264/AAC movie: Chromium login, browse, details, real playback and successful start/progress/stop requests pass (1 test). Normal FFmpeg 7.1.5 validation/probing used. No production data/server involved.
- LICENSE and CONTRIBUTORS.md byte-identical to main; diff check passes. See docs/verification-web-1.3.md for commands and limits.

## Known limits and unfinished work

- This is a vertical slice, not full frontend parity. Music/photos/books, advanced library/metadata/playlist controls, tuner/XMLTV/DVR settings, encoding/hardware/plugins/networking/tasks/backups and full account/device administration use the existing client/dashboard.
- Live tuner playback/channel transitions/DVR, hardware acceleration, cross-browser codecs, real audio/subtitle variants, HLS seek/resume timelines and long-running sessions need fixture/hardware testing. HLS code exists; passing direct MP4 playback does not certify transcoding.
- Category output is bounded to 48 items; full indexing/pagination and folder-name patterns remain future work. Banners/visibility/device row ordering currently use the advanced schema editor. Navigation icons/white-label artwork picker and revision history/rollback are not implemented.
- Native built-in intro/outro detection and skip/auto-skip controls, native TV/mobile renderers, plugin UI registry, recommendations/interests/follows/notifications, invitations/signup, presets, optional $0 resource tiers/payments, creator/OBS, private cloud/offline are roadmap features, not completed screens or contracts.
- Player/HLS lazy chunk exceeds Vite's 500 kB advisory (~602 kB minified/~189 kB gzip). Dependencies are locked; evaluate bundle optimization without removing required audio/subtitle support.
- Persistence lock is in-process; shared config storage across multiple server processes is unsupported. Include castlumora config files in backup/restore verification.

## Exact next steps for a new task

1. Fetch and check out this existing development branch. Read this file, the permanent requirements, parity inventory and verification report. Inspect current HEAD/PR before changing anything; do not repeat Phase 1 or rebuild the client from scratch.
2. Review and test this PR before any merge. No automatic merge/deployment is authorized. Keep the existing Web fallback.
3. Run `npm ci`, typecheck/lint/test/build under Castlumora.Web. For browser tests run Vite on loopback and follow documented synthetic/disposable-server instructions. Never use Coon Media.
4. Prioritize real HLS/remux/transcode seek/resume, multiple track and Live TV fixtures, then category pagination/folder-name rules and admin editor usability. Add tests for each supported claim and update parity/report accordingly.
5. Build future features in tested milestones on this branch or a separately authorized follow-up branch, commit/publish regularly, and update this file at each checkpoint. Preserve backend namespaces, APIs, databases, plugins, licenses and upstream history.

## Active continuation: complete the master specification

The owner requested completion beyond the initial vertical slice. Continue the existing branch and PR; the retained full specification remains authoritative. Do not call the platform complete while parity rows or product requirements remain unfinished. Publish tested milestones and update this checkpoint throughout.

Current milestone: real HLS/remux/seek/resume and track/quality controls, then server-backed category pagination/layout revision history, full administration and Live TV/DVR controls, built-in skipper, and remaining account/personalization/platform requirements. Hardware/native-device and real-tuner claims still require their corresponding fixtures/devices. No Coon Media access, automatic merge or deployment.

### Continuation checkpoint: playback controls

Real HLS, seeking, bitrate negotiation, English/French audio switching and external subtitle loading now pass against the disposable server using a 90-second synthetic multi-track/chapter fixture. Added quality/source/chapter/absolute-timeline/next-episode controls and corrected exact-zero resume handling. See docs/verification-web-continuation.md. Layout history/rollback and server-side paginated folder-name category rules are being implemented next; those backend changes are not yet marked verified.

### Continuation checkpoint: categories, history and native administration

Implemented permission-filtered server-side category pages (metadata, physical/configured folder names, manual OR selection, exclusions and totals). Explicit selections retain allowed-library roots. Added administrator-only published revision history and draft restoration with optimistic concurrency, plus featured/artwork title pickers and category controls.

Added native administration sections for library creation/options/scans, users/policies/passwords/devices, encoding, general/network settings, tasks/triggers/logs, backup actions, plugins/repositories/catalog and tuner/XMLTV/channel mapping. Forms preserve unknown inherited configuration fields and expose advanced JSON; existing dashboard remains available. These screens are implemented, but not every mutating operation is verified: real tuner/DVR, hardware settings, plugin installation, backup restoration and restart/shutdown need their own isolated fixtures.

Verification: frontend typecheck/lint/build and 22 tests pass; 2 fixture browser tests include publish/restore isolation. Three real-server browser cases cover direct playback, HLS/tracks and persisted encoding settings plus administration navigation. API tests: 168 pass, 3 Windows-specific skips. Extended presentation HTTP authorization test passes. Actual disposable-server category checks pass for genre/folder/manual matching, exclusions, pagination and restricted-library permissions. No production data or server involved.

This checkpoint supersedes earlier limits for category pagination/folder names, revision history and administration links. Remaining work includes complete DVR/Live TV workflows, other media types, native skipper, registration/invites, personalization/notifications, presets/resource plans, creator/cloud/offline/native-client/plugin UI requirements and corresponding verification. The complete specification is **not finished**.

Next: publish this tested checkpoint on the existing branch, then implement and test native Live TV/DVR workflows with synthetic M3U/XMLTV; proceed through remaining requirements without replacing existing work. Keep publishing tested milestones; no automatic merge/deployment.

### Continuation checkpoint: native Live TV and DVR

Added six-hour guide date/channel selection and program pagination, recording options using server defaults, single/series schedule creation and editing, cancellation, recordings pagination/removal controls and live player channel switching. Existing server permissions remain authoritative; management controls respect the profile policy. Fixed new-timer defaults carrying a derived ID so creation uses POST /Timers rather than attempting to update a nonexistent timer.

Verified on the disposable loopback server with synthetic two-channel M3U and six-program-per-channel XMLTV: create single schedule, persist/edit padding, cancel; create/cancel series rule; play Castle One, switch to Castle Two and confirm own live-stream cleanup on switch/exit. Two actual-server Chromium tests pass. Frontend 25 tests, typecheck/lint/build pass. Latest normal API analyzer build: 0 warnings/errors. No production deployment or access.

Recording completion/files/deletion, long-duration DVR/timeshift, physical tuners, large channel catalogs/groups and hardware compatibility still need verification/work. browser/setup-live-fixture.py retains a guarded fixture setup helper (loopback, explicit disposable marker, refuses existing tuner/providers). Next: complete recording lifecycle fixture and the retained native skipper requirements, then other media/account/platform features. Full specification remains unfinished; do not merge automatically.

### Continuation checkpoint: native skipper

Implemented a built-in optional repeated-audio provider using configured FFmpeg, same-season corroboration and silence/flat-fingerprint guards. Detection yields review-only suggestions. Added central revisioned review state, manual correction/approval/removal, protected post-credit boundary, file-signature checks and profile default/manual/automatic/disabled modes. The existing scheduled extraction task includes the native provider; bounded single-process decoding defers during playback and kills only its own process on cancellation. Existing providers and the MediaSegments API remain intact. Deferred session-manager resolution avoids a startup dependency cycle caught by actual-server testing.

Native admin review/settings and player controls are implemented. Actual synthetic FFmpeg detection found intro 10–40s/outro 145–169.5s; suggestions were absent from central intervals until approval, and protected post-credit content remained outside the outro. Chromium manual/automatic/disabled playback modes pass. Guarded generator/verifier scripts are retained. See docs/CASTLUMORA_SKIPPER.md for exact limits, contracts and reproduction.

Latest verified checks before this checkpoint: 28 frontend tests; typecheck/lint/build; 177 API tests with 3 Windows-only skips; normal API build 0 warnings/errors; isolated executable build 0 errors; HTTP authorization/profile-isolation test passes. Complete integration suite: 130 pass, 3 existing skips. Actual-server browser cases for movie/HLS, administration, DVR, live transitions and native skipping pass; administrator review UI also passes. Both editor/mobile fixture browser cases pass. Native TV/mobile renderers/device behavior and broad real-series detection accuracy remain unverified. Stale sessions from abruptly terminated clients conservatively defer analysis until cleared. Replacing media requires existing metadata/segment refresh to update central intervals.

Next after publishing this milestone: complete remaining music/photos/books/collection/playlist and recording lifecycle coverage; invitations/registration/profile/preset requirements; personalization and notification workflows; optional quotas/business/creator/cloud/offline/plugin/native-platform requirements. Preserve this work and the full requirements; do not claim complete master-spec parity or merge/deploy automatically.

### Continuation checkpoint: disabled account provisioning

Added an additive IUserManager overload for creating an initially disabled account. The existing method retains its behavior; custom managers without the new capability fail closed for disabled provisioning. The disabled permission is persisted before account creation is published. This supports safe public registration without a blank-password/initial-policy exposure window.

Verification: all 64 user-manager tests pass, including persisted disabled state and unchanged legacy creation. Full Linux server/reference build with normal analyzers passes: 0 errors, the same 215 inherited warnings. Registration/invitation services, protected/public endpoints and frontend forms are being developed next; their working-tree code is not yet marked published or verified end to end. Continue that work rather than recreating the existing application.

### Continuation checkpoint: registration and invitations

Implemented admin-enabled closed/open/invite-only registration, expiring/revocable invitation links with successful-use caps, server-owned non-administrator policy snapshots, explicit library permission pickers, private audit/recovery and bounded signup attempts. Raw tokens are returned once and stored as digests; passwords stay in the existing account manager. Durable reservations and disabled provisioning prevent quota races and blank-password/policy exposure. Schedule templates are rebound to the new account. The public endpoint ignores supplied policy/administrator fields. Added native Users deletion (with confirmation/self guard) for normal account management/recovery.

The signup frontend confirms the target server, strips the invitation from the URL, submits secrets only in the body and requires ordinary sign-in after creation. Paired addresses reconnect automatically without persisting passwords or bypassing authentication.

Verified: 32 frontend tests plus typecheck/lint/build; 192 API tests pass with 3 Windows-only skips; all 64 user-manager tests pass. Full normal Linux server/reference build: 0 errors, the same 215 inherited warnings. Latest isolated normal API build: 0 warnings/errors. The real HTTP signup test passes, including blank-password denial, policy isolation, role boundaries, caps and throttling. Actual Chromium invitation → restricted signup → normal sign-in → remembered pairing passes and restores its disposable configuration. Full integration suite: 131 pass, 3 existing skips. All 8 actual-server browser cases and both editor/mobile fixture cases pass. Details and exact commands: docs/CASTLUMORA_REGISTRATION.md.

This does not complete the master specification. Next: finish native music/photos/books/collections/playlists and recording lifecycle coverage, then linked account/viewing-profile presets, personalization/notifications and optional quotas/creator/cloud/offline/plugin/native-platform requirements. Continue from these commits; keep the full requirements, legacy functionality, GPL attribution and castle assets. Do not merge/deploy automatically or contact Coon Media.

### Continuation checkpoint: native music and playlists

Added native albums/artists/tracks/search/paging, artist/album details, a queue that survives navigation, audio-only negotiation with direct/MP3-compatible playback, seeking, repeat/shuffle, persistent queue volume and own-session cleanup/progress reporting. Video playback clears background music. Added private playlist creation, permission-aware editing, track search/add, server entry-ID reorder/removal, rename without sharing-field replacement and deletion while retaining media.

Frontend typecheck/lint/build and 37 tests pass. Actual disposable-server music workflow passes: artist → album, playback/seek/background navigation/next track, compatible MP3, pause/stop and complete private playlist editing. Retained a guarded synthetic tagged-MP3 generator/indexer. Video coexistence passes: starting the movie stops only the background music session. All 10 actual-server browser cases and both editor/mobile fixture cases pass. See docs/CASTLUMORA_MUSIC.md.

Remaining: photo/book/collection screens and recording lifecycle, richer music metadata/lyrics/sharing/scale coverage, linked profile/preset and personalization requirements, optional resource/creator/cloud/offline/plugin/native-platform work. Continue existing code; no main merge or production/Coon Media deployment.


### Continuation checkpoint: photos, books and collections

Added native photo/album shelves, image navigation/zoom/rotation/slideshow; books and authenticated PDF/EPUB reading; profile-specific atomic reading positions/themes/font preferences; native collection creation, media add/remove and deletion retaining media. Fixed native administrator photo-library creation to use supported `homevideos` plus `EnablePhotos`. EPUB is sanitized before rendering, scripting disabled, archive sizes/path traversal bounded and external metadata resources rejected. PDF uses a bundled local worker. Extended distributed dependency notices without changing GPL/upstream attribution.

Frontend typecheck/lint/build and 41 tests pass. Normal API/executable builds pass with zero warnings/errors. API suite: 195 pass, 3 Windows-specific skips, including inaccessible/non-book denial, claims-owned persistence and submitted foreign profile-ID isolation. All three actual-server shelf cases pass on synthetic photos/two-page PDF/two-chapter EPUB and collections; reader positions survive reload and collection deletion retains its movie. See docs/CASTLUMORA_READER_AND_SHELVES.md and browser/setup-shelf-fixture.py. The latter requires loopback, a disposable marker and matching actual server data directory. A failed initial synthetic photo library retained its invalid identity when recreated with the same name; the final uniquely named fixture is created with the correct supported type from the start. No source workaround or production data change was needed.

Exact next steps: complete actual DVR recording lifecycle/playback/deletion and duration coverage; finish linked account/viewing-profile and editable kids/adult presets; implement interests/follows/local recommendations and profile notifications; then editable onboarding/resource presets, optional $0 quotas, creator/OBS, private cloud/offline, declarative plugin UI and native device requirements from the permanent specification. Rich music lyrics/metadata/sharing, comics/other book conversion, annotations/search/offline reading and broader real-library/device compatibility remain unfinished. Keep the existing implementation and castle assets, preserve GPL and legacy behavior, publish tested milestones and do not merge/deploy automatically or contact Coon Media.

Full browser regression after this milestone: **15 passed in one run** (2 editor/mobile API fixtures plus 13 actual disposable-server cases covering movie/HLS, administration, DVR schedules, live switching, skipper, signup/pairing, music/private playlists, video/audio coexistence and shelves/reader/collections).


### Runtime validator dependency checkpoint

Published shelf/reader milestone: `07f62f88ff8f3d35bed3dc13ec5443e2c4c1ddda` on the existing development branch, with an exact match between the tested local Git tree and the GitHub tree. PR #3 remains open against main, with no automatic merge or deployment.

A production-dependency audit then identified GHSA-mrhx-c7xc-m3w5 in inherited Zod 3.25.76. Updated to pinned Zod 4.6.7; `partialRecord` preserves the existing optional device-override contract (plain Zod 4 enum records would require all four device keys). Existing schema/default/unknown-reference tests, typecheck/lint/build and all 41 frontend tests pass; `npm audit --omit=dev` reports zero vulnerabilities. Dependency license notice updated. The first browser attempt used a stale Vite prebundle after the dependency major-version change and failed before login; restarting the development server with `npm run dev -- --force` rebuilt its cache. After cache rebuilding, 14 of 15 browser cases passed in the full run; the reader exceeded its five-second cold-module assertion while still loading. The assertion now allows a bounded 20 seconds and the complete PDF/EPUB resume case passes on rerun. All 15 workflows therefore pass across the full/targeted runs; the preceding shelf milestone also passed all 15 in one run. The earlier 195 API tests/3 platform skips remain valid because this dependency update changes only frontend validation.

For the next DVR milestone: extend the guarded loopback XMLTV fixture with a short synthetic program, snapshot/restore only its own guide and Live TV options, record through the native UI, verify recording appearance/playback/deletion and retain the original movie. `src/Jellyfin.LiveTv/DefaultLiveTvService.cs` copies program start/end into a newly created timer and existing single-timer updates only change padding; do not assume posting arbitrary edited dates shortens a guide-backed recording. Existing scheduling tests cover padding/cancel, not completed recording lifecycle. Guard all fixture mutations by marker, loopback URL and actual System/Info.ProgramDataPath. Keep config/credentials outside any served media directory and never target Coon Media.

## Phase 1.4 active milestone: installable Linux development preview

Main documentation through `68fd686d43` was merged without overwriting development files. Retained the Phase 1.4 addendum and mandatory importer/provider requirements. Usage status: **Not accessible** (no reliable account five-hour allowance/reset meter in exposed tools). Current branch remains development/castlumora-web-1.3, PR #3; latest published functional commit at start: `c98ee732eb7a6414ec5e294237b137fd654a6008`.

Completion for the next single task: reproducible Linux x64 server + compiled integrated web archive, fresh setup and isolated launcher, instructions/checklist/licenses/checksum/source SHA; actual extracted-package smoke tests; upload and verify a GitHub prerelease download; publish code and evidence. Do not start the importer or unrelated features before packaging is completed or specifically blocked. Legacy admin gaps and unavailable importer/provider must be disclosed. CLI GitHub authentication now returns 200 and repository push permission; repository Actions-settings read is denied, so prerelease delivery will be checked separately. No Coon Media or production data.

Packaging source checkpoint: native setup integrated, optional legacy-web path isolated, launcher/build/license collection scripts and owner installation/checklist prepared. Frontend typecheck/lint and 44 tests pass, including setup endpoint ordering, no password storage, interruption recovery and integrated reverse-proxy server discovery. Shell syntax/Python compile pass. Actual Release publish, extracted-package smoke and prerelease upload remain pending; do not call this source checkpoint final acceptance. Documentation merge published as `56e5e68f3d`.

Preliminary extracted preview smoke passed fresh setup → administrator login → native admin → logout and confirmed unauthenticated startup writes are denied after setup. It ran with DOTNET_ROOT unset and PATH excluding the SDK. First launch correctly rejected insufficient disk space; removed only re-downloadable NuGet archives, reproducible Debug outputs and duplicate package staging, then normal startup passed without lowering the 2 GiB requirement. Packaging review corrected tar timestamp capture outside the repository, retained framework/font licenses and isolated TMPDIR. Final archive rebuild and full packaged media regression are next; the first archive is not the release deliverable.

Delivery fallback: local final archive `/tmp/castlumora-preview-build-2/Castlumora-1.0-dev-8d591a4d58cc-linux-x64.tar.gz` passed checksum and actual package smoke tests. Workspace release uploads to uploads.github.com fail HTTP 401 while repository publishing/API release creation succeed; a tiny API-host upload returns 404. Draft release ID 439656187 has no assets and is not a usable download. Do not claim delivery yet. Automatic approval review rejected a persistent workflow with release-write permission on PR updates as too broad; replaced it with a contents-read-only workflow limited to PR #3/this development branch (plus explicit manual dispatch), no release publishing, and standard Actions artifact delivery. Actual workflow execution and artifact download verification are next before packaging completion. Usage: Not accessible; no unrelated feature started.

Read-only artifact workflow published successfully as `255b7616d9d53dc7da720db7bb30296d1508b5c9`; its first run started. Updated the install guide to explicitly explain Actions sign-in, outer ZIP extraction, inner archive checksum/extraction and 30-day retention. Enabled cancellation only of superseded preview runs so the corrected guide is bundled without wasting parallel builds. Current application code remains identical to the locally smoke-tested `8d591a4d58cc705de1668fb7dc63a0b013cc35e4`; only delivery/docs/browser harness changes follow it. Final successful run/artifact download verification still pending.

### CI style follow-up

GitHub Format and ABI HEAD on 269d4c5b3909dad3fb1c47491e3a3bd8f50a1b71 both identified SA1513 at LiveTvManager.cs:1267 (missing blank line following the new permission-field guard). Added the required blank line without behavior changes; local targeted formatting passes. Its current preview run 42740145240 completed successfully before this fix was pushed, so it was not canceled. Await the new commit's CI before claiming Format/ABI success; cross-platform full suites were still running at this checkpoint.

### Updated preview packaging checklist

On 8e489b965dd38fd833bede3f4372e18e36594040 GitHub Format, ABI HEAD/BASE/Difference, OpenAPI head/base/report generation and the complete preview build/extracted-package smoke/artifact delivery all passed. Full cross-platform test jobs were still running when recorded. The old default-branch OpenAPI Report comment workflow remains a separate rollout limitation; no automatic merge.

Updated owner install/checklist text to distinguish implemented native local-sidecar/selected-item metadata and synthetic DVR verification from unfinished online provider/importer/quotas. BUILD_INFO now records metadataManagementStage=selected-item-local. This packaging/documentation follow-up starts only after the previous preview finished, preserving earlier artifacts. Verify its exact new source/archive/checksum before delivery.

### Active continuation: integrated authorized single-video workflow (not published)

Latest verified published checkpoint remains
`36cf2b44a01c0f88c6f5c9ec979161790f03d8a1` on
`development/castlumora-web-1.3`; exact-SHA Linux/macOS/shared CI passed after the
pinned native dependency repair. PR3 remains open. The retained publication stash
`49a95fefbab944c6e230a6b1bd1d37ec745ec8db` was applied once; do not reapply it over
this working tree. No main merge or production deployment.

Current changes add real native-inspected atomic library publication, private
provenance/receipts, host-owned queue lifecycle, elevated job APIs and a native
administrator form/history/cancel/retry interface. Eighteen focused publication /
TLS / journal cases pass; frontend typecheck/lint/production build and all60tests
pass. Full-server authorization coverage passed, but the new real import → normal
indexing → exact-byte download test has not passed yet. Do not publish these
changes or claim the single-video milestone complete before that test and the
full mandatory gate pass.

Investigation: media and NFO commit successfully. Initial library scan reports
completed, yet no physical destination is indexed; the monitor notification then
has no indexed ancestor to refresh. Add a queued full scan when the newly created
import folder is not indexed, retaining normal monitoring for existing folders.
Verify automatic indexing and media API delivery without manually scanning from
the test. Diagnostic evidence: `/tmp/castlumora-import-index-diagnostics5.log`.
Normal formatting is being applied repository-wide; final verify/build/tests must
run on the final source. Keep all assertions and production backup safeguards.

Disk: removed only an extracted copy of an already retained packaged preview and
one stale owned downloader-test directory (~350MiB), retaining archives, sources,
Git/stash/history and dependencies. About800MiB became free before formatter work.
Usage status: **Not accessible**. No account allowance or reset time is claimed.

Recovery: inspect `git status`, this section, the existing import queue/runner /
inspection/publication/service, controller and native admin UI; finish the indexing
regression, then run formatting verification and focused backend tests. Commit and
push normally through the full .NET10 Debug/Release/all-backend/Node24 gate, inspect
exact Linux/macOS CI, update PR3, and verify a new downloadable preview. Subsequent
work remains playlists/channels/bulk, resource/retry controls, online creator
metadata and all remaining Phase1.4/full1.0 requirements. Do not recreate existing
features or mistake the earlier internal-only preview for this new workflow.
