import { useMemo, useState } from 'react';
import { sameId } from '../api/client';
import type { User } from '../api/types';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { adminApi } from './api';
import type { ObjectData } from './api';
import { ActionStatus, SettingsEditor, useAction } from './Controls';
export default function Users() {
  const { api, session } = useApp();
  const admin = useMemo(() => adminApi(api), [api]);
  const [selected, setSelected] = useState('');
  const [refresh, setRefresh] = useState(0);
  const [name, setName] = useState('');
  const [password, setPassword] = useState('');
  const [currentPassword, setCurrentPassword] = useState('');
  const action = useAction();
  const users = useLoad(() => api.request<User[]>('Users'), [api, refresh]);
  const user = useLoad(
    () =>
      selected
        ? api.request<User & { Policy: ObjectData; Configuration: ObjectData }>(`Users/${selected}`)
        : Promise.resolve(undefined),
    [api, selected, refresh],
  );
  const devices = useLoad(
    () =>
      api.request<{
        Items: {
          Id: string;
          Name: string;
          AppName: string;
          LastUserName?: string;
          DateLastActivity?: string;
        }[];
      }>('Devices'),
    [api, refresh],
  );
  return (
    <>
      <h1>Users, access & devices</h1>
      <Feedback busy={users.busy} error={users.error} />
      <ActionStatus message={action.message} />
      <div className="filters">
        <label>
          Viewing account
          <select value={selected} onChange={(e) => setSelected(e.target.value)}>
            <option value="">Choose a user…</option>
            {users.value?.map((u) => (
              <option key={u.Id} value={u.Id}>
                {u.Name}
              </option>
            ))}
          </select>
        </label>
      </div>
      {user.value && (
        <>
          <SettingsEditor
            key={selected + 'policy'}
            title={user.value.Name + ' permissions'}
            value={user.value.Policy}
            fields={[
              { key: 'IsAdministrator', label: 'Administrator', type: 'boolean' },
              { key: 'IsDisabled', label: 'Disable account', type: 'boolean' },
              { key: 'EnableMediaPlayback', label: 'Allow playback', type: 'boolean' },
              { key: 'EnableLiveTvAccess', label: 'Live TV access', type: 'boolean' },
              { key: 'EnableLiveTvManagement', label: 'DVR management', type: 'boolean' },
              { key: 'EnableContentDownloading', label: 'Allow downloads', type: 'boolean' },
              {
                key: 'EnableVideoPlaybackTranscoding',
                label: 'Video transcoding',
                type: 'boolean',
              },
              {
                key: 'EnableAudioPlaybackTranscoding',
                label: 'Audio transcoding',
                type: 'boolean',
              },
              { key: 'EnableAllFolders', label: 'All libraries', type: 'boolean' },
              { key: 'MaxParentalRating', label: 'Maximum parental rating', type: 'number' },
              {
                key: 'MaxActiveSessions',
                label: 'Maximum active sessions (0 = unrestricted)',
                type: 'number',
              },
            ]}
            save={(value) => admin.savePolicy(selected, value)}
          />
          <SettingsEditor
            title={user.value.Name + ' playback preferences'}
            value={user.value.Configuration}
            fields={[
              { key: 'AudioLanguagePreference', label: 'Preferred audio language' },
              { key: 'SubtitleLanguagePreference', label: 'Preferred subtitle language' },
              { key: 'EnableNextEpisodeAutoPlay', label: 'Autoplay next episode', type: 'boolean' },
            ]}
            save={(value) =>
              api.request<void>(`Users/${selected}/Configuration`, {
                method: 'POST',
                body: JSON.stringify(value),
              })
            }
          />
          <form
            className="panel"
            onSubmit={(e) => {
              e.preventDefault();
              void action.run(async () => {
                await admin.setPassword(selected, password, currentPassword || undefined);
                setCurrentPassword('');
                setPassword('');
              }, 'Password changed.');
            }}
          >
            <h2>Change password</h2>
            {sameId(selected, session.user.Id) && (
              <label>
                Current password
                <input
                  type="password"
                  autoComplete="current-password"
                  required
                  value={currentPassword}
                  onChange={(e) => setCurrentPassword(e.target.value)}
                />
              </label>
            )}
            <label>
              New password
              <input
                type="password"
                autoComplete="new-password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </label>
            <button disabled={action.busy}>Set password</button>
          </form>
        </>
      )}
      <section className="panel">
        <h2>Create user</h2>
        <form
          className="filters"
          onSubmit={(e) => {
            e.preventDefault();
            void action.run(async () => {
              const created = await admin.createUser(name);
              setSelected(created.Id);
              setRefresh((r) => r + 1);
              setName('');
            }, 'User created. Set a password and review permissions.');
          }}
        >
          <label>
            Username
            <input required value={name} onChange={(e) => setName(e.target.value)} />
          </label>
          <button disabled={action.busy}>Create account</button>
        </form>
      </section>
      <section className="panel">
        <h2>Devices</h2>
        <Feedback busy={devices.busy} error={devices.error} />
        <div className="admin-list">
          {devices.value?.Items.map((d) => (
            <article key={d.Id}>
              <span>
                <strong>{d.Name}</strong>
                <small>
                  {d.AppName} · {d.LastUserName} ·{' '}
                  {d.DateLastActivity && new Date(d.DateLastActivity).toLocaleString()}
                </small>
              </span>
              <button
                disabled={action.busy}
                onClick={() => {
                  if (confirm('Remove this device and revoke its stored access?'))
                    void action.run(async () => {
                      await api.request('Devices', { method: 'DELETE' }, { id: d.Id });
                      setRefresh((r) => r + 1);
                    }, 'Device removed.');
                }}
              >
                Remove device
              </button>
            </article>
          ))}
        </div>
      </section>
    </>
  );
}
