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

## Administrator lifecycle and cleanup

Administration → Users & devices lists managed profiles with owning account,
preset and availability, including pending records and identities already removed
through inherited administration. Only authenticated administrators can enumerate
this inventory or delete a managed profile. Viewing users cannot list other
accounts' managed identities or delete themselves through these endpoints.

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

Remaining account work includes dedicated managed-profile editing and non-destructive
pending recovery UI, broader preference/device coverage, invitation/registration edge-case
review, account-wide device/session controls and broader multi-device/media-history
verification. Inherited administrative user-policy controls remain available.
External-auth-provider account compatibility is unverified; the initial workflow
requires an existing local password. This increment does not complete stable 1.0.
