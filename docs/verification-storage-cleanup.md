# Storage cleanup verification — 2026-11-10

## Result

Safe cleanup reclaimed 1.59GiB. Available space increased
from 2.64GiB to 4.23GiB on the same
workspace filesystem. The unchanged backup test and publishing gate require
5GiB free; the remaining gap is approximately 0.77GiB before
additional rebuild/test headroom. Safe deletion alone has not unblocked validation.
No backup test was retried below its known prerequisite. No test or gate was weakened.

## What was cleaned

- Regenerable bin outputs and Debug/Release obj intermediates for tracked projects,
  retaining backup-test binaries/metadata and the custom analyzer. NuGet restore
  metadata remains available.
- Frontend node_modules/dist and disposable browser outputs; these regenerate from
  the committed package lock and source.
- Obsolete compiled preview cache archives whose source commits were verified to
  exist in Git. Original checksum/report metadata was retained.
- Completed temporary Jellyfin test data and temporary image-conversion dependencies;
  all committed branding source/assets remain intact.
- 35 unused NuGet package versions absent from all41 current project.assets.json
  graphs, plus npm/pip download caches. Current required package versions and SDK
  remain installed. Future self-contained publishing may redownload runtime packs.

165 candidate paths were removed. Detailed path/reason audit:
/tmp/castlumora-storage-cleanup-report.json. Hard-linked dependency files made apparent
bin sizes misleading; actual filesystem free space, not summed directory sizes,
was used to measure the1.59GiB recovery.

## What was preserved and checked

- Commit0ad2f94608d7efbe5d2f0c9c07ed1e39c7ca4edf, all Git history/refs, application
  source and development documentation. Git HEAD and clean status matched before
  and immediately after cleanup. Only this report/progress documentation is added
  afterwards; no application, test or gate code changes.
- Existing unpublished recovery bundle verified successfully; its HEAD remains0ad2f94608.
- Both protected previews and the runtime extraction remain intact. SHA256 verified:
  cafc archive33f3a924efa5557985f4d3ff4ac99f350bac87b4f98a2ea9dd643bdd35c5a67f;
  584e archivea229ad90f93de09276cbb77d6f1e1d54257a5ff0725dbcadf4f01664b9b14e9e.
- Required backup-test assembly/dependencies retained. .NET SDK10.0.403 still starts.
- All previous validation logs and the real pinned yt-dlp fixture remain available.
  No active test/build process was stopped, and no production server was accessed.

An initial cleanup command was rejected before execution by automatic approval
review because it included the protected runtime-preview archive. The revised plan
excluded that entire archive directory, its extraction and the newer cafc preview.
No rejected deletion was retried indirectly; protected previews were retained.

## Infrastructure limitation and recovery

The previously observed failure was the existing static StorageHelper free-space
guard in BackupService, before backup serialization. Cleanup did not modify that
code or the corruption regression test. This is an infrastructure prerequisite
failure, not evidence of an application defect; backup behavior remains unverified
locally until the real test runs with enough storage.

Remaining large inputs include roughly1.7GiB of required NuGet package files,
624MiB of .NET SDK, and protected previews. No further comparable disposable cache
was found. Deleting required build inputs would cause them to be restored by the
unchanged gate, so it would not sustainably solve validation capacity.

Provide at least5GiB free after dependencies are restored, plus build/test headroom;
about2GiB additional writable capacity is a practical minimum for this checkpoint.
Once capacity exists, rerun the exact backup test once, then the entire mandatory
gate before any push. Restore cleaned frontend dependencies with npm ci as part of
the gate. Do not skip the backup test, lower its threshold or bypass the pre-push hook.
No GitHub push was made during this cleanup task.
