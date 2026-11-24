# Followed series

On a visible TV series detail page choose **Follow series**. My List has a separate
**Followed series** section; its watchlist remains independent. Unfollow from details
or the list. **Stop following all series** requires confirmation and also removes
references to currently unavailable shows. It never deletes media or watch history.

Following belongs to the current viewing identity, including managed profiles. A
parent's list is not inherited by children. Current native library/rating visibility
is checked on follow and every list/status read. Unavailable titles are omitted;
retained private references can be removed safely without granting access. Following
does not yet generate episode/season notifications or personalized recommendations.

API: GET Castlumora/Following (startIndex>=0, limit1–100) returns visible seriesIds
and total. GET /{seriesId} returns followed only for a visible series. PUT /{seriesId}
accepts only {"enabled":true|false}; enabling requires a currently visible Series,
while disabling can remove an unavailable reference. POST /Clear accepts only {}.
Authentication supplies the viewing identity; API keys/identity overrides are denied.
Responses are no-store. Existing generic Preferences mutations cannot set following.

Persistence reuses castlumora/user-{viewing-user-ID}.json and its existing atomic
writer/shared gate, with at most1000references. Duplicate updates are idempotent;
watchlist/resume preferences remain independent. Corrupt preferences return503 and
are not silently reset. Explicit clear can repair an invalid following field while
preserving other readable preferences; malformed whole-file JSON requires repair.
Preserve backups/configuration when repairing; no new database or credential changes.

See DEVELOPMENT_PROGRESS.md for exact-source tests, download/checksum and limitations.
The automated fixture uses owned synthetic series media; physical Mint/NAS devices
and production-sized libraries are not certified by these tests.
