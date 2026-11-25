# Castlumora development preview test checklist

Use an isolated installation and copies/authorized fixtures. Record BUILD_INFO.json commit, OS/CPU/RAM/free space, FFmpeg/browser versions and test date. “Prior automated” means source/disposable-server tests before packaging; the release verification report identifies actual archive tests. No physical GPU/tuner/Mint manual testing is implied.

| Feature / location | Steps and expected result | Coverage and limits |
| --- | --- | --- |
| Fresh install | Extract; run start-castlumora; visit :28096/web; create admin; sign in | Actual packaged smoke required; local-only preview data, no service install |
| Branding/navigation | Purple castle; Home/Browse/My List/Search; open native admin | Prior browser checks; compiled UI must load from packaged server |
| Account security | Sign out/in; create limited user; deny admin routes/library not allowed | Inherited auth plus managed-profile API/service tests; use the linked profile checklist below for current build evidence |
| Libraries | Admin → Libraries; add copied Movie/TV/music/photo/book folder; scan; Browse | Prior actual synthetic catalog/browser checks; packaged report distinguishes newly rerun workflows |
| Metadata/artwork | Open movie/episode; verify title/artwork/tracks | Existing providers retained; online enrichment/artwork repair remains unfinished |
| Local creator metadata | Home videos library; retain matching filename.info.json; scan/repair; Browse → Creator & home videos; play | Native strict sidecar ID/type/size/security tests and actual existing-file/lock/conflict/browser/playback checks pass; no downloading or full online provider |
| Metadata Management | Admin → Metadata Management; choose library; toggle/reorder reader/save/reload; search item; correct title/description and lock; unlock/confirmed repair/reload | Native persistence/repair and denied-viewer API workflow pass; fresh complete DTO preserves unrelated fields; bulk/ambiguity/duplicates/artwork editing pending |
| Movie/episode play | Watch now; pause/seek/exit/resume; choose quality/audio/subtitles/chapter | Prior direct/HLS/track browser checks; actual package smoke; codec/device matrix unfinished |
| Music | Browse → Music; album/artist/track; background navigation; private playlist add/move/remove/rename | Prior actual-server tests; lyrics/sharing/large-scale compatibility pending |
| Photos/books | Browse → Photos/Books; rotate/next; PDF page/EPUB chapter; reload position/theme | Prior actual-server browser tests; more ebook formats/offline/annotations pending |
| Collections/My List | Create collection/add/remove/delete; original files remain; add/remove My List | Prior actual-server tests; no file movement/duplication |
| Resume/Up Next | Play part of title; options → remove resume; verify progress retained | Prior preference/menu tests; follows/notifications remain separate and unfinished |
| Live TV/DVR | Admin → Live TV; owned M3U/XMLTV; Guide; play/switch; schedule/cancel; complete/play recording; active Stop; remove vs delete | Actual synthetic complete/partial playback, viewer denials, retained-file removal/rescan and physical deletion pass; real tuners/groups/long duration/quotas pending |
| Skipper | Admin → Skipper enable/analyze/review/approve; play episode; manual/automatic/disabled skip | Prior synthetic detection/browser tests; no intervals until approved; post-credit boundary; broad real-series/native-device accuracy pending |
| Layout/categories | Admin → Presentation; draft/reorder/category/profile scope; publish; reload; restore history to draft | Prior HTTP/browser tests; package persistence smoke; advanced preview/plugin/native renderer work pending |
| Encoding | Admin → Playback/Encoding; inspect codecs/hardware settings | UI/API available; no supported GPU exists here, do not enable unsupported acceleration |
| Plugins/network | Admin → Plugins/System; inspect repos/catalog/network | Native screens retained; real plugin installation, discovery/proxy/TLS deployment pending |
| Logs/tasks/backups | Admin → System; view logs/tasks/triggers/backup controls | Inherited backend retained; destructive actions/restore unverified; back up preview before experimenting |
| Advanced legacy | Existing client opens help unless compatible compiled legacy assets are attached | Not bundled; see INSTALL_TEST_BUILD.md; no false parity claim |
| Managed importer runtime | Admin → Imports & Sources; install bundled executable; check health; reload | Native pinned/hash/version lifecycle implemented; Linux real-binary tests pass; macOS native CI required; Windows advisory; integrated job capability also requires initialized FFmpeg/FFprobe |
| Authorized direct video | Admin → Imports & Sources; choose one-folder Home Videos library; submit owned HTTPS H264/AAC MP4≤32MiB with rights; monitor/cancel/retry; reload/restart; browse/play imported item | Real TLS/native queue/publication/index/media-bytes/restart and role tests pass locally; packaged/UI evidence is recorded separately. Source queries stay private. Full YouTube/playlist/channel/bulk/schedule provider work remains mandatory unfinished work |
| Atomic multi-URL import | Imports & Sources → Import multiple URLs; submit two owned MP4s, then a batch with one private/unsafe URL | Valid batch persists all entries and scans completed media; invalid batch creates no partial jobs. Duplicate references preserve original rights/retries. See batch guide and per-source verification. |
| Opt-in transient retries | Imports & Sources; opt in for an authorized test source with a temporary network/rate failure, observe the scheduled retry time, cancel while waiting and restart | Three total attempts, persisted deadline and cancellation; other ready work proceeds. Permanent/security failures require review. Exact verification is recorded in DEVELOPMENT_PROGRESS.md; use only owned fixtures. |
| Import failure guidance | Imports & Sources; inspect failed owned job; correct cause before manual retry | Fixed redacted codes, persisted failure/reload and safe UI guidance are verified locally; final gate/native CI reported per source. Never disable TLS/SSRF checks or raise limits to bypass errors. |
| Restart/removal | Ctrl+C; restart same state; verify account/layout; stop and delete only preview folder/state | Package smoke required; do not touch original media/another server |

## Report a bug

Open a GitHub issue with exact commit/package/checksum, environment, numbered steps, expected/observed behavior, timestamp, relevant redacted log excerpt and optional screenshot. Include whether a feature was verified/experimental, media codec/container (not copyrighted files), and whether direct or compatible playback failed. Never include passwords, access tokens, private media URLs, databases or invitation links.

## YouTube single-video development workflow

Find Administration → Imports & Sources → Import a YouTube video. Install the
managed runtime and select an eligible Home Videos library first. Preview a video
you own/have permission to retain; verify creator/video identity, quality and size
budget, then confirm rights and queue it. Expect persistent history, cancellation,
bounded retries, creator metadata and library scanning/playback after success.
Changing the source/destination/resource choice requires another preview.

Security check: a loopback/private URL must be rejected, without a confirmation or
new job. This passed in the actual packaged Chromium workflow ate3298e68ce, including the
policy reason, safe guidance, no confirmation and no-job assertions. Native
policy/remux/queue/publication and owned-media API tests passed at application1ac;
public YouTube extraction/download remains unverified because workspace source
access failed/returnedHTTP403. Do not count that restriction as a successful test.
Thumbnails/subtitles, playlists/channels/synchronization and independent online
creator metadata remain unfinished. Report error code/job ID, logs, source
permission/availability, server/browser versions and reproduction steps; omit
passwords, cookies, tokens and private URLs.

## Managed profiles (accounts-first increment)

In Administration → Users & devices create a kids profile under a password-protected
account, choosing explicit libraries. Use the profile menu → Viewing profiles and
the account password to switch. Confirm the kids name survives reload, admin
controls disappear, history/lists/preferences differ from the parent, and returning
to the account again requires its password. Disabled owners must block linked
profiles. Review ratings and blocked unrated content before granting child access.
See the [account/profile guide](https://github.com/dangerousnick/Castlumora/blob/development/castlumora-web-1.3/docs/CASTLUMORA_ACCOUNTS_AND_PROFILES.md) for restrictions, recovery and unverified
areas. Record the exact archive SHA, browser/device, expected/actual behavior and
redacted logs when reporting issues; never attach credentials or the profile key.

For previews declaring managedProfilesStage=administrator-create-switch-delete,
return to Administration → Users & devices, cancel a profile deletion once and
confirm it remains, then confirm deletion. Its identity/history must disappear,
its sessions must be rejected, and its owning account and media playback must
still work. Pending/orphan records can be removed; keep matching backups before
changing real accounts. Actual packaged verification is recorded per exact SHA.

For previews declaring profileMediaPreferencesAvailable=true, open Viewing profiles
→ Media preferences. Choose spa audio, disable source-default audio, choose eng
subtitles/Always, save and reload. With matching tracks, playback should use those
defaults; another profile must retain its own settings. Identity and permissions
must remain unchanged. Remembered per-title choices may override defaults. The
package fixture measures Spanish880Hz audio and actual VTT text; this does not
certify all codecs, real media languages or hardware acceleration.


## Managed profile display-name editing

For previews declaring managedProfilesStage=administrator-create-rename-switch-delete:
open Administration → Users & devices, choose Rename beside a managed profile,
edit Profile display name and Save profile name. Expect a new friendly label with
the same identity/history, policy and media preferences. Switch to it and reload;
the menu must show the server label. Blank/duplicate/control-character/overlong
names must be rejected. A limited profile must receive403 from the admin rename
endpoint. An owning account ID must receive404. No user/key/media should be deleted.
Automated service/HTTP/UI and extracted-package checks cover this increment; see
DEVELOPMENT_PROGRESS.md for the exact tested commit and any pending verification.


## Profile lifecycle and upgrades

For builds declaring profileLifecycleSchema=2, create a profile and confirm normal
switching/media preferences remain available. Administration → Users & devices
shows distinct reasons for interrupted provisioning, pending deletion and legacy
review records. These states must remain unavailable and no Reactivate/Recover
button should appear. Do not force a failure on a live installation to test it.
Automated private-file/service/HTTP tests cover interrupted operations, v1 migration,
matching/conflicting snapshots, invalid states, keys/policy preservation and storage
budgeting. The normal packaged browser test covers fresh-v2 profiles and existing
playback/security workflows; full installed-system restore/upgrade is not certified.
Always retain a matching database/configuration/key backup before trying an upgrade.


## Interrupted managed-profile recovery (development)

Find it under Administration → Users & devices → Managed viewing profiles. Only
explicit interrupted provisioning with an existing identity offers Recover. Confirm
the action, check the same identity becomes available, switch with the owner password,
and verify retained lists/preferences. Active/deleting/legacy-review/orphan records
must not offer recovery. Missing keys/libraries/owners must produce a useful error
without silently creating replacements. Never edit live production private files to
manufacture this condition; report a real interruption with redacted diagnostics.

Automated coverage: focused service failure/reopen tests and real SQLite HTTP
role/override/preservation tests passed locally. UI confirmation/availability tests
passed. The dedicated package workflow stops only its disposable server, records
interrupted intent, restarts, recovers through the interface, and restarts again to
verify durable identity/viewing data; consult the exact build's CI results for pass
status. No full installed-system restore or hardware certification is implied.


## Self-service account password

Profile menu → Viewing profiles → Account security (owning account only). With a
second browser signed in to the same account, change your password using the
current password and matching new entries. Expect this browser to remain signed
in, the other sign-in to require authentication again, the old password to fail,
and the new password to work. Do not use another person's account for this test.
Managed profiles must not show this editor; account-edit permission restrictions
remain enforced by the server. Managed-profile sessions are separate and are not
claimed to be reset by this identity-scoped operation. Password fields should clear
on success/error and should not appear in application local/session storage.

Focused UI tests passed; the newly extended extracted-package workflow must pass
before the feature is listed as verified for a particular downloadable build.

## Personal saved sign-ins (consult exact-build progress)

1. Open Viewing profiles → My signed-in devices. Verify only the current identity's
   saved sign-ins appear and the current sign-in is marked.
2. Sign in as the same identity on a second device/browser. Refresh the first;
   the second should appear with its name, app/version and activity, without tokens.
3. Cancel Sign out this device once; the second must still work. Confirm it;
   its old token must fail while the first and another account remain signed in.
4. Create two more sign-ins, confirm Sign out other devices. Only this sign-in
   remains; other accounts/profiles are unaffected. Use ordinary Sign out for this one.
5. With more than twenty saved sign-ins, use Next/Previous devices. Errors must
   not show successful revocation. Older servers should offer an honest fallback.

Focused component and real HTTP tests cover controls and authorization; actual
packaged single/bulk browser assertions extend the retained profile workflow.
See DEVELOPMENT_PROGRESS.md for which exact build has passed them. Parent-wide
profile revocation remains unfinished. Report bugs with exact commit, OS/browser,
reproduction and redacted logs/screenshots; never share credentials or tokens.

Exact-source verification: [personal sign-in report](https://github.com/dangerousnick/Castlumora/blob/bdc4bd4cf8369d272fc83ff01eca1ee51dcf8b83/docs/verification-personal-sign-ins.md)
records passing complete local/hosted checks, actual compiled-browser/cold-restart
proof and the downloadable sourceebab7567cfa7Linux package with checksum.


## First recording in a fresh library

The ebab7567 preview exposed a known first-recording indexing defect. Check
[the correction report linked from PR3](https://github.com/dangerousnick/Castlumora/pull/3) and current progress
for the NEW fixed build before testing this case.

1. On a disposable test installation, configure an authorized synthetic/local tuner
   and XMLTV guide, then select a new empty recording folder.
2. Schedule a short program. After completion it must appear automatically in
   Recordings without a manual scan, and must play.
3. A viewer without management/deletion permission must be denied cancellation
   and deletion. Library-only removal retains the file; an explicit rescan can
   restore it. Confirmed file deletion removes only that recording.
4. Start another recording, wait for actual data, then Stop recording before its
   scheduled end. Its partial file should remain indexed and playable.

Source82e78af32455 passed the complete local/hosted gate and actual NEW downloaded
self-contained-package setup, DVR lifecycle/active stop, HLS/audio/subtitles,
cold-restart retained-media access and clean shutdown on Debian13/FFmpeg7.1.5.
See PR3's exact-source post-build report. Physical Mint/NAS/GPU/tuner checks remain
unverified, and this is a development preview rather than stable1.0 acceptance.

## Owner management of viewing-profile devices

1. As the owning account, open Viewing profiles → Viewing profile devices and
   choose a child. My signed-in devices continues to show your own sign-ins.
2. Sign into that child on two test devices. Refresh the owner's page; both should
   appear without tokens. Cancel one confirmation, then confirm Sign out this device.
   The selected device must require authentication, while the other child device
   and owner still work.
3. Confirm Sign out all profile devices, including when only one remains. That
   child must require authentication everywhere; owner and sibling profiles stay in.
4. Verify profile history/preferences/identity remain. A child cannot see owner
   controls. Another account or API key must not list or revoke this profile.
5. An unavailable profile may have old sign-ins revoked without becoming active.

Focused service/real HTTP/component tests passed during implementation. Consult
DEVELOPMENT_PROGRESS.md for the exact complete-gate and packaged-browser result;
new assertions are included in the existing actual profile workflow, not a skipped
placeholder. No stable release or completion of all account features is claimed.

## Account-wide other-device sign-out

In a preview explicitly containing this increment, sign in an owner on two devices
and switch two additional devices into different owned viewing profiles. Keep a
separate account signed in. Under Viewing profiles, cancel **Sign out other account
and profile devices** once: all remain authorized. Confirm it: the current owner
stays signed in, its other device and both profiles must authenticate again, and
the unrelated account stays signed in. Reload and confirm profile watch history
and identities remain unchanged. Child and API-key requests must be denied, and
an account-ID override must be rejected. Record the tested commit and whether this
was automated or manually checked. Exact package availability is in progress docs;
older previews do not claim these controls.

## Administrator profile access editing

In a preview declaring profileAccessEditingAvailable, create an owned synthetic movie
library and a second empty library. Create an adult viewing profile, record a played
item and media preferences, then sign in that profile on another device. In Users &
devices choose Edit access, select Kids and only the second library. Cancel once:
no permissions/tokens change. Confirm: old profile devices require authentication;
new profile sign-ins cannot see the first library's movie. Owner/sibling sessions
remain valid. Reopen and restore the original preset/libraries: the same profile ID,
played history and language preferences return. Test stale edits by changing owner
permissions in another admin session; expect409 and review/reopen, not an overwrite.
Ordinary users/API keys must be denied and manually disabled identities remain
unavailable. Interrupted updates require explicit recovery; never recreate the
profile or key. Record source/archive SHA and automated/manual status when reporting
issues. Consult DEVELOPMENT_PROGRESS.md for which exact package has passed.

## Followed series

For previews declaring followedSeriesAvailable, open a visible TV series and follow
it. My List must show it in Followed series independently of the watchlist. Reload
and restart the server: membership remains. Unfollow from the list/details; cancel
clear-all once, then confirm it. Other profile lists and watch history must remain.
Repeat under a managed profile to verify separation. Restrict the library/rating:
unavailable titles disappear, follow/status access is denied, and safe unfollow or
clear-all can remove retained references. Movies cannot be followed; API keys and
user/account overrides must be rejected. Record exact package SHA, expected/actual
behavior, redacted logs and whether verification was automated/manual. Notifications
are separate unfinished work; following alone does not promise notifications.

## Episode notification center

Profile menu → Notifications. Follow a visible TV show, enable notifications and
confirm existing episodes do not alert. Add/scan an authorized new episode, refresh
and open its notification. Mark read/unread, restart, then dismiss and refresh.
Expect no duplicate alerts and unchanged playback/watchlist state. A second viewing
profile must have independent settings/state; revoke library access and confirm
entries and unread counts disappear. Pause/resume baselines missed additions.
Detection is on-refresh; background/email/push/recording/separate season alerts
remain unavailable. See CASTLUMORA_NOTIFICATIONS.md for bounds and test evidence.

## Filtered Surprise Me and rerolling

Use the header **Surprise Me** button. Select Movies or Episodes, an allowed library,
and Any watch status/Unwatched/Watched. Choose a title; verify it fits those filters.
Reroll to get a different recent pick; exhaust a small fixture and use Start over
to include previous picks again. Open picked details or deliberately choose Play pick.
Selection/rerolls must not change played progress or watchlists. Empty matches and
denied requests must show an honest result/error, never a fallback outside the
selected scope. Switch viewing profile and confirm filters/pick history reset and
only that profile's media is available. Last50successful picks are excluded per
filter set; this temporary history is separate from real watched history. No
automatic playback or persistent discovery preferences are implied. Verification
evidence and the exact preview source are recorded in DEVELOPMENT_PROGRESS.md.
