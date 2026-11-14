# Authorized single-video importer — development preview

This extends the existing managed runtime, durable queue and native inspection;
it does not replace the server's libraries, authentication, databases or plugins.
The integrated workflow is a bounded development preview. Refer to DEVELOPMENT_PROGRESS.md for
the exact published commit and verification status, rather than treating this
contract as a production-readiness claim.

## Multiple explicit URLs

The native administrator form also accepts up to25 direct URLs with atomic queue
acceptance. See [batch semantics and limits](CASTLUMORA_ATOMIC_IMPORT_BATCHES.md).
Downloads remain independently cancellable/retriable; no playlist/channel capability
is implied. Linux-only server policy supersedes historical platform requirements.

## Administrator workflow

In Administration → Media → Imports & Sources, install/check the bundled managed
runtime, select a destination library, enter a direct HTTPS MP4 URL, title and
rights/permission record, and explicitly confirm permission to retain the source.
Only authenticated elevated administrators can list, submit, cancel or retry jobs.
No source download is delegated to the browser; jobs belong to the server host.

The initial scope is a single direct H264/AAC MP4, at most32MiB and three minutes
of downloader execution, with one worker and at most three total attempts. Existing
jobs and new submissions default to manual retries. Administrators may explicitly
opt in to bounded transient retries; see CASTLUMORA_IMPORT_RETRIES.md. Playlists,
channels, YouTube page URLs, scheduled synchronization and format selection remain
unavailable. They remain
mandatory future work. All setup presets use the same functionality.

Eligible destinations are real Videos/Home Videos libraries with exactly one
existing local location, secure write permissions, and no overlap with server
application/configuration/data/cache/web paths. Ordinary requests cannot supply
filesystem paths, executable locations, command-line options or requesting-user
identities. Group/world-writable library roots are deliberately rejected; inspect
permissions without automatically changing existing media ownership or permissions.

## Persistence and publication

Private server data under `castlumora/imports` holds the locked atomic queue
journal, private staging and prepared publication records. Source query credentials
and rights records remain private; queue responses omit source paths and query
strings. Logs identify job IDs and failure types instead of dumping source URLs
or untrusted downloader output. Back up and preserve this directory for recovery.

Running jobs are requeued after interruption within the attempt limit. Cancel
requests persist before signalling the owned child process tree. Failed/cancelled
jobs can be retried through their existing IDs. Submitting the same source and
library returns the existing pending/running/completed job. The journal is bounded
to1000jobs/8MiB; automatic retention/pruning is not implemented.

A completed download must pass bounded container checks, native FFprobe inspection,
real first-frame FFmpeg decoding, and SHA-256/byte verification. Publication
reauthorizes the administrator and library, records provenance privately, copies
to a private partial file in the destination filesystem, flushes it, then renames
it atomically without overwriting existing media. A safe locked NFO preserves the
administrator's title; raw downloader sidecars, URLs and credentials are never
copied into public metadata. Manual NFO corrections survive reconciliation.

Content hashes prevent duplicate files within a destination's reserved
`Castlumora Imports` folder. Its ownership marker must match the real library ID;
unowned files, conflicting metadata and links are rejected and retained for review.
Prepared records allow restart reconciliation of already committed content without
another download. Cancellation after the rename commit point does not remove or
misreport committed media. Library permissions continue to govern browsing and
playback after import.

Completion means the media has been committed and normal library scanning requested;
it does not mean asynchronous metadata/indexing has completed. Metadata and playback
use the existing library/provider/media API. First-frame validation does not prove
that every frame is valid or certify hardware acceleration. Filesystem power-loss,
all network/NAS configurations and native macOS installation are not certified.

## Verification and troubleshooting

Tests use an owned synthetic video, private test CA/TLS source, the reviewed real
yt-dlp runtime and real FFmpeg/FFprobe. Test-only network/dependency injection does
not expose a production URL/TLS bypass. Publication tests exercise real files,
prepared records, cancellation, duplicates, permissions, malicious destinations,
manual metadata and interrupted reconciliation. Real HTTP tests demonstrate automatic library indexing, exact media API bytes,
completed-job/deduplication persistence and media delivery after a host restart.
Final full-gate, native CI and packaged/browser evidence is recorded separately.

If a job fails, retain its ID and private state, inspect server diagnostics and
verify source permission, supported media bounds, dependency health, destination
permissions and available space. Do not share signed URLs, private journal files,
passwords or tokens in bug reports. Report reproduction steps, exact build commit,
OS, safe logs and screenshots. Never delete the database or completed media to
repair an import queue. Existing server functionality remains available if the
queue cannot safely open its journal.

The initial egress model opens validated numeric HTTPS connections and deliberately
does not inherit arbitrary proxy settings/credentials. Proxy-only networks may
reject this traffic; see verification-single-video-import-1.4.md. Controlled trusted
egress support remains future work; do not disable SSRF or TLS validation.
