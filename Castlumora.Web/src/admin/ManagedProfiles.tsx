import { useState } from 'react';
import { sameId } from '../api/client';
import { useApp, useLoad } from '../state';
import type { User } from '../api/types';
import { Feedback } from '../components/Media';
import { ActionStatus, useAction } from './Controls';

export default function ManagedProfiles({ onChanged }: { onChanged: () => void }) {
  const { api } = useApp();
  const [owner, setOwner] = useState('');
  const [name, setName] = useState('');
  const [preset, setPreset] = useState('kids');
  const [folders, setFolders] = useState<string[]>([]);
  const action = useAction();
  const [refresh, setRefresh] = useState(0);
  const [editing, setEditing] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [accessEditing, setAccessEditing] = useState('');
  const [accessPreset, setAccessPreset] = useState('kids');
  const [accessFolders, setAccessFolders] = useState<string[]>([]);
  const [accessRevision, setAccessRevision] = useState('');
  const managed = useLoad(
    () =>
      api.request<{
        recoveryAvailable?: boolean;
        accessEditingAvailable?: boolean;
        profiles: {
          id: string;
          name: string;
          accountName: string;
          preset: string;
          ready: boolean;
          lifecycle?: string;
          libraries?: string[];
          accessRevision?: string | null;
          exists: boolean;
          disabled?: boolean;
        }[];
      }>('Castlumora/Profiles/Managed'),
    [api, refresh],
  );
  const users = useLoad(() => api.request<User[]>('Users'), [api]);
  const libraries = useLoad(
    () => api.request<{ ItemId: string; Name: string }[]>('Library/VirtualFolders'),
    [api],
  );
  return (
    <section className="panel">
      <h2>Managed viewing profiles</h2>
      <p>
        Create up to ten profiles under an enabled, password-protected account. Each uses a separate
        restricted server identity for its own history and preferences.
      </p>
      <p>
        Kids starts with hidden sign-in, restricted rated media and blocked unrated content; Live
        TV, sharing and downloads are off. Adult also starts with explicitly selected libraries.
        Review policies before use.
      </p>
      <Feedback busy={users.busy || libraries.busy} error={users.error || libraries.error} />
      <form
        className="settings-fields"
        onSubmit={(event) => {
          event.preventDefault();
          void action.run(async () => {
            await api.request('Castlumora/Profiles', {
              method: 'POST',
              body: JSON.stringify({ accountId: owner, name, preset, libraries: folders }),
            });
            setName('');
            setFolders([]);
            setRefresh((value) => value + 1);
            onChanged();
          }, 'Profile created. Review its policy below or switch from the profile menu.');
        }}
      >
        <label>
          Owning account
          <select required value={owner} onChange={(event) => setOwner(event.target.value)}>
            <option value="">Choose an account</option>
            {users.value?.map((user) => (
              <option key={user.Id} value={user.Id}>
                {user.Name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Profile name
          <input
            required
            maxLength={40}
            value={name}
            onChange={(event) => setName(event.target.value)}
          />
        </label>
        <label>
          Profile preset
          <select value={preset} onChange={(event) => setPreset(event.target.value)}>
            <option value="kids">Kids</option>
            <option value="adult">Adult</option>
          </select>
        </label>
        <fieldset>
          <legend>Allowed profile libraries</legend>
          {libraries.value?.map((folder) => (
            <label key={folder.ItemId}>
              <input
                type="checkbox"
                checked={folders.includes(folder.ItemId)}
                onChange={(event) =>
                  setFolders(
                    event.target.checked
                      ? [...folders, folder.ItemId]
                      : folders.filter((id) => id !== folder.ItemId),
                  )
                }
              />
              {folder.Name}
            </label>
          ))}
        </fieldset>
        <button disabled={action.busy || !owner || !name.trim() || !folders.length}>
          Create viewing profile
        </button>
      </form>
      <ActionStatus message={action.message} />
      <h3>Existing managed profiles</h3>
      <Feedback busy={managed.busy} error={managed.error} />
      {managed.value?.profiles.length === 0 && <p>No managed profiles.</p>}
      <ul>
        {managed.value?.profiles.map((profile) => (
          <li key={profile.id}>
            <p>
              {profile.name} · {profile.accountName} · {profile.preset}
            </p>
            <p>
              {profile.ready
                ? 'Available'
                : profile.lifecycle === 'provisioning'
                  ? 'Provisioning interrupted — unavailable; preserve for recovery or remove'
                  : profile.lifecycle === 'deleting'
                    ? 'Deletion pending — unavailable; retry removal'
                    : profile.lifecycle === 'review'
                      ? 'Legacy pending profile — unavailable; administrator review required'
                      : 'Unavailable — review permissions or remove'}
            </p>
            {editing === profile.id ? (
              <form
                className="settings-fields"
                onSubmit={(event) => {
                  event.preventDefault();
                  void action.run(async () => {
                    await api.request('Castlumora/Profiles/Managed/' + profile.id + '/Name', {
                      method: 'PUT',
                      body: JSON.stringify({ name: displayName }),
                    });
                    setEditing('');
                    setRefresh((value) => value + 1);
                    onChanged();
                  }, 'Profile name saved. Identity, history and permissions preserved.');
                }}
              >
                <label>
                  Profile display name
                  <input
                    required
                    maxLength={40}
                    value={displayName}
                    onChange={(event) => setDisplayName(event.target.value)}
                  />
                </label>
                <button disabled={action.busy || !displayName.trim()}>Save profile name</button>
                <button type="button" disabled={action.busy} onClick={() => setEditing('')}>
                  Cancel name edit
                </button>
              </form>
            ) : (
              <button
                disabled={action.busy}
                onClick={() => {
                  setAccessEditing('');
                  setEditing(profile.id);
                  setDisplayName(profile.name);
                }}
              >
                Rename {profile.name}
              </button>
            )}
            {managed.value?.accessEditingAvailable &&
              profile.exists &&
              profile.lifecycle === 'active' &&
              !profile.disabled &&
              accessEditing !== profile.id && (
                <button
                  disabled={action.busy || !profile.accessRevision}
                  onClick={() => {
                    setEditing('');
                    setAccessEditing(profile.id);
                    setAccessPreset(profile.preset);
                    setAccessFolders(profile.libraries || []);
                    setAccessRevision(profile.accessRevision || '');
                  }}
                >
                  Edit access for {profile.name}
                </button>
              )}
            {accessEditing === profile.id && (
              <form
                className="settings-fields"
                aria-label={`Access settings for ${profile.name}`}
                onSubmit={(event) => {
                  event.preventDefault();
                  if (
                    !window.confirm(
                      `Apply ${accessPreset} preset and selected libraries to ${profile.name}? This replaces custom permissions within the owner's limits and signs out this profile's devices. Identity and viewing history are retained.`,
                    )
                  )
                    return;
                  void action.run(async () => {
                    try {
                      await api.request('Castlumora/Profiles/Managed/' + profile.id + '/Access', {
                        method: 'PUT',
                        body: JSON.stringify({
                          preset: accessPreset,
                          libraries: accessFolders,
                          revision: accessRevision,
                        }),
                      });
                      setAccessEditing('');
                      onChanged();
                    } finally {
                      setRefresh((value) => value + 1);
                    }
                  }, 'Profile access updated. Devices signed out; identity and viewing history retained.');
                }}
              >
                <p>
                  Reapply a preset with explicit libraries. Custom profile permissions are replaced
                  within the current owner's limits; other profiles and accounts are unchanged.
                </p>
                {profile.lifecycle !== 'active' && (
                  <p>
                    Setup is interrupted. Preserve this profile and use its recovery action before
                    editing again.
                  </p>
                )}
                <fieldset
                  disabled={action.busy || profile.lifecycle !== 'active' || profile.disabled}
                >
                  <legend>Profile access</legend>
                  <label>
                    Updated profile preset
                    <select
                      value={accessPreset}
                      onChange={(event) => setAccessPreset(event.target.value)}
                    >
                      <option value="kids">Kids</option>
                      <option value="adult">Adult</option>
                    </select>
                  </label>
                  <fieldset>
                    <legend>Access libraries</legend>
                    {libraries.value?.map((folder) => (
                      <label key={folder.ItemId}>
                        <input
                          type="checkbox"
                          checked={accessFolders.some((id) => sameId(id, folder.ItemId))}
                          onChange={(event) =>
                            setAccessFolders(
                              event.target.checked
                                ? [...accessFolders, folder.ItemId]
                                : accessFolders.filter((id) => !sameId(id, folder.ItemId)),
                            )
                          }
                        />
                        {folder.Name}
                      </label>
                    ))}
                  </fieldset>
                  <button
                    disabled={
                      !accessFolders.length ||
                      accessFolders.length > 100 ||
                      !accessRevision ||
                      libraries.busy ||
                      !!libraries.error
                    }
                  >
                    Apply profile access
                  </button>
                </fieldset>
                <button type="button" disabled={action.busy} onClick={() => setAccessEditing('')}>
                  Cancel access edit
                </button>
              </form>
            )}
            {managed.value?.recoveryAvailable &&
              !profile.ready &&
              profile.exists &&
              profile.lifecycle === 'provisioning' && (
                <button
                  disabled={action.busy}
                  onClick={() => {
                    if (
                      !window.confirm(
                        `Resume interrupted setup of ${profile.name}? Its original identity, history and preferences will be retained. Current owner and library restrictions are rechecked.`,
                      )
                    )
                      return;
                    void action.run(async () => {
                      await api.request('Castlumora/Profiles/Managed/' + profile.id + '/Recover', {
                        method: 'POST',
                        body: JSON.stringify({}),
                      });
                      setRefresh((value) => value + 1);
                      onChanged();
                    }, 'Profile recovered. Original identity retained; review its current policy before switching.');
                  }}
                >
                  Recover {profile.name}
                </button>
              )}
            <button
              disabled={action.busy}
              onClick={() => {
                if (
                  window.confirm(
                    'Delete this managed profile and its viewing history? Its owning account and media files are preserved.',
                  )
                )
                  void action.run(async () => {
                    await api.request('Castlumora/Profiles/Managed/' + profile.id, {
                      method: 'DELETE',
                    });
                    setRefresh((value) => value + 1);
                    onChanged();
                  }, 'Managed profile removed. Account and media files preserved.');
              }}
            >
              {profile.exists ? 'Delete ' : 'Remove orphan entry '}
              {profile.name}
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}
