# Backup capacity policy and real integration tests

## Design

The production reserve remains exactly 5,368,709,115 bytes (the original constant,
approximately 5 GiB). The existing public `BackupService` constructor and DI wiring
are unchanged; that constructor always creates the production policy using real
filesystem free space. Unknown capacity still fails closed. No application option,
environment variable, API or global test switch lowers production safety.

An internal constructor accepts an immutable per-instance `BackupStoragePolicy`.
Tests can control its reserve and capacity observer without replacing backup
creation. A nonpositive reserve is invalid. Tests cover the production boundary,
unknown/zero/insufficient capacity, custom-reserve rejection, policy isolation,
and a real SQLite export/ZIP at the isolated boundary. The existing corrupt
keyframe regression uses actual filesystem capacity with a 1 MiB test reserve;
it still reads the generated ZIP and verifies the healthy row survived while the
corrupt row was skipped. Each test owns and cleans its database/filesystem fixture.

The pre-push gate records capacity as telemetry instead of imposing the production
backup reserve on builds. All actual formatting, workspace loading, build, test,
bundling and frontend failures remain fatal. Hook tests include an actual-stage
simulated ENOSPC failure and a complete successful pipeline with low reported
capacity. No backup test is skipped or replaced with a mocked backup operation.

## Verification checkpoint

Full local gate passed on190d5ea9ed36b51044d218228b2524a6e8272681:
full solution format (2,417 files, zero changes, no workspace warning), Debug server
and Release solution builds (zero warnings/errors), 4,365 backend tests passed with
19 existing skips across17 projects,58 frontend tests/typecheck/lint/build,9 hook
cases,5 bundler cases and5 workflow-handler cases. Focused backup tests10/10 passed,
including real ZIP inspection. Logs:/tmp/castlumora-prepush.XfcHPStd.

Verification results and exact source checkpoint are maintained at the top of
DEVELOPMENT_PROGRESS.md. The prior storage reports describe earlier source; their
5 GiB executor prerequisite is superseded by this design. A complete mandatory
gate and successful normal push are still required before publication is claimed.
Windows and hardware-specific tests cannot be claimed from Linux-only execution.

## Phase 1.4 continuation

After full validation and publication, continue the existing internal import
staging implementation toward the first persistent authorized single-source job.
Do not enable public URL jobs until rights/provenance, destination permission,
bounded media validation, metadata sanitization, deduplication and recovery are
implemented and tested. Preserve legacy administration and all completed work.

## Published CI and preview result

Published0293885c4bb5bee25a912d0903916eecaec31020 through the full hook.
Format, ABI, OpenAPI, Linux/macOS tests and extracted-package Linux preview passed.
Windows failed five existing staging-runner abort/cleanup cases; no backup case
failed. The separate OpenAPI Report still runs main's legacy missing-bot-token
implementation; the tested replacement in PR3 requires reviewed adoption on main.
No full cross-platform CI success is claimed and no repeated failing source push
is authorized. Exact jobs, verified preview download/checksums and recovery steps
are recorded in DEVELOPMENT_PROGRESS.md.
