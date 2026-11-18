# Verification — Node 24, native creator metadata and administrator controls

Date: 2026-11-10. Branch: development/castlumora-web-1.3. Review: https://github.com/dangerousnick/Castlumora/pull/3 . No merge or production deployment.

## Source checkpoints

- dc26e0aedb53d105ad721b55972117082ea8eb55: Node24 LTS/dev engines/action migration and missing OpenAPI report token.
- 9a89e239d4f9d2852b5d938fb1c204f8c5f58d70: native retained-sidecar source identification and safe generic-video browsing/details.
- 269d4c5b3909dad3fb1c47491e3a3bd8f50a1b71: native selected-item Metadata Management.
- 8e489b965dd38fd833bede3f4372e18e36594040: precise SA1513 formatting correction; Format/ABI/OpenAPI Check then passed.
- **835716785add501a27d98e979fa67d411c63b570**: updated owner install/checklist and manifest stage; exact latest packaged application source. Later documentation-only checkpoints do not change its application code.

## Runtime and reporting

Node v24.22.1 local; repository .nvmrc=24, frontend engines >=24 <25, preview setup-node reads .nvmrc. Clean npm ci/typecheck/lint/production build and 56 frontend tests passed. Five Node report-handler tests pass; preview packaging runs them without extra dependencies. All workflow files pass actionlint v1.7.12. Pinned checkout/setup/artifact actions already supported Node24; deprecated Node20 report-comment and Node12 wait/yq helpers were replaced with official Node24 github-script and a pinned Docker/digest yq action.

Actual old report annotations confirmed `Parameter token or opts.auth is required`. New reporting uses built-in github.token with job-scoped permissions, skips missing/stale/closed PRs and bounds report text. The privileged comment job checks out only the trusted workflow SHA and does not execute PR/artifact code. The default-branch workflow_run implementation is unchanged until reviewed merge; old main report failure emails can continue meanwhile. No claim that this future live comment workflow has already executed successfully. Release/version-bump publishing was not invoked; inherited build.yaml-dependent release tooling still requires independent Castlumora work. No release/deployment permission broadened.

## Backend and actual workflows

Exact-source 835716785add GitHub full server test jobs **all passed on Ubuntu, Windows and macOS**: https://github.com/dangerousnick/Castlumora/actions/runs/42741231552 . Downloaded Ubuntu job logs show **17 projects, 4,255 passed, 19 skipped, zero failures**. Provider tests (532, including 17 new cases), LiveTV tests (81), API tests (195/3 skips) and integration tests (131/3 skips) are subsets of those totals, not additional independent totals. Format, ABI HEAD/BASE/Difference and OpenAPI Check head/base/report generation also passed on this exact source. The separately triggered old main OpenAPI Report is the rollout limitation above.

Local actual-backend creator fixture verified previously scanned media, strict source/channel IDs, explicit metadata repair, global and title-only locks, unlock repair, original runtime/media preservation and conflicting filename rejection. Normal refresh retains an existing title; explicit repair was tested without changing global merge behavior. Local actual browser verified reader settings persistence/reload/order, correction/lock/repair, provider-ID preservation and independent viewer denials for update/refresh/library-options writes. Fixture settings/item were restored; synthetic viewers removed. Explicit label associations corrected issues found in real browser runs.

## Downloaded application

Verified successful GitHub run: https://github.com/dangerousnick/Castlumora/actions/runs/42741231279 .

**Download:** https://github.com/dangerousnick/Castlumora/actions/runs/42741231279/artifacts/13730390071 (GitHub sign-in; expires 2026-12-10T02:26:34Z).

- Archive: `Castlumora-1.0-dev-835716785add-linux-x64.tar.gz`.
- SHA-256: `030ffa8840f267032b1fe9a6073f2a75b4c0520c2d6ac7041ba1c0ad10ce01ca`.
- Exact BUILD_INFO.commit: `835716785add501a27d98e979fa67d411c63b570`; creatorSidecarImplemented=true; metadataManagementStage=selected-item-local; full metadataProviderImplemented/importerImplemented remain false.
- Actual ZIP retrieved through the connected GitHub app, safe ZIP/tar paths inspected, inner checksum/manifest verified, application/server/web/branding/GPL/license/metadata guide presence confirmed. CLI Azure artifact/log routes previously returned 403; connected app retrieval succeeded. No fabricated ChatGPT artifact link.
- CI compiled the integrated web and self-contained linux-x64 server, extracted the real archive, passed fresh setup/login/admin/logout/security and restart/account persistence/library creation/scan/movie playback before upload. Native-dependency installation was slow but succeeded without cancel/restart.
- Independent local download/extraction ran its actual server **and compiled web**, serving HTTP from the package, against marked disposable loopback data (no Vite frontend). Creator browsing/escaped description/playback, persistent Metadata Management/repair/viewer denials, and existing movie authenticate/browse/details/play/progress passed (3/3). An earlier identical-application 8e489b965dd3 package also passed those three workflows independently and remains preserved.

## Final packaged DVR verification

The exact downloaded **835716785add** package also passed both owned M3U/XMLTV DVR browser workflows: completed schedule/cancel/record/file/play/denied-viewer/remove-retain/rescan/physical-delete (1.9 minutes), and active stop before scheduled end with nonempty retained partial media, denied viewer stop and real partial playback (1.2 minutes). Each fixture's original guide/options/admin policy was restored and its synthetic viewer removed. No metadata-admin-state.json or dvr-fixture-state.json remains pending recovery. This yields **five additional local actual-package browser workflows passed**, separate from CI's two fresh/restart smoke cases. All used the packaged server and its compiled web, not Vite. Existing original synthetic media survives.

## Scope and remaining limitations

This is an intermediate development preview, not full Phase 1.4 acceptance. Native local sidecars and selected-item metadata controls are implemented; complete online enrichment, channels/playlists/languages/cache, bulk/ambiguity/duplicates/artwork management, managed yt-dlp import/queue/scheduling and remaining profile/personalization/creator/business/cloud/native-client requirements remain mandatory unfinished work. Legacy compiled Web is not bundled; compatible-assets attachment and native/legacy limitations are documented. No physical Linux Mint installation, supported GPU/hardware acceleration, real tuner, broad codec/device matrix or long-duration DVR certification is claimed. Existing GPL/copyright/attribution and compatibility identifiers are retained. Coon Media and the official Jellyfin repository were not modified.

Usage allowance/reset information is not exposed by available tools; status is Not accessible. No automatic background continuation is promised. DEVELOPMENT_PROGRESS.md records the next bounded importer milestone and recovery instructions.
