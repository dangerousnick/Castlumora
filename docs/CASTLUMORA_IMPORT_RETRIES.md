# Authorized import retry policy

Status: verified at application commit57129188fac4288002e4240cd311bf89059085e9.
The full local gate, required Linux/macOS/shared CI, advisory Windows and updated
packaged preview passed. DEVELOPMENT_PROGRESS.md records exact evidence and known
platform limits; this is a bounded preview, not full production-importer parity.

## Administrator controls

Administration → Imports & Sources offers an unchecked automatic-retry option.
Only an authenticated elevated administrator can submit it, together with the
existing rights confirmation and eligible library. Existing journals and clients
that omit the option remain manual. Duplicate submission returns the existing job
without changing its rights, owner, policy, attempt count or deadline.

Opted-in jobs retry only fixed sourceNetwork and sourceRateLimited failures. The
first and second network delays are15/30seconds; rate-limit delays are60/120seconds.
These are bounded preview defaults, not a general source Retry-After implementation.
Certificate, authorization, resource, format, output and unknown failures need
administrator review. No certificate/DNS/address/path/native validation is relaxed.

The unchanged limit is three total attempts, including manual retries and restart
recovery. Automatic retries cannot reset it. The dashboard shows the scheduled
local time and permits cancellation while waiting. Backoff does not occupy the
single worker: other ready jobs continue. Waiting jobs remain queued for source
and library deduplication. Manual retry of an eligible failed/cancelled job remains
an explicit administrator action.

## Persistence and recovery

Optional AutomaticRetries and NextAttemptAt fields extend the private version1
journal. Old entries default to manual/no deadline. Atomic saves retain the same
job identity, rights, source and owner across restarts. Invalid scheduled states
fail closed while preserving the journal. The public response includes only the
boolean and ISO8601 deadline alongside existing redacted fields.

Restart retains a queued retry's exact deadline and attempt count. Running and
cancelling recovery retains the existing reconciliation rules. Cancellation clears
the deadline durably. Successful publication remains final even if cancellation
races with its receipt. Every actual execution still rechecks administrator and
library permissions and uses the same bounded, verified downloader/publication path.
The scheduler polls at most once per second only when a retry is waiting; otherwise
it waits for work. Tests use an internal TimeProvider, never a user-controlled
production clock or global settings override.

## Required verification

Exercise due times, no early execution, nonblocking ready work, reload, duplicate
submission, cancellation, permanent/security failures, old journal compatibility,
invalid state preservation and the three-attempt budget. The native owned HTTPS
fixture must fail temporarily, persist/restart, then download/inspect/publish exact
owned media with the real pinned executable. Integration/API and web tests cover
opt-in persistence, authorization, malformed input and scheduled controls. All
format/build/native Linux/macOS gates and packaged preview checks remain required.

Larger source enumeration, playlists/channels/bulk/schedules, configurable resource
and retry policies, source-aware metadata, pause/resume and all remaining1.0 scope
remain mandatory future milestones. This is not full production-importer parity.
