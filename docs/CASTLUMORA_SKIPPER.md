# Native Castlumora intro/outro skipper

This is built-in server/frontend code; no external detection plugin or cloud service is required. It uses the configured FFmpeg and the inherited central MediaSegments API/database. Existing providers and library enablement/order remain intact. No database migration is added.

## Detection and review

Enable optional analysis in Administration → Intro & outro skipper. The existing Extract Media Segments scheduled task discovers the native provider; its existing task controls/triggers remain available. An administrator can also select a local episode and analyze it explicitly.

Analysis compares an episode with up to three local peers in the same season. At least two other episodes must corroborate a continuous passage of at least 15 seconds. It decodes only the first/last third, bounded to 300/180 seconds respectively, into 8kHz mono PCM and compares changing spectral landmarks. Silence/flat repeated fingerprints are rejected. This is repeated-audio evidence, not semantic recognition of credits, dialogue or recaps.

All detected intervals start as **suggestions**. They cannot produce skip buttons or automatic skipping until an administrator reviews/corrects and approves them. Review the full interval and surrounding content; repeated recaps/advertisements can also match. An optional protected-content boundary rejects outros extending into post-credit content. Approved intervals retain exact ends; skipping an outro does not force an episode transition or discard material after the interval. The existing Next Episode action remains explicit.

The editor supports manual intervals, start/end correction, approval/removal, optimistic revision conflicts and reanalysis. File size/mtime signatures prevent the provider reusing review data for a replaced source. Refresh media/segments after replacing files so inherited central intervals are refreshed too. A source changed during analysis invalidates that analysis.

## Resource and cancellation boundaries

Analysis is disabled by default. A shared semaphore permits one analysis/FFmpeg process at a time; decoding uses one thread, bounded windows/output, a one-minute timeout per window and short delays between peers. The provider defers while the server reports a playing item, and the decode loop checks for playback starting. Cancellation/timeout terminates only the FFmpeg process instance created by that analysis. It never terminates unrelated encoders or recording processes.

A stale playback session left by an abruptly terminated client can postpone analysis until that session is cleared; the guard is intentionally conservative. Very short episodes (under two minutes), fewer than three local episodes, missing season metadata and nonlocal sources are unsupported for automatic analysis. Manual correction of local episodes remains available.

## Persistence and contracts

Files are atomically replaced under `<ConfigurationDirectoryPath>/castlumora/skipper/`: global settings, per-profile preferences and per-item revisioned review state. Include this directory in backup verification. A single server process owns these files; shared multi-process configuration is unsupported.

- Authenticated `GET Castlumora/Skipper/Settings`; administrator `PUT` controls `analysisEnabled` and `defaultMode`.
- Authenticated `GET/PUT Castlumora/Skipper/Profile` reads/writes only the claim's user ID; mode is default/manual/automatic/disabled.
- Administrator `GET/PUT Castlumora/Skipper/Items/{id}` reviews/corrects local episode intervals. `revision` prevents silent overwrite; bounds/type/status and post-credit protection are validated.
- Administrator `POST .../Items/{id}/Analyze` produces suggestions only.
- Saving reviewed intervals refreshes the existing enabled segment providers without force-deleting other providers' data. Approved native intervals are exposed by the inherited permission-aware `GET MediaSegments/{itemId}`.

The browser consumes central intervals and per-profile modes. Manual mode shows Skip Intro/Skip Outro only within an interval. Automatic mode seeks to its exact end; disabled mode does neither. Changing the mode takes effect immediately and persists on the server. TV/mobile clients can consume the existing central API, but native Castlumora TV/mobile renderers and device verification are not completed by this milestone.

## Verification and reproducible synthetic fixture

API regression tests cover changing/silent audio, corroboration, persistence, suggestions versus approval, revision/bounds validation, post-credit protection, source replacement, profile isolation and elevated endpoint policies. HTTP integration verifies ordinary-user denial and profile isolation. React tests cover manual/automatic/disabled modes and server persistence.

Actual FFmpeg testing with three synthetic episodes detected intro 10–40 seconds and outro 145–169.5 seconds. Unreviewed candidates did not appear in MediaSegments; approval exposed both while preserving content from 170 seconds onward. Actual Chromium verified manual skip, automatic skip and disabled mode against those intervals. These checks do not establish accuracy for real series, all codecs/languages or physical TV/mobile clients.

After the documented isolated server/movie setup, with the same CASTLUMORA_TEST_SERVER/USER/PASSWORD environment and an explicit `.castlumora-disposable` marker in the temporary root:

```bash
python3 browser/generate-skipper-fixture.py --fixture-root /tmp/castlumora-web-real-server
python3 browser/verify-skipper-fixture.py --fixture-root /tmp/castlumora-web-real-server
```

The verifier requires loopback, the matching disposable movie library and an unreviewed synthetic episode. It preserves existing review data and restores global analysis settings afterward. It prints the synthetic episode ID (no credentials). Set that as CASTLUMORA_TEST_EPISODE and run with Vite:

```bash
npm run test:browser -- browser/real-server.spec.ts -g 'approved intro'
```

Use a fresh disposable configuration when all three synthetic episodes already have review data. A failed browser test can leave an abandoned playback session; clear only the disposable fixture's session before further analysis. Never run these fixture helpers against production or Coon Media.
