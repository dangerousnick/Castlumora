# Episode notification center

Open the profile menu → **Notifications**, then **Enable episode notifications**.
It belongs to the current viewing identity, including a managed child profile.
Follow TV shows from their detail pages first. Enabling baselines existing visible
episodes; the first observation of each newly followed show also baselines its catalog.
There are no historical alerts for those baselines. Opening or choosing **Refresh
notifications** compares the current permission-filtered native library snapshot with
previous observations. A newly observed episode of an already observed followed show
produces an alert, even when its source file's creation date is old. No title matching,
external service or filename-date assumption is used.

Open the episode through its notification link, mark read/unread, or dismiss it.
These actions do not mark episodes played, change watch history, or modify watchlists.
Read/dismissal and observation IDs survive server restarts; repeated refreshes do not
recreate alerts. Pause retains existing notifications; resuming baselines the current
catalog so additions during the pause do not flood the center. Unfollowed, removed,
wrong-type or inaccessible episodes and their unread counts are omitted on every
request. Retained references never grant access. New alerts are detected on refresh,
not through background push. Episodes added and removed between observations cannot
be detected. Email, push, separate season alerts and recording alerts remain unfinished.

API: GET Castlumora/Notifications returns enabled, items and a visible-only unread
count. PUT /Settings accepts only {"enabled":boolean}. PUT /{episodeId} accepts exactly
{"read":boolean} or {"dismissed":true}. Authentication supplies the viewing identity;
API keys and body identity overrides are denied. Responses are no-store. Both series
and episode visibility are checked using native library permissions. No copied titles,
paths, artwork, credentials or owner identifiers are persisted in notification state.

Private storage reuses PresentationStore's serialized atomic writer, in
castlumora/notifications-{viewing-user-ID}.json with schemaVersion1. It is separate
from watchlist/following and resume preferences. Retain it with server configuration
backups. The initial bounded implementation retains the newest500 notification records,
including dismissed records, and up to20000 observed episode IDs across1000 shows per
viewing identity. Evicted notifications are not recreated because observations remain.
Oversized snapshots or corrupt/over-capacity state fail explicitly with503 without
silently replacing the file or claiming a successful update. Administrators should
preserve configuration for review; this is not a production-scale retention/reset tool.

See DEVELOPMENT_PROGRESS.md for exact tested commits and preview evidence. This
notification center is one development milestone; it does not complete personalization
or the complete Phase1.4 requirements.
