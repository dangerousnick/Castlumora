# Persistent importer lifecycle — internal increment

This extends the existing managed runtime and direct-HTTPS staging runner. It is
**not yet a publicly available library importer**. No controller, hosted worker or
UI submission is registered, and `downloadJobsAvailable` stays false. Production
media, existing server configuration and authentication are unchanged.

## Implemented

`ImportQueue` is a versioned private journal and single asynchronous worker.
Submission requires a public HTTPS direct `.mp4` URL, an opaque destination-library
UUID, administrator UUID, bounded title and explicit rights/provenance record.
There is no caller-supplied destination path, executable, shell argument, cookie,
provider credential or production safety override. These internal identifiers are
not a substitute for checking real library existence/permissions at the eventual
API and publication boundaries.

Jobs are durably persisted before acknowledging submission or changing state.
Each replacement writes a new private file, flushes bytes to disk and atomically
replaces `queue.json`. A separate exclusive ownership handle prevents two queue
instances sharing a journal. Directories are POSIX0700 and files POSIX0600;
Windows retains inherited server-directory ACLs. Links/reparse points, invalid
schema/state/identity and oversized or corrupt journals fail closed. Failed loads
preserve the original journal for operator recovery. Atomic replacement protects
against partial JSON on process interruption; this is not a claim of directory
fsync/power-loss durability on every filesystem.

The journal retains the original normalized URL (including any signed query),
rights record, requester, title, destination UUID, timestamps, attempts, status and
completion receipt. Those are private server data, not ordinary-user responses or
logs. Backups of this directory must be treated as sensitive. Exception text,
source URLs/tokens, raw subprocess diagnostics and arbitrary paths are excluded
from queue logs; failures retain a fixed error code plus job ID and attempt.

One worker enforces concurrency1. Source/destination SHA256 keys prevent duplicate
queued/running/completed submissions; completed history remains retained. Failed
or cancelled jobs can be explicitly retried, with a maximum of three attempts per
job. A replacement submission blocks retries of the old source/destination job.
The journal is capped at1000 records/8MiB loading; history is not silently evicted.
Stable provider-ID and content-checksum deduplication across different URLs,
retention controls and larger archive scale remain future work.

Cancellation is persisted before signaling the active executor. Queued jobs cancel
without execution. Shutdown interrupts work but preserves its identity for startup
recovery; interrupted Running jobs requeue with bounded attempt counts, while
persisted Cancelling jobs recover as Cancelled. The worker observes process cleanup
before starting the next job. Recovery restarts a download; it does not claim
partial-byte resume. Actual byte progress is sampled in memory and final bytes/hash
persist with completion; no unsupported total/ETA is invented.

An executor receives the stable job ID, progress callback and cancellation token.
It must reconcile an existing publication receipt before repeating side effects.
A successful receipt wins a cancellation race after the publication commit point;
otherwise cancellation/failed execution cannot claim success. Receipt byte/hash
validation rejects invalid success results. Durable journal errors stop the worker
rather than publishing an acknowledged state that was not saved.

## Validation

Tests exercise real journal/file I/O, reload, exclusive ownership, corrupt/link
rejection, rights/destination/source requirements, duplicate submissions/retries,
concurrency, progress, cancellation, shutdown recovery, attempt exhaustion,
publication/cancellation ordering and receipt rejection. Real pinned-tool TLS tests
connect the queue to the existing staging runner and verify exact owned transport
bytes, durable completion/deduplication, process cancellation, real download restart recovery and staging cleanup.
These transport fixtures are deliberately **not playable media** and do not prove
safe library publication. See DEVELOPMENT_PROGRESS.md for exact executed results,
failures and mandatory full-gate/published CI status.

## Required next boundary

Implement idempotent library publication and restart reconciliation using the job
ID. Resolve a real authorized generic-video library UUID to a configured local
root on every attempt, reject unsafe ancestors/links and source/destination changes,
validate a bounded self-contained playable container with fixed safe FFmpeg/probe
protocols, generate sanitized metadata rather than publishing raw yt-dlp sidecars,
retain private rights/provenance, deduplicate content, atomically promote complete
files and trigger the existing library scanner. Test crashes/cancellation around
publication, existing-file collisions, path traversal/reparse substitution,
unauthorized users and real media/scanning/playback. Only then expose an elevated
administrator queue API and the existing Imports & Sources screen. Persistent
source allowlists, retry/backoff/resource controls, playlist/channel/bulk/schedules
and all permanent1.0 requirements remain mandatory.
