# Linux/macOS policy and native importer prerequisite verification

Verified application source: **8aa77c0cc9c46f3847907483154a141831503af3**.
Existing branch: development/castlumora-web-1.3; PR #3 remains open.
Platform policy c9129c0aeb, native Mac assets f70bc317ca and native fixture scheduling
8aa77c0cc9 preserve all earlier commits, licenses and Castlumora 1.0 requirements.
Windows support is deferred, not removed. No automatic main merge or deployment.

## Required checks and actual evidence

| Check | Result |
| --- | --- |
| Full local .NET 10 formatting | 2,418 files, zero changes; no workspace-load warnings |
| Local Debug server / Release solution | Passed, zero warnings/errors |
| All local backend projects | 17 projects; 4,365 passed, 19 existing platform skips |
| Local frontend on Node 24 | Typecheck/lint/build and 58 tests passed |
| Local enforcement / bundler / report handler | 9 / 6 / 5 cases passed |
| Required Linux CI | Debug/Release and full test job passed |
| Required macOS CI | Debug/Release and 17 projects passed: 4,365 passed, 19 existing skips |
| Native macOS ARM64 | Verified osx-arm64 asset installed/executed; API 295 passed/3 existing skips, including real health/TLS/transport/cancellation/cleanup cases |
| macOS x64 | Manifest selection and universal executable architecture verified; native x64 execution not tested |
| Advisory Windows CI | Passed this run; API 294 passed/4 existing skips; retained TRX diagnostics. This does not establish Windows release readiness. |
| Formatting / ABI / OpenAPI CI | Passed |
| Packaged Linux preview | Passed actual extracted-package/browser workflows and uploaded artifact |

Tests: https://github.com/dangerousnick/Castlumora/actions/runs/42960456142

Native ARM64 Mac job: https://github.com/dangerousnick/Castlumora/actions/runs/42960456142/job/130325480859

The production backup reserve remains exactly 5,368,709,115 bytes. Real backup
tests use a private per-instance policy and real SQLite/ZIP operations; no global
override or ordinary-user setting exists. The gate reports disk capacity without
requiring the production backup reserve for tiny test fixtures.

## Failure and correction disclosure

The first native-asset gate on f70bc317ca was blocked: the untrusted-certificate
case timed out in AcquireAsync's existing 15-second version probe before TLS.
All other backend projects passed. No failed head was published by that attempt.
Idle SDK shutdown and controlled probes were investigated; mostly defunct compiler
processes were observed, so no RAM-recovery or proven resource root cause is claimed.
Verified probes took 1.317–1.457 seconds in isolated/two-process diagnostics.

Real native-executable fixtures now share an isolated xUnit collection, and CI
serializes test projects to match the local gate. Unrelated CPU-heavy fixtures do
not compete for fixed native startup deadlines. All assertions, actual binaries,
within-test concurrency checks and production deadlines remain unchanged. Full
API, full local gate and required native Mac/Linux CI passed on the corrected head.
Earlier Windows cleanup failures are retained in history; their absence in this
run does not prove complete descendant/resource containment under every workload.

## Real downloadable Linux preview

[Download verified GitHub artifact](https://github.com/dangerousnick/Castlumora/actions/runs/42960456033/artifacts/13829470962)
(GitHub sign-in; expires 2026-12-11).

- Archive: Castlumora-1.0-dev-8aa77c0cc9c4-linux-x64.tar.gz
- SHA-256: `c51fd507d6187ecd889c1b3ebacd7414f2c1f0b3c7f73d55ea01d03475d7b603`
- Outer ZIP SHA-256: `b262348ea572eaac11306609546efb78201886e83ddd286db7973d440aa16ee8`

Independent connector download matched GitHub's outer digest and the inner
checksum. Source manifests matched the exact commit; original GPL LICENSE bytes,
server/.NET runtime, compiled branded Web, executable launcher, platform guide and
16 branding assets were checked by streaming inspection without another expanded
application copy. CI actually extracted/launched the package and tested fresh
setup/login/admin/logout/security, restart/account persistence, native library
creation/scanning/playback and bundled-tool installation/integrity/version health.

The archive includes install/dependency instructions, checksum, feature checklist,
license materials and CI smoke report. See INSTALL_TEST_BUILD.md. Existing protected
previews were not overwritten/deleted. This is a development preview, not stable 1.0.

## Limits and next milestone

Native ARM64 tool execution and backend CI are distinct from a complete Mac desktop
installation. No Mac application installer, native x64 Mac execution, physical Mint,
GPU/tuner or Docker/NAS deployment certification is claimed. Compiled legacy Web
still requires the documented compatible-assets fallback for remaining controls.

Main's separate OpenAPI Report still executes its legacy missing-JF_BOT_TOKEN
implementation. The tested standard-token replacement in PR3 needs reviewed
adoption on main; do not merge unfinished Phase 1.4 automatically. Main branch
protection inspection returned HTTP403, so repository-level required contexts
could not be verified/changed; an administrator must inspect those settings.

The next development task is the persistent authorized single-source importer:
durable private state/rights/provenance, secure library destinations, cancellation,
retry/recovery, deduplication, safe playable-container inspection, sanitized metadata,
promotion and scanning. Public URL jobs remain disabled until that full boundary is
implemented and tested. Every remaining 1.0 feature requirement stays mandatory.
