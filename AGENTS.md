# Castlumora development requirements

Continue development/castlumora-web-1.3 and PR3. Read the permanent requirements
and latest DEVELOPMENT_PROGRESS.md. Preserve working code/history/GPL attribution.
Never access/deploy to Coon Media, modify official Jellyfin or merge main automatically.

## Owner policy — Linux servers only (2026-11-13)

Linux Mint/Ubuntu LTS, Debian, Docker on Linux and compatible Linux NAS are the1.0
server targets. macOS/Windows server support is deferred indefinitely. Preserve
cross-platform code where practical; do not spend time on unrelated non-Linux
failures. No previously agreed1.0 feature requirement is removed.

## Validation and publishing

Install scripts/install-git-hooks.sh in fresh checkouts; preserve existing hooks.
Use focused tests during development. Substantial application changes must pass
the normal pre-push gate: complete restored formatting, .NET10 Debug/Release,
all backend suites including security/backup/migration/native importer regressions,
and relevant Node24 frontend checks. Do not bypass hooks, suppress analyzers, omit
unique coverage or push known failures. An identical extra manual full run is not
required before the normal hook. Preserve exact snapshots during validation.

Documentation-only changes use UTF-8/conflict-marker/whitespace checks and avoid
application builds/tests. Classification covers the whole unpublished push range,
not just the final commit. Unknown bases, new branches, workflow/policy changes,
symlinks or mixed changes default to application validation. Never claim docs-only
checks are an application test pass. CI uses the same conservative classifier.

Verify exact-SHA required Linux/shared CI; macOS/Windows are not required jobs or
release acceptance targets. Preserve formatting/security/API checks and investigate
actual failures. Disclose checks unavailable locally and actual runtime evidence
separately from CI. The separate main-only OpenAPI Report awaits reviewed adoption
of the existing token fix; do not merge/change main automatically.

Prefer coherent tested feature increments. Update concise progress/parity/roadmap
within those increments, then publish. Avoid repeated documentation-only full
pipelines or recreating completed work. No claim that a preview completes Phase1.4.
