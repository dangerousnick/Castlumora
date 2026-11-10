# Internal importer staging checkpoint

This is a tested internal transport increment, not a production importer. It is
not registered as a public service or exposed through job endpoints or the UI.
`downloadJobsAvailable` remains false. Existing libraries and server functions are
unchanged. No installation or deployment to an existing server is required.

## Implemented boundary

`YtDlpImportRunner` acquires the existing hash-verified runtime lease and launches
the pinned yt-dlp executable with fixed arguments and no shell. This increment
accepts public HTTPS direct `.mp4` sources only. Every connection uses the
authenticated, validating import tunnel. The source is supplied on standard input;
the proxy credential is supplied in a cleared child environment, never command
arguments. Raw downloader diagnostics and signed source URLs are not logged.

Only the generic extractor and native downloader are enabled. User configuration,
plugins, caches, JavaScript runtimes, remote components, geo-bypass headers,
playlist expansion, fixups and external downloaders are disabled. Production TLS
verification is retained; a private CA is accepted only through the internal owned
test-fixture constructor. No arbitrary command or executable selection is exposed.

Each attempt owns private staging files and a private runtime temporary directory.
Output names are fixed, links/subdirectories/unexpected files are rejected, and
completed output must contain a nonempty media file plus metadata without a partial
download. The returned disposable result contains the actual byte count and SHA-256;
disposing it removes only that attempt. Failed/canceled attempts are cleaned up.
Cleanup failures log only the attempt ID for a future recovery worker.

Limits: 32 MiB media, 2 MiB per metadata/state file, 16,384 characters per diagnostic
stream, three minutes including runtime acquisition, and a 1 MiB/s downloader rate.
The filesystem monitor runs every 100 ms, so size enforcement is observational and
can overshoot between samples; it is not a filesystem quota. Existing tunnel
connection and byte limits also apply. The owned process tree is terminated on
failure/cancellation and streams are observed with bounded cleanup waits.

This does not provide OS job/cgroup isolation, aggregate concurrency accounting,
memory/CPU quotas or crash/orphan recovery. Trusted executable extraction overhead
in the runtime temporary directory is not counted as untrusted media output.
Neither a `.mp4` suffix nor a successful transfer proves a playable container.
Metadata is still untrusted and remains private; it must not be published directly.

## Verification

On .NET 10, standalone API Debug and API-test Release builds passed with zero
warnings/errors. The complete API suite passed: 295 passed, three existing skips.
Fourteen staging cases cover the real pinned executable, exact bytes/hash,
disposal/lease release, untrusted TLS, denied access, declared and streaming size
limits, unexpected output/metadata growth, cancellation, diagnostic bounds,
deadline handling, unsafe sources and fixed process arguments. All network fixtures
are owned loopback TLS servers with injected DNS/connection policy; no public media
was downloaded. Metadata growth/unexpected files are producer-fault injection,
and transport payload bytes are not playable-media fixtures.

The first focused run passed 11/12; one case failed in the existing runtime
installation health probe before the runner started. An isolated verified executable
probe subsequently took 4.44 seconds, and the later full API run passed. The cause
of the original timeout was not established. The 15-second health deadline was
not relaxed and the failure was not skipped.

Final repository formatting verification is recorded in DEVELOPMENT_PROGRESS.md.
These checks do not substitute for the mandatory full pre-push gate. The complete
gate remains infrastructure-blocked by less than 5 GiB free disk space. No push,
GitHub CI success, Windows/Mint manual test, playable-media validation, or production
readiness is claimed for this checkpoint.

## Next implementation boundary

First restore sufficient disk capacity, run the complete mandatory validation gate
on a clean committed tree, resolve every failure and inspect exact-source CI after
publishing through the hook. Preserve all existing commits and requirements.

Then implement one persistent, administrator-authorized single-source queue slice:
rights/provenance acknowledgement; destination authorization; bounded playable
media inspection with fixed format/protocol restrictions; sidecar sanitization;
deduplication; durable history; cancellation/retry/recovery; promotion followed by
library scanning. Only expose URL jobs when the complete security and lifecycle
boundary is tested. YouTube identification independent of downloading, playlist and
channel migration, scheduling, production resource containment and native dashboard
work remain mandatory later requirements, not completed features.
