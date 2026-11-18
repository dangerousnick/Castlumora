# Platform policy and verification evidence

## Authoritative Linux-only server policy — 2026-11-13

Castlumora1.0 targets Linux servers only: Mint22.3/newer compatible versions, Ubuntu
LTS, Debian, Docker on Linux and compatible Linux NAS. macOS and Windows server
support are deferred indefinitely, including CI/release/packaging acceptance.
Preserve inherited cross-platform code and clients where practical; no unnecessary
platform-removal refactoring. Linux security, integrity and functionality remain
mandatory. This supersedes older platform requirements below; all original1.0
features and the future1.0→2.0 migration plan remain in scope. No backend rewrite.

Use focused feature tests, full backend validation for substantial application
increments, relevant frontend/native checks, and lightweight documentation-only
validation. Classify the whole unpublished range, fail closed on mixed/unknown
changes, and preserve unique security/backup/migration/regression coverage. Do not
repeatedly rebuild unchanged source or create needless documentation checkpoints.

## Current acceptance and evidence

Linux is the only required server CI/release target. Ubuntu-hosted checks cover
Debug/Release, full backend security/backup/migration/native regressions and actual
extracted-package browser workflows. Mint hardware, Debian distribution coverage,
Docker/NAS volumes/permissions, drivers and real tuners still require installation
and runtime evidence; a green CI job alone does not certify those environments.

macOS/Windows builds passed at883d1e60ba, retained as historical evidence only.
Neither is a1.0 server support or packaging obligation. Their compatibility code,
platform manifests and tests remain available for future work without automatic
runner cost. Clients on those operating systems are not intentionally broken.

CI retains the required context run-tests (ubuntu-latest), formatting/ABI/API/security
checks and all unique Linux tests. Documentation-only synchronize events validate
only the whole changed range; unknown or application changes run the application
checks. Hooks classify the full remote-to-HEAD range, so a final documentation
commit cannot hide application changes. Actual failures remain fatal.

Repository protection was previously inaccessible (HTTP403); do not claim changes
to it without verifying. An administrator must retain required Linux/shared checks
and remove obsolete macOS/Windows required contexts if configured. No automatic
merge into main or weakening of shared checks is authorized.

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
