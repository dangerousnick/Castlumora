import { useState } from 'react';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { SettingsEditor, ActionStatus, useAction } from './Controls';
import { PolicyEditor } from './PolicyEditor';
import { registrationApi } from '../registration/api';
import type { RegistrationSettings } from '../registration/api';
export default function Registration() {
  const { api } = useApp();
  const [refresh, setRefresh] = useState(0);
  const result = useLoad(() => registrationApi(api).admin(), [api, refresh]);
  const action = useAction();
  const [name, setName] = useState('');
  const [expires, setExpires] = useState(() =>
    new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 16),
  );
  const [maxUses, setMaxUses] = useState(1);
  const [link, setLink] = useState('');
  const reload = () => setRefresh((v) => v + 1);
  const save = async (settings: RegistrationSettings) => {
    await registrationApi(api).saveSettings(settings);
    reload();
  };
  return (
    <div className="page admin">
      <h1>Registration & invitations</h1>
      <p>
        Self-registration is optional. Only administrators create or revoke invitations. Signup
        never creates an administrator; existing server policies remain authoritative.
      </p>
      <Feedback busy={result.busy} error={result.error} />
      {result.value && (
        <>
          <SettingsEditor
            title="registration settings"
            value={result.value.settings}
            fields={[
              { key: 'enabled', label: 'Enable self-registration', type: 'boolean' },
              { key: 'requireInvitation', label: 'Require an invitation', type: 'boolean' },
            ]}
            save={(value) => save(value as RegistrationSettings)}
          />
          <PolicyEditor
            title="Default signup permissions"
            value={result.value.settings.defaultPolicy}
            allowAdministrator={false}
            save={(policy) => save({ ...result.value!.settings, defaultPolicy: policy })}
          />
          <section className="panel">
            <h2>Create invitation</h2>
            <p>
              Each invitation captures the current default signup policy. The link is shown once;
              its raw token is not saved. Registration must be enabled to use it.
            </p>
            <form
              onSubmit={(event) => {
                event.preventDefault();
                void action.run(async () => {
                  const created = await registrationApi(api).createInvitation({
                    name,
                    expiresAt: new Date(expires + 'Z').toISOString(),
                    maxUses,
                  });
                  const url = new URL(window.location.href);
                  url.hash =
                    '/register?' +
                    new URLSearchParams({ server: api.server, invite: created.token });
                  setLink(url.href);
                  setName('');
                  reload();
                }, 'Invitation created. Copy the one-time link before leaving this page.');
              }}
            >
              <div className="admin-fields">
                <label>
                  Invitation label
                  <input
                    required
                    maxLength={120}
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                  />
                </label>
                <label>
                  Expires (UTC)
                  <input
                    type="datetime-local"
                    required
                    value={expires}
                    onChange={(e) => setExpires(e.target.value)}
                  />
                </label>
                <label>
                  Maximum successful registrations
                  <input
                    type="number"
                    min="1"
                    step="1"
                    required
                    value={maxUses}
                    onChange={(e) => setMaxUses(Number(e.target.value))}
                  />
                </label>
              </div>
              <button className="primary" disabled={action.busy}>
                Create invitation
              </button>
            </form>
            {link && (
              <label>
                One-time invitation link
                <textarea readOnly value={link} rows={3} onFocus={(e) => e.target.select()} />
              </label>
            )}
          </section>
          <ActionStatus message={action.message} />
          <section>
            <h2>Invitations</h2>
            <div className="admin-cards">
              {result.value.invitations.map((invite) => (
                <article className="panel" key={invite.id}>
                  <h3>{invite.name}</h3>
                  <p>
                    {invite.successfulUses} / {invite.maxUses} registrations · {invite.reservedUses}{' '}
                    pending
                  </p>
                  <p>
                    Expires {new Date(invite.expiresAt).toLocaleString()} ·{' '}
                    {invite.revoked
                      ? 'Revoked'
                      : new Date(invite.expiresAt) <= new Date()
                        ? 'Expired'
                        : 'Active'}
                  </p>
                  <button
                    disabled={action.busy || invite.revoked}
                    onClick={() =>
                      void action.run(async () => {
                        await registrationApi(api).revoke(invite.id);
                        reload();
                      }, 'Invitation revoked for future registrations.')
                    }
                  >
                    Revoke {invite.name}
                  </button>
                </article>
              ))}
            </div>
            {!result.value.invitations.length && <p>No invitations yet.</p>}
          </section>
          <section>
            <h2>Registration audit & recovery</h2>
            <p>
              Interrupted attempts keep their reservation. Inspect the matching account before
              resolving; this action never enables or deletes an account. Release is allowed only
              after a matching account has been removed.
            </p>
            <div className="admin-cards">
              {[...result.value.registrations].reverse().map((entry) => (
                <article className="panel" key={entry.id}>
                  <h3>{entry.username}</h3>
                  <p>
                    {entry.status} · {new Date(entry.at).toLocaleString()}
                  </p>
                  {['reserved', 'provisioning', 'cleanupRequired'].includes(entry.status) && (
                    <div className="actions">
                      <button
                        disabled={action.busy}
                        onClick={() => {
                          if (
                            window.confirm(
                              'Count this reservation as a completed registration? This does not enable the account.',
                            )
                          )
                            void action.run(async () => {
                              await registrationApi(api).resolve(entry.id, true);
                              reload();
                            }, 'Reservation counted as completed.');
                        }}
                      >
                        Count as completed
                      </button>
                      <button
                        disabled={action.busy}
                        onClick={() =>
                          void action.run(async () => {
                            await registrationApi(api).resolve(entry.id, false);
                            reload();
                          }, 'Reservation released after cleanup.')
                        }
                      >
                        Release after cleanup
                      </button>
                    </div>
                  )}
                </article>
              ))}
            </div>
          </section>
        </>
      )}
    </div>
  );
}
