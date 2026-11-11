# Castlumora development requirements

Preserve existing work on development/castlumora-web-1.3 and PR3. Read the permanent
product specifications and DEVELOPMENT_PROGRESS.md before feature development.
Never access/deploy to Coon Media or the official Jellyfin repository. Do not merge
main automatically. Preserve GPL notices, attribution and compatible identifiers.

## Mandatory pre-push gate

1. Run `./scripts/install-git-hooks.sh` in every fresh checkout. Git does not clone
   local hook configuration. Do not replace another hook configuration silently.
2. Commit the intended checkpoint locally with .NET10/Node24/Python3 available.
   A normal push runs `./scripts/validate-before-push.sh` through the mandatory
   hook before Git publishes anything. A separate manual run is available for
   diagnosis; an identical extra full run is not required before that hook.
3. Push normally, allowing `.githooks/pre-push` to validate the exact HEAD.
   Never bypass hooks, use --no-verify, disable analyzers, narrow formatting to
   selected files, skip required tests or push after a failed/incomplete check.
4. The gate runs full-solution formatting, Debug server build (ABI HEAD equivalent),
   Release solution build and all server tests with CI coverage settings, a real
   verified platform yt-dlp fixture, workflow-handler tests and frontend checks.
   Workspace-load warnings invalidate formatting verification. Resource/OS limits
   must be reported as blockers, never represented as a pass.
5. Inspect GitHub Actions for the exact published SHA. Investigate failure logs and
   fix required Linux/macOS or shared-code failures before further pushes.
   Windows-only failures are advisory for Phase1.4/stable1.0: retain tests/logs,
   investigate shared/security implications, preserve compatibility code and avoid
   repeated retries solely to obtain Windows success.
   Keep required CI enabled and unchanged in strength.

Linux/macOS and shared formatting/security/API checks remain mandatory. Windows
compatibility CI is non-blocking by explicit owner policy (2026-11-11). Native
macOS installation/runtime support requires evidence beyond a passing test job.
The legacy OpenAPI Report runs main's workflow: track its missing-token error
until reviewed adoption of the existing fix; do not automatically merge/change main
or mistake that external workflow-version issue for a development-source failure.

Do not start unrelated milestones while required Linux/macOS/shared validations fail.
Use small recoverable commits and keep progress/parity/roadmap accurate. Never
claim that an intermediate preview completes all Phase1.4 product requirements.
