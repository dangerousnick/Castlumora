# Registration and administrator invitations

Registration is optional and disabled by default. Administrators control whether an invitation is required and which non-administrator policy new accounts receive. The default grants no libraries/channels, downloading, public sharing, Live TV management or shared-device control; administrators must choose access explicitly. Existing server permission enforcement remains authoritative.

## Workflow

Use Administration → Registration & invitations to enable signup, set default permissions and choose allowed libraries. Each invitation snapshots that default policy. The administrator supplies a future expiry and a positive maximum number of successful registrations. The API also accepts an administrator-supplied per-invitation policy. Signup cannot create administrators; administrators grant elevated roles separately through existing account management.

Invitation links are returned/displayed once. A cryptographically random 256-bit token is stored only as a SHA-256 digest. Copy the link before leaving the page; the server cannot recover its raw token. Revocation rejects future attempts. Settings changes do not silently rewrite previously issued invitation policies.

The public link opens the independent Web application's registration page and identifies the server. The visitor confirms that address before entering account details. The token is removed from the browser URL after being read into memory; it is submitted only in the JSON request body. Passwords and raw invitation tokens are not put in local/session storage or the registration journal. Signup issues no authentication token: the account must sign in normally. A policy can require administrator activation.

After initial pairing, the frontend reconnects the saved server address automatically. Losing a tab session still requires sign-in; this convenience does not bypass authentication or store a password. Change server remains available.

## Provisioning and recovery

An additive IUserManager overload creates an account with its disabled permission already persisted. The old creation method retains its behavior; a custom manager that does not implement disabled provisioning fails closed rather than falling back to unsafe account creation.

Signup durably reserves an invitation use, creates the disabled account, sets its password through the existing manager, applies the server-owned policy while disabled, records successful consumption, then applies the intended activation state. Authentication/password-reset providers come from the actual newly created account. Access-schedule templates are cloned and bound to that account, rather than carrying another user's ownership/IDs. Request-supplied policy/administrator fields are ignored by the public endpoint.

Counters are serialized and include outstanding durable reservations, so concurrent signup cannot exceed the invitation cap. Failed provisioning deletes the disabled partial account and releases its use where cleanup succeeds. Failed cleanup/process interruption retains a reservation for administrator inspection. An interruption after durable completion can leave an account requiring manual activation; inspect it in Users & devices.

The private audit shows username, time, invitation and status. Recovery actions never enable/delete an account. Count as completed requires a matching account; release requires that the matching account has already been removed. Administrators inspect/reset/enable or delete that account through ordinary protected management. Completed historical uses remain consumed when an account is later deleted.

State is atomically replaced in `<ConfigurationDirectoryPath>/castlumora/registration.json`. Include it in backup/restore checks. One server process owns the configuration; shared multi-process writes are unsupported. Invitations/audit are retained, so large deployments should plan retention/export and pagination work.

## Contracts and abuse boundaries

- Anonymous `GET Castlumora/Registration`: only enabled/invitation-required flags.
- Anonymous `POST Castlumora/Registration`: username, password and optional invitation token. Maximum body 16KiB, password 8–1024 characters, ordinary server username validation. Errors do not disclose token validity or account existence details. Five attempts/minute per normalized client address, bounded in-memory address windows; HTTP 429 includes Retry-After. Configure trusted proxies through inherited server networking.
- Administrator `GET .../Admin`: settings, redacted invitations and private audit. Token digests are excluded too.
- Administrator `PUT .../Settings`: optimistic settings revision and non-administrator default policy.
- Administrator `POST .../Invitations`, `DELETE .../Invitations/{id}`: create/revoke.
- Administrator `POST .../Reservations/{id}/Resolve?consume=...`: explicit recovery.

These are security/operation controls, not paid editions or feature paywalls. Existing accounts, authentication providers, namespaces, wire identifiers and database structures are retained; no migration is introduced.

## Verification and remaining scope

Service tests exercise concurrency/caps, expiry/revocation, closed/open registration, administrator-policy rejection, token/password secrecy, disabled provisioning order, failure cleanup/recovery, schedule ownership, settings conflicts and attempt-window reset. Existing user-manager regressions verify persisted disabled creation and unchanged legacy behavior.

Actual HTTP checks include blank-password denial, successful password sign-in, malicious public policy fields being ignored, no default library access, administrator-only invitation/settings/audit and throttling. The guarded disposable-server Chromium flow creates an invitation through the UI, registers a visitor, signs in, verifies access to Fixture Movies but not Fixture Skipper/administration, and verifies remembered pairing with an empty password after tab-session removal. It deletes its synthetic account, revokes the invitation and restores settings.

Run that opt-in browser case with the documented disposable credentials, CASTLUMORA_TEST_REGISTRATION=1 and Vite running. It requires the `.castlumora-disposable` marker and a System/Info ProgramDataPath matching `<fixture-root>/data` (default root `/tmp/castlumora-web-real-server`, override CASTLUMORA_TEST_FIXTURE_ROOT).

```bash
npm run test:browser -- browser/real-server.spec.ts -g 'administrator invitation'
```

This implements registration/invitation lifecycle and explicit library policy editing. Linked multiple viewing profiles per account, named kids/adult presets, paired-device refresh/revocation improvements and optional resource tiers remain separate unfinished requirements. No production server, Coon Media deployment or automatic merge is part of this work.
