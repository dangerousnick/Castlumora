# Authorized direct-MP4 batch imports

Administration → Imports & Sources → Import multiple URLs accepts one to25 direct
HTTPS MP4 URLs, a title prefix, a real eligible Home Videos library and confirmed
rights. All setup presets share the feature. The server endpoint also accepts an
explicit title for each entry: POST Castlumora/Imports/Jobs/Batch, with entries
containing url/title and common destinationId/rights/rightsConfirmed/automaticRetries.
Requests are administrator-only, bounded to196608bytes and25 entries. Source paths,
executables, extra entry options and arbitrary filesystem destinations are rejected.

Atomic means **all-or-none queue acceptance**, not a transaction across downloaded
media files. Every entry is validated before one private atomic journal save. The
existing1000-job/8MiB bounds apply to the whole batch. Save/capacity/input failure
rolls back every new entry; existing jobs and the retained journal are preserved.
Duplicates within the batch or existing pending/completed history reuse the original
job ID, title, rights, requester and retry policy. Responses contain one reference
per entry, so duplicate references do not imply extra downloads.

Downloads subsequently run independently through the existing single worker and
safe publication path. Queue/history controls cancel/retry individual jobs. Rights,
requester, destination, optional transient backoff, restart recovery, source/content
deduplication, private provenance, native media validation and automatic library
scanning retain their existing safeguards. All files remain within the current
32MiB/H264/AAC direct-video preview limits. Metadata and source queries stay private.

Validation covers25-entry reload/execution, unsafe final entries, empty/26-entry
inputs, concurrent duplicate submissions, real capacity and filesystem-save failure,
submission cancellation, administrator/viewer denial and actual owned native HTTPS
imports/library indexing/restart/content deduplication. The packaged browser test
also checks an unsafe mixed batch leaves no partial jobs, then imports two public
owner-authorized GPL fixture URLs with exact hash/bytes. DEVELOPMENT_PROGRESS.md
and PR3 record which checks have actually finished for the published source.

This does not enable YouTube page, playlist/channel enumeration, scheduled sources,
full bulk migrations, format selection or all resource controls. Those and the
independent creator metadata provider remain mandatory next milestones. Linux is
the only supported server target for1.0; inherited cross-platform code is retained.
