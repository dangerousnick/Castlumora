# Single-video workflow — verification and environment boundaries

## Published application foundation

Branch development/castlumora-web-1.3; PR3. Application fbea85f83c plus corrected
formatting d6d5299f04, then fixture-only macOS correction
3726c3314c6b53960a4d44519296181547169164. No main merge/deployment.

The complete normal gate passed at3726: full-solution formatting2428files/zero
changes/no workspace warning; .NET10 Debug server/Release solution zero warnings/
errors;17backend projects4438passed/19existing platform skips; Node24 typecheck/
lint/build plus60tests; hook/native-fixture/bundler/report-handler tests. Production
backup reserve is unchanged and real backup operations remain tested.

Exact GitHub source3726: Tests43147576520 passed Linux130948891009,
macOS130948891062 and advisory Windows130948890675. Format43147576628,
ABI43147576586, OpenAPI43147576534 and Preview43147576526 passed. Native Mac
correctly uses /private/var; the original fixture compared an equivalent /var
alias. Its setup now canonicalizes only verified system aliases, retaining exact
path equality, real bytes/provenance/scan and all symlink/security assertions.

Real owned TLS/yt-dlp/native import → atomic publication → automatic indexing →
exact media API bytes → server restart/deduplication tests pass. These use a test-
only owned TLS/network fixture, not a production URL/TLS-policy override.

## Ordinary compiled server/browser

The ordinary Release executable and compiled Web ran on the existing marked
synthetic loopback fixture at127.0.0.1:18096, with normal FFmpeg validation. A real
Chromium workflow passed runtime install/hash/version health, reload/recheck,
rights confirmation, registered library selection, unsafe URL rejection with no
job creation and viewer denials. The optional browser fixture now supports both
separate dev-server connection and integrated same-origin autodetection, waits
for real library registration and uses the combobox's accessible name. Readiness
wait20seconds covers the existing backend health deadline15seconds; production
limits/assertions remain unchanged.

Startup originally hit the unchanged2GiB free-space safeguard. Safely removed175
old regenerable coverage directories (latest2/project retained) and unused server
Debug output, recovering real free space to2.6GiB. Source/history/stash, current
Release outputs, native dependencies, logs and all preview archives were preserved.
The normal server then started without a storage override. It was stopped cleanly
after browser validation; its synthetic state/media remain for recovery.

## Public egress limitation and stronger package test

A legitimate normal import of the repository's own public GPL clip failed locally.
An isolated diagnostic using the compiled production policy validated public DNS,
then received SocketException/Connection refused for the pinned TCP443 connection;
TLS was never reached. The identical URL returnedHTTP200 via the cloud's HTTP
proxy. This managed environment's proxy-only routing is a material test limitation,
not a reason to disable SSRF/certificate controls or claim a successful import.
The initial importer deliberately uses validated numeric HTTPS connections and
does not inherit arbitrary proxy/configuration credentials. Proxy-only server
networks require future controlled egress support; no bypass was added here.

The new packaged browser test is written but **must run successfully in GitHub
before being claimed passed**. It uses normal production APIs/default network
policy, the owner-authorized11734-byte synthetic H264/AAC clip pinned at36cf2b44a0,
a real Home Videos library, mandatory rights confirmation, persistent completion,
exact hash/size, source-query omission, deduplication, automatic indexing/native
codec metadata and advancing browser playback. It contains no fake queue responses
or production URL/TLS override. The preview workflow uploads a report/artifact only
after all three extracted-package browser workflows pass.

Pinned fixture:
https://raw.githubusercontent.com/dangerousnick/Castlumora/36cf2b44a01c0f88c6f5c9ec979161790f03d8a1/tests/Jellyfin.Api.Tests/Fixtures/import-owned.mp4
SHA256:8f56e298a9ceaacc3f34126546eeb3cd9d8368c85f3e2c719c63aaa98a55e554.

All remaining production-importer/online-metadata/Phase1.4/full1.0 requirements stay
mandatory. A preview, synthetic tests and CI do not certify Mint/macOS desktop
installation, real hardware acceleration, real tuners, long media or complete1.0.
