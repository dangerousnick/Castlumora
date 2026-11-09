# Castlumora Web Phase 1.3 development progress

Branch: `development/castlumora-web-1.3`. Base: `main` at `b06078bb7c`.
Permanent specification: [docs/CASTLUMORA_PRODUCT_REQUIREMENTS.md](docs/CASTLUMORA_PRODUCT_REQUIREMENTS.md).

## Completed implementation (verification in progress)

- Inspected existing authorization, browsing, playback and configuration APIs. Main contains Phase 1 branding and Phase 1.1 version/project documentation.
- Added additive presentation persistence/controller: administrator draft/publish, optimistic revisions, validation, profile filtering and per-user watchlist/resume exclusions.
- Created isolated React/TypeScript/Vite project; API adapter, authentication, media rows, search, browsing, details, seasons/episodes, player, Live TV channels/guide and administrator layout editor are implemented pending verification.
- Existing server APIs, database, plugin architecture and legacy Web fallback preserved. No production server access or deployment.

## Current checkpoint

Milestone 1 published on GitHub as `b30674348ab61176bc1193a819522e11177b5e3f`: independent frontend foundation. Frontend typecheck, lint and production build passed; 15 schema/API/component tests passed. API regression suite: 164 passed, 3 skipped. Chromium fixtures: 2 passed. Server executable build and real HTTP integration verification are in progress. This checkpoint does not establish actual-server playback or tuner compatibility. Frontend source and requirements are committed and published; backend/store tests, parity/roadmap/docs and browser verification are ready for the next checkpoint.

## Milestone 2 checkpoint

Presentation API/store and 7 new server unit cases are implemented; API regression suite passes 164 tests with 3 existing skips. Final isolated API compilation and executable compilation against existing built references each pass with 0 warnings/errors. A full server/reference build and the new HTTP integration test are the next verification gates. Browser fixtures pass 2 tests. Milestone 2 is published as `73b7960fdae6bb48af4f34722249cc3da743e480`. Backend, tests, formatting, screenshots, requirements, roadmap and parity docs are recoverable from GitHub. No claim of real-server playback yet.

## Milestone 3 checkpoint

HTTP integration verification passed: anonymous access rejected, administrator save/publish succeeds, stale revisions rejected, ordinary-user save/publish forbidden, user preferences isolated. Test cancellation-token analyzer requirements fixed. Frontend has 16 passing unit/component/API tests, typecheck and lint pass. Legacy links now use the installed client root rather than assuming a router version. Full server-reference build and real-server/browser playback are still running/pending.

## Exact next steps

1. Responsive CSS and schema/API/component/store tests are implemented. Finish server executable build and execute the HTTP authorization integration test.
2. Latest frontend typecheck/lint/unit tests/build and fixture browser tests pass; verify remaining server checks and commit/publish milestone 2.
3. Verify real disposable server authentication/browsing/playback if local dependencies permit. Never use Coon Media. Record fixture testing separately from actual-server testing.
4. Parity inventory, roadmap, schema/API/development instructions and verification report are drafted; finalize actual results and limitations.
5. Publish tested milestones to this branch and open PR against main; do not merge.

## Deferred scope

Full advanced administration uses legacy interface. Invitations/signup, tiers/payments, creator/OBS, personal cloud/offline, recommendation engine, notifications, native clients, plugin UI execution and history/rollback are roadmap work, not implemented features.
