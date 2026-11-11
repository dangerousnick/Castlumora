# Platform policy and verification evidence

Owner-approved strategy, 2026-11-11: Linux-first, macOS supported where verified;
Windows compatibility deferred for Phase1.4 and initial stable1.0 acceptance.
All previously agreed1.0 feature requirements remain mandatory. Inherited Windows
code and tests remain, with diagnostic failures visible for later investigation.

| Platform | Release strategy | Actual evidence / remaining verification |
| --- | --- | --- |
| Mint22.3 / Ubuntu-compatible Linux x64 | Primary required target | Ubuntu24.04 CI rebuilds/extracts the self-contained preview and tests setup/authentication, restart, library/scanning/playback and tool installation. Physical Mint installation and GPU/tuner certification remain unverified. |
| Debian / other supported Linux | Required target | Debian13 cloud packaged runtime/browser/media verification exists. Other distributions, native dependencies and architecture coverage require separate certification. |
| Docker / Linux NAS | Required target | Preserve existing container/backend support; new Castlumora integrated-image installation, persistent volumes, permissions and NAS/hardware workflows still need verification. |
| macOS | Required managed build/test checks; runtime support where verified | Debug/Release and all 17 backend projects passed at 8aa77c0cc9. Native ARM64 verified-tool installation, health, TLS/transport and abort tests passed (API 295/3). x64 manifest/universal header verified, but x64 execution and full desktop installation/runtime certification remain pending. No standalone macOS app preview delivered. |
| Windows / Windows Server | Deferred; advisory compatibility CI | Inherited implementation/tests retained. Five abort/file-lock cleanup failures at 0293885c4b remain recorded; they did not recur in the passing advisory 8aa77c0cc9 run after fixture scheduling isolation (API 294/4). No Windows release readiness or complete containment claim. Investigate shared/security implications before exposing jobs. |

Passing CI alone never establishes full OS support. Mint hardware, macOS desktop,
NAS permissions, Docker storage mappings, FFmpeg drivers and supported codecs
must be tested and reported independently. Development previews are not stable1.0.

## CI and branch protection

The existing matrix contexts `run-tests (ubuntu-latest)` and
`run-tests (macos-latest)` remain failure-blocking. The Windows matrix job alone
uses job-level `continue-on-error`; fail-fast remains disabled. All tests still run,
including failures. Windows TRX results upload on success or failure and normal
Actions logs remain available. Formatting, ABI and shared security/API checks are
preserved. Explicit Debug server and Release solution builds run on required hosts.

Repository-level main branch protection could not be inspected: GitHub returned
HTTP403 `Resource not accessible by integration`. Ruleset listing returned an empty
list; this is not proof that branch protection is absent. No settings were changed.
A repository administrator should preserve existing restrictions and ensure these
required test contexts are selected, together with existing formatting/ABI/security
checks; remove Windows from required contexts if present. Do not claim this was
configured without verifying settings. CI jobs remain mandatory regardless of
whether branch-protection administration is accessible.

The pre-push gate runs full local formatting, Debug/Release, all backend tests,
frontend and handler/bundler checks. macOS now prepares its reviewed universal executable under the native
architecture package path and runs the real-tool tests; no managed test is excluded. A Linux workspace cannot claim local macOS execution. Real disk I/O
failures remain fatal, but no production-backup reserve is imposed on test fixtures.

## Separate report-workflow version issue

OpenAPI Report uses `workflow_run` and therefore main's implementation. Main's
legacy comment action still requires missing `JF_BOT_TOKEN`. PR3 already includes
the standard-token, trusted-checkout handler with tests. Applying it on main needs
review; do not automatically merge unfinished Phase1.4. Track the actual failure
separately from required source validation, without hiding it or disabling checks.

## Verified source and delivery

See [verification report](verification-platform-runtime-1.4.md) for exact commits,
required/advisory job outcomes, native ARM64 coverage, verified Linux download and
known limitations. Historical pending entries describe earlier snapshots; a green
advisory Windows run does not change the owner-approved release strategy.
