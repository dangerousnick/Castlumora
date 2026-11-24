# Accounts and managed viewing profiles

Owner priority (2026-11-14): continue accounts through release readiness; defer
further YouTube importing and creator-metadata development. Existing work remains.

## First managed-profile increment

An account remains an inherited server user with its normal password and policy.
An administrator can create up to ten managed viewing profiles underneath an
enabled, password-protected account in **Administration → Users & devices →
Managed viewing profiles**. Choose a display name, kids/adult preset and explicit
existing libraries. A managed profile is a separate inherited user identity, so
watch history, playback progress, watchlists and presentation/skipping preferences
use their existing independent server-side user keys. No database identifiers,
password authentication, GPL notices or existing account migration are replaced.

Use the profile menu → **Viewing profiles** to switch. Every switch requires the
owning account password and uses full inherited session authentication, including
remote/device/schedule/session policy. The previous identity's token is revoked;
the browser replaces its session and clears the old identity's component caches.
The account password and parent token are not retained for switching back. Each
device switches independently. Profile IDs alone cannot authorize a switch.

Kids initially permits only low-rated content (maximum score 6) and blocks every
unrated media kind. Both presets require explicit libraries; neither grants
administrator, Live TV/DVR, sharing, deletion, conversion or management rights.
Adult downloading is bounded by the owning account. Remote access, transcoding,
ratings/tags, devices, bitrate, session limits and schedules inherit the owner's
bounds. Review the media's ratings and profile policy before children use it.

Managed identity names/passwords and core account configuration are administrator
controlled. The displayed name is distinct from the reserved internal username.
Normal profile watch progress/lists and Castlumora presentation preferences remain
independent. Audio/subtitle defaults have a separate self-scoped media editor; other core
configuration still requires the administrator's existing editor. No PIN-only switch or retained
parent credential bypass is provided. Existing standalone accounts still work.

The server checks linked identity ownership on authenticated requests. Disabled or
missing owners, disabled/admin-elevated managed identities, lost private keys,
unsafe configuration and child policies exceeding owner bounds fail closed.
Tightening an owner policy can require an administrator to review the linked
profile's inherited policy before its sessions are allowed again. Ordinary
inherited users and API keys retain their existing authentication behavior.

## Profile-specific media defaults

Viewing profiles → Media preferences edits preferred audio/subtitle language,
source-default-audio choice and subtitle behavior for the current identity only.
Use ISO language/language-region codes (eng, spa, en-US), blank for automatic, or
OriginalLanguage for original audio. Turn source-default audio off to prefer a
matching language. Existing remembered per-title choices can override defaults;
manual player choices remain available. Some tracks require permitted compatible
streaming. Missing matching tracks fall back through inherited server selection.

The endpoint derives the user from authentication and rejects identity, password,
permission and target-user fields. It changes only the four media columns instead
of replacing a stale full configuration. It does not re-enable identity editing.
Ordinary account preference restrictions and managed owners' restrictions remain.
The player respects server subtitle defaults and routes alternate preferred tracks
through compatible streaming when direct MP4 cannot select them reliably.

The automated package fixture uses owned English440Hz/Spanish880Hz audio plus VTT;
tests measure decoded browser audio and actual cue text, not just saved settings.
Consult the exact checkpoint for which package has passed these new assertions.

## Durable lifecycle and registry migration

The private profile registry uses version2. New provisioning reserves the identity,
`provisioning` lifecycle and explicit requested libraries before password/policy
updates. Success records `active` and clears the pending library plan. Deletion
records `deleting` and clears that plan before disabling/revoking/removing the child.
A failed operation keeps its original identity and unavailable state. Display-name
editing does not change lifecycle or permissions.

On first read of version1, the server preserves its exact bytes as private
`profiles.v1.json`, then atomically migrates the registry. Ready profiles remain
active; all legacy pending profiles become `review`, because the old format cannot
distinguish interrupted creation from deletion. Migration never enables a user,
changes a password/key, recreates an identity or alters history/policy. The original
credential derivation remains unchanged. Unknown or inconsistent version2 states
fail closed. A conflicting or linked legacy snapshot requires administrator review;
neither registry nor snapshot is overwritten. A matching snapshot allows a safe
retry after an interrupted migration.

Administration shows provisioning interrupted, deletion pending, or legacy review
as distinct unavailable states. **Recover [profile name]** is available only for an
existing identity with explicit version2 provisioning intent. An administrator must
confirm it; the server rechecks the enabled/password-protected owner, the original
restricted managed identity, currently available/owner-permitted libraries and the
existing private key. It disables the child and revokes its sessions before repairing
the original derived password and owner-bounded preset. Only a durable completed
registry update marks it available. Identity, display name, key, history, watchlists
and media/presentation preferences remain; account passwords are not requested or
changed. Switching afterward still requires the owner's password normally.

Recovery never creates a replacement user/key, guesses a missing library, accepts
policy/identity overrides, or reactivates active, deletion-pending or ambiguous
legacy-review records. Unknown IDs return404; unsafe states return409; damaged
configuration/failed repair returns503 and remains unavailable/retryable. Preserve
all private files and correct the problem before retrying. Do not use recovery to
undo an administrator's deliberate disablement of an active profile. Preset/library
editing remains separate unfinished work. Ordinary account administration remains
available if profile-only configuration is damaged.

Back up the user database and the entire private profile directory before upgrading.
Both registries and the key remain private; configuration backup includes the legacy
snapshot and restore reapplies its private Linux permissions. Registry2 is not
readable by old previews: do not downgrade by blindly overwriting files or creating
a new key. Restore a matching database/configuration/key backup under administrator
review; the preserved snapshot alone does not represent later profile changes.

## Administrator lifecycle and cleanup

Administration → Users & devices lists managed profiles with owning account,
preset and availability, including pending records and identities already removed
through inherited administration. Only authenticated administrators can enumerate
this inventory, rename or delete a managed profile. Viewing users cannot list other
accounts' managed identities or delete themselves through these endpoints.

Use **Rename** → **Profile display name** → **Save profile name** to correct its
friendly label. Names are trimmed, limited to forty characters and unique within
one account. Only the private ownership registry label changes: the reserved core
username, ID, password, key, permissions, viewing history and sessions are retained.
The profile menu reloads the current server label after a browser reload rather
than trusting a stale cached label. This action does not repair pending provisioning
or change presets/libraries; those remain separate recovery/policy tasks.

The Delete action requires confirmation because it removes that profile's viewing
identity/history. It preserves the owning account and media files. An owning
account or arbitrary ordinary user ID is never accepted by the managed-delete
endpoint. Existing standalone-account administration remains available separately.

Removal first durably marks the mapping unavailable, disables the child and revokes
its sessions. It deletes the inherited child identity before durably removing the
mapping. An interrupted/failed deletion remains unavailable and retryable; if the
user was already deleted, retry removes only its orphan mapping. Remaining profile
keys/owners are preserved. Reload the inventory after changes made through another
client or inherited administration. Pending provisioning can be removed safely;
editing/re-enabling a partially provisioned profile remains a separate recovery task.

## Persistent state, backup and recovery

Include the entire private `config/castlumora/profiles/` directory in server
configuration backups alongside the normal user database. `profiles.json` stores
versioned ownership/display names/provisioning state; `credentials.key` is a
private random 32-byte key used to derive managed passwords. Files are mode 0600
and the private directory 0700 on Linux. Never publish the key, passwords or tokens.
Registry writes are bounded, atomic and durable; symbolic-link paths are rejected.
Built-in backups now include this directory; Linux archives are private mode 0600
and restore reapplies private profile-file/directory modes. The archive-content test
reads actual registry/key entries alongside the unchanged production-reserve tests.
Pause profile provisioning during backups and keep database/configuration snapshots
together. A full installed-system restore remains a separate release-readiness test.

Provisioning creates a disabled user before writing its ownership reservation and
setting its secret/policy. Interrupted provisioning remains pending/unavailable;
it does not silently grant a half-created identity access. Preserve the disabled
user and configuration, restore matching backups, and have an administrator
investigate. Do not blindly repeat creation, delete the key or generate a new key
for existing managed profiles. Preserve reserved managed usernames during repair.
There is no new public production threshold or authentication override.

## Verification and remaining scope

Focused service tests cover persistence/reopen, private key storage, owner bounds,
ordinary-account preservation during registry damage, missing-registry recovery,
wrong passwords, foreign targets, admin escalation, missing keys, disabled-first
provisioning and use of full authentication before old-token revocation. Real HTTP
coverage exercises creation/switching, separate preferences, administrator denial,
identity-edit denial, old-token rejection and owner-disable enforcement. The
packaged browser workflow covers native administration, switching/reload, separated
preferences and return-to-account authentication. Consult DEVELOPMENT_PROGRESS.md
for the exact checks actually run and source SHA; test presence alone is not a pass.

Remaining account work includes managed-profile preset/library editing and non-destructive
pending recovery UI, broader preference/device coverage, invitation/registration edge-case
review, account-wide device/session controls and broader multi-device/media-history
verification. Inherited administrative user-policy controls remain available.
External-auth-provider account compatibility is unverified; the initial workflow
requires an existing local password. This increment does not complete stable 1.0.


## Shared-device logout isolation

A logout ends only live sessions matching the revoked authorization record's user,
application and device. A supplied device ID alone is not authority over another
account or client. This preserves other accounts' activity and avoids ending a new
viewing profile's session while revoking its previous identity on the same browser.
Existing per-user/device token replacement, token revocation and administrator
device removal behavior remain; no authentication/database identifiers change.

Two regression cases failed on the inherited behavior before correction, then all
22focused session-manager tests passed. A real SQLite HTTP workflow confirms the
revoked token returns401 while another account and another app/device retain their
sessions/tokens, inspecting session inventory before requests could recreate them.
Consult the latest checkpoint for complete gate and packaged evidence. A dedicated
self-service session/device page is still a separate unfinished milestone.


## Change your account password

Open the profile menu → Viewing profiles → **Account security** while signed in to
the owning account. Enter the current password, a new password of8–1024characters,
and matching confirmation. If the server explicitly reports a passwordless account,
leave the current field blank to set its first password. Server account-edit
permissions still apply; contact the administrator when changes are disabled.

The form targets only the signed-in account through the inherited authenticated
password API, with an explicit self ID so administrator self-changes also verify
the current password. No reset/empty-password bypass, user selection or policy
change is exposed. Managed profiles do not display this editor; switch to the
owning account rather than replacing their private derived credentials.

The current token remains active; other sign-ins for this identity are revoked by
the existing server. Managed viewing profiles are separate identities/sessions,
so this does not claim an account-wide profile-session reset. Full device/session
management remains unfinished. Password fields are cleared after success or error
and are not written by Castlumora to local/session storage. A failed network
response cannot prove whether the server already applied the change.

Seven focused form tests plus both profile-switch tests passed locally, including
permission/ownership limits, confirmation, passwordless setup, error clearing and
secret-storage checks. The packaged profile workflow now also checks wrong-current-
password403, successful change, preserved current token, revoked second token,
old/new password sign-in behavior, cleared fields/storage and fixture restoration.
Consult the latest checkpoint for actual packaged verification status.

## Personal saved sign-ins

Viewing profiles → My signed-in devices shows the current identity's saved
sign-ins, including offline devices. Names, app versions and last activity are
untrusted display text, never executable markup. The current sign-in is marked;
use ordinary Sign out to end it. Confirm Sign out this device for one other
sign-in, or Sign out other devices to retain only this sign-in. Other accounts
and managed viewing profiles are separate identities and remain unaffected.
Inventory is paginated, twenty rows per page, and refreshes after successful
revocation. Older servers without the endpoint show an upgrade/administrator hint.

`GET Castlumora/Account/Sessions` accepts nonnegative startIndex and limit1–100.
Responses contain record IDs, display metadata, current marker and total, never
access tokens or physical device IDs. The authenticated identity determines scope;
query parameters cannot select another account. API keys are forbidden.
`DELETE Castlumora/Account/Sessions/{id}` returns404 for foreign/unknown records,
409 for the caller's current token, and204 after inherited scoped logout.
`POST Castlumora/Account/Sessions/RevokeOthers` requires an empty JSON object and
revokes only this identity's other tokens. Inventory is explicitly no-store.
Existing authentication, profile authorization and database compatibility remain.

Focused real HTTP tests exercise two ordinary accounts sharing a physical device,
selected and bulk revocation, current retention, unauthorized and API-key denial,
pagination, and secret-free results. UI tests cover confirmation, errors, older
servers, pagination shrinkage and escaped client names. Packaged browser checks
exercise real list/single/bulk controls and token validity. Check current progress
for actual complete-gate and packaged verification status; parent-wide profile
session management, remote physical-device certification and stable readiness are
not claimed by this increment.


## Account-owner controls for viewing-profile sign-ins

The owning account can choose a child under Viewing profiles → Viewing profile
devices. It may revoke one saved sign-in or all devices for that child; its own and
sibling sessions remain. An unavailable child can have old tokens revoked without
reactivation. Profile identity, key, lifecycle, preferences and history are preserved.
The existing My signed-in devices section still manages only the current identity.

GET Castlumora/Account/ProfileSessions/{profileId} accepts bounded pagination like
personal sessions and returns only display metadata and record IDs, never tokens
or physical device IDs. DELETE its /{recordId} revokes only an owned child record;
POST /RevokeAll requires an empty JSON object. The authenticated caller must be an
enabled owning identity, not a managed child or API key. Admin status alone does
not grant access to another account's children. Unknown/foreign targets return404,
child/machine callers403, invalid body/page bounds400. No target account override
is accepted. Ownership checks and operations share the registry lifecycle lock.

See DEVELOPMENT_PROGRESS.md for actual verification/publication status. The current
increment is not a claim that all account or Phase1.4 requirements are complete.
