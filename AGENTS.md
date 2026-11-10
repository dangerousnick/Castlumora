# Castlumora development requirements

Preserve existing work on development/castlumora-web-1.3 and PR3. Read the permanent
product specifications and DEVELOPMENT_PROGRESS.md before feature development.
Never access/deploy to Coon Media or the official Jellyfin repository. Do not merge
main automatically. Preserve GPL notices, attribution and compatible identifiers.

## Mandatory pre-push gate

1. Run `./scripts/install-git-hooks.sh` in every fresh checkout. Git does not clone
   local hook configuration. Do not replace another hook configuration silently.
2. Commit the intended checkpoint locally, then run
   `./scripts/validate-before-push.sh` with .NET10/Node24/Python3 available.
3. Push normally, allowing `.githooks/pre-push` to validate the exact HEAD again.
   Never bypass hooks, use --no-verify, disable analyzers, narrow formatting to
   selected files, skip required tests or push after a failed/incomplete check.
4. The gate runs full-solution formatting, Debug server build (ABI HEAD equivalent),
   Release solution build and all server tests with CI coverage settings, a real
   verified platform yt-dlp fixture, workflow-handler tests and frontend checks.
   Workspace-load warnings invalidate formatting verification. Resource/OS limits
   must be reported as blockers, never represented as a pass.
5. Inspect GitHub Actions for the exact published SHA. Investigate failure logs and
   fix the cause before further pushes; do not repeat a known broken checkpoint.
   Keep required CI enabled and unchanged in strength.

Do not start unrelated milestones while these required validations are failing.
Use small recoverable commits and keep progress/parity/roadmap accurate. Never
claim that an intermediate preview completes all Phase1.4 product requirements.
