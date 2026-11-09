import { useMemo, useState } from 'react';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { adminApi } from './api';
import { ActionStatus, SettingsEditor, useAction } from './Controls';
export default function Libraries() {
  const { api } = useApp();
  const admin = useMemo(() => adminApi(api), [api]);
  const [refresh, setRefresh] = useState(0);
  const [selected, setSelected] = useState('');
  const [name, setName] = useState('');
  const [path, setPath] = useState('');
  const [type, setType] = useState('movies');
  const action = useAction();
  const result = useLoad(() => admin.libraries(), [admin, refresh]);
  const library = result.value?.find((l) => l.ItemId === selected);
  return (
    <>
      <h1>Libraries & metadata</h1>
      <p>Organize sources without moving your original media.</p>
      <Feedback busy={result.busy} error={result.error} />
      <ActionStatus message={action.message} />
      <div className="actions">
        <button
          disabled={action.busy}
          onClick={() => void action.run(() => admin.scan(), 'Library scan queued on the server.')}
        >
          Scan all libraries
        </button>
        <button onClick={() => setRefresh((r) => r + 1)}>Refresh status</button>
      </div>
      <div className="admin-card-grid">
        {result.value?.map((l) => (
          <button
            className={selected === l.ItemId ? 'panel selected' : 'panel'}
            key={l.ItemId}
            onClick={() => setSelected(l.ItemId)}
          >
            <strong>{l.Name}</strong>
            <small>{l.CollectionType || 'Mixed media'}</small>
            <small>{l.Locations.join(' · ')}</small>
          </button>
        ))}
      </div>
      {library && (
        <SettingsEditor
          key={library.ItemId}
          title={library.Name + ' options'}
          value={library.LibraryOptions}
          fields={[
            { key: 'EnableRealtimeMonitor', label: 'Monitor folder changes', type: 'boolean' },
            {
              key: 'EnablePhotos',
              label: 'Include photos in home video libraries',
              type: 'boolean',
            },
            { key: 'PreferredMetadataLanguage', label: 'Metadata language' },
            { key: 'MetadataCountryCode', label: 'Metadata country' },
            { key: 'SaveLocalMetadata', label: 'Save metadata beside media', type: 'boolean' },
          ]}
          save={(value) => admin.saveLibrary(library.ItemId, value)}
        />
      )}
      <section className="panel">
        <h2>Add a library</h2>
        <form
          className="settings-fields"
          onSubmit={(e) => {
            e.preventDefault();
            void action.run(async () => {
              await admin.createLibrary(name, type, path);
              setRefresh((r) => r + 1);
              setName('');
              setPath('');
            }, 'Library created; scan requested.');
          }}
        >
          <label>
            Name
            <input required value={name} onChange={(e) => setName(e.target.value)} />
          </label>
          <label>
            Media type
            <select value={type} onChange={(e) => setType(e.target.value)}>
              {[
                ['movies', 'Movies'],
                ['tvshows', 'Television'],
                ['music', 'Music'],
                ['photos', 'Photos'],
                ['books', 'Books'],
                ['homevideos', 'Home videos'],
              ].map(([v, t]) => (
                <option key={v} value={v}>
                  {t}
                </option>
              ))}
            </select>
          </label>
          <label>
            Server folder path
            <input
              required
              value={path}
              onChange={(e) => setPath(e.target.value)}
              placeholder="/media/movies"
            />
          </label>
          <button className="primary" disabled={action.busy}>
            Create library
          </button>
        </form>
      </section>
    </>
  );
}
