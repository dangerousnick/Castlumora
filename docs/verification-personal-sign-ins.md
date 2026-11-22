# Personal saved sign-ins — development verification

Application source: `ebab7567cfa7c42cbdac3ac438a236372c79427f`.
Branch: `development/castlumora-web-1.3`; PR3 targets main, not merged.

## Implemented behavior

Viewing profiles → My signed-in devices lists only the signed-in identity's
persisted authorizations. It marks the current token, shows twenty rows per page,
and confirms selected or bulk sign-out of other tokens. Current-token revocation
uses ordinary Sign out. Other accounts and separate viewing profiles remain
unaffected; parent-wide profile session management is not implemented here.
No access tokens/physical device IDs are returned. Responses use no-store.
Machine API keys cannot list or revoke; query/body overrides cannot choose another
identity. Unknown/foreign record IDs return404; current-record DELETE returns409.

## Automated verification actually run

- Complete normal pre-push gate: all2456files restored formatting/0changes, Debug
  and Release0warnings/0errors, .NET10.0.403, all17backend suites4619passes/
  20existing skips. Production backup/storage safety remains tested and intact.
- Node24.22.1: typecheck, lint,107tests in27files, production build passed.
- Real SQLite HTTP regression covers ordinary accounts sharing a physical device,
  anonymous/API-key denial, foreign/current record denial, bounded pagination,
  secret-free inventory, selected/bulk revocation, current and peer preservation.
- Component cases cover confirmation, errors, older-server fallback, escaped
  client-supplied names and shrinking inventories across pages.
- Independently launched Release server plus compiled web on Debian13/loopback28098:
  actual Chromium fresh setup and single/bulk revocation passed. Current token200,
  revoked tokens401. Cold restart retained the same record/token/identity and no
  revoked records. Fixture stopped cleanly; private temporary token proof removed.
- Exact-source hosted Format/OpenAPI Check/ABI passed. Hosted backend Tests44996487239/job137128580864 passed. Downloaded hosted
  logs independently total17suites/4619passes/20existing skips/0failures.
- Preview44996487177/job137128578269 passed six actual extracted-package browser
  executions: fresh setup, restart/library/movie playback/import regression,
  managed profiles/media/password/device controls, interrupted recovery and another
  cold restart. Existing assertions were retained; no empty artifact was created.

## Download independently verified

https://github.com/dangerousnick/Castlumora/actions/runs/44996487177/artifacts/14743888286

Archive: `Castlumora-1.0-dev-ebab7567cfa7-linux-x64.tar.gz`.
SHA-256: `deca97f7ee4a09b2391c32ec083a276e60c34dd5a5d4633e9fd6ca8f936ef9ff`.
Artifact ZIP SHA-256:
`ee1a539b320febaa93ee4b23f35999c5c42f1cf8e080a3f0b44e686704c6ff98`.

Verified actual ZIP/archive hashes, exact source manifest, self-contained runtime,
compiled controller and controls, pinned Node license/hash, sixteen branding files,
GPL/other licenses, startup executable modes and exact-source docs. No private
profile registry/credential key is packaged. Installation/checklist are included.
This report was written after the build, so it is not claimed to be inside that
already-produced archive. GitHub artifacts require sign-in and expire; use the
run's artifact link, not a ChatGPT download.

## Limitations

Development preview only. The hosted package ran on Ubuntu24.04. The separate
local test used the installed SDK runtime and native nightly FFmpeg for startup;
it did not verify playback or the self-contained archive on Debian. Stable-FFmpeg
packaged DVR verification is next. No physical Mint22.3/NAS, hardware acceleration,
real tuner, macOS/Windows support or complete1.0acceptance is claimed. Existing
YouTube regressions remain, but further importing/creator-metadata work is deferred.
The main-only OpenAPI Report adoption blocker remains separate from the passing
OpenAPI Check. No main merge, production deployment or Coon Media access occurred.
