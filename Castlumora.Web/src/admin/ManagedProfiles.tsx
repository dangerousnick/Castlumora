import { useState } from 'react';
import { useApp, useLoad } from '../state';
import type { User } from '../api/types';
import { Feedback } from '../components/Media';
import { ActionStatus, useAction } from './Controls';

export default function ManagedProfiles({ onCreated }: { onCreated: () => void }) {
  const { api } = useApp();
  const [owner, setOwner] = useState('');
  const [name, setName] = useState('');
  const [preset, setPreset] = useState('kids');
  const [folders, setFolders] = useState<string[]>([]);
  const action = useAction();
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
            onCreated();
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
        <ActionStatus message={action.message} />
      </form>
    </section>
  );
}
