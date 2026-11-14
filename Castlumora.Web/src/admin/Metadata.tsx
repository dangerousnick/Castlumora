import { useState } from 'react';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { ActionStatus, useAction } from './Controls';
import { adminApi } from './api';
import type { LibraryInfo } from './api';
import type { MetadataItem } from './metadataApi';
import { metadataApi, sidecarReader } from './metadataApi';

function ReaderSettings({ library, changed }: { library: LibraryInfo; changed: () => void }) {
  const { api } = useApp();
  const action = useAction();
  const disabled = library.LibraryOptions.DisabledLocalMetadataReaders;
  const [enabled, setEnabled] = useState(
    !Array.isArray(disabled) ||
      !disabled.some((name) => String(name).toLowerCase() === sidecarReader.toLowerCase()),
  );
  const original = library.LibraryOptions.LocalMetadataReaderOrder;
  const [order, setOrder] = useState<string[]>([
    ...new Set([
      ...(Array.isArray(original) && original.length
        ? original.filter((v): v is string => typeof v === 'string')
        : ['Nfo']),
      sidecarReader,
    ]),
  ]);
  return (
    <section className="panel">
      <h2>{library.Name}: local metadata readers</h2>
      <p>
        The YouTube sidecar reader identifies generic creator/home videos offline. It does not
        reclassify movies or download media. Saving sets an explicit library-level reader order;
        unlisted readers follow their default relative order.
      </p>
      <label>
        <input
          type="checkbox"
          disabled={action.busy}
          checked={enabled}
          onChange={(e) => setEnabled(e.target.checked)}
        />
        Enable YouTube sidecar reader
      </label>
      <ol>
        {order.map((name, index) => (
          <li key={name}>
            {name}{' '}
            <button
              disabled={index === 0 || action.busy}
              aria-label={'Move ' + name + ' up'}
              onClick={() =>
                setOrder((values) => {
                  const next = [...values];
                  [next[index - 1], next[index]] = [next[index], next[index - 1]];
                  return next;
                })
              }
            >
              Move up
            </button>
            <button
              disabled={index === order.length - 1 || action.busy}
              aria-label={'Move ' + name + ' down'}
              onClick={() =>
                setOrder((values) => {
                  const next = [...values];
                  [next[index + 1], next[index]] = [next[index], next[index + 1]];
                  return next;
                })
              }
            >
              Move down
            </button>
          </li>
        ))}
      </ol>
      <button
        disabled={action.busy}
        onClick={() =>
          void action.run(async () => {
            await metadataApi(api).saveReaders(library.ItemId, enabled, order);
            changed();
          }, 'Reader settings saved on the server.')
        }
      >
        Save reader settings
      </button>
      <ActionStatus message={action.message} />
    </section>
  );
}

export function MetadataEditor({ item, changed }: { item: MetadataItem; changed: () => void }) {
  const { api } = useApp();
  const action = useAction();
  const [title, setTitle] = useState(item.Name);
  const [description, setDescription] = useState(item.Overview ?? '');
  const [locked, setLocked] = useState(Boolean(item.LockData));
  const dirty =
    title !== item.Name ||
    description !== (item.Overview ?? '') ||
    locked !== Boolean(item.LockData);
  return (
    <section className="panel">
      <h2>Selected media metadata</h2>
      <p>
        Type: {item.Type}.{' '}
        {item.Type === 'Video'
          ? 'Generic creator/home video; explicit source identity is preferred.'
          : 'Existing media type is preserved; the local creator reader does not reclassify this item.'}
      </p>
      <p>
        {item.ProviderIds?.YouTube
          ? 'YouTube video ID: ' + item.ProviderIds.YouTube
          : 'No reliable YouTube identity stored. A title alone is not a source match.'}
      </p>
      {item.ProviderIds?.YouTubeChannel && (
        <p>Stable channel ID: {item.ProviderIds.YouTubeChannel}</p>
      )}
      <p>
        Locked fields: {item.LockedFields?.join(', ') || 'None'}. Field locks are retained when
        saving.
      </p>
      <form
        className="settings-fields"
        onSubmit={(e) => {
          e.preventDefault();
          void action.run(async () => {
            await metadataApi(api).saveCorrections(item.Id, {
              Name: title,
              Overview: description,
              LockData: locked,
            });
            changed();
          }, 'Corrections and lock saved on the server.');
        }}
      >
        <label>
          Metadata title
          <input
            required
            disabled={action.busy}
            maxLength={1000}
            value={title}
            onChange={(e) => setTitle(e.target.value)}
          />
        </label>
        <label htmlFor="metadata-description">Description</label>
        <textarea
          id="metadata-description"
          disabled={action.busy}
          maxLength={65536}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />
        <label>
          <input
            type="checkbox"
            disabled={action.busy}
            checked={locked}
            onChange={(e) => setLocked(e.target.checked)}
          />
          Lock manual metadata corrections
        </label>
        <button disabled={action.busy || !dirty || !title.trim()}>Save metadata corrections</button>
      </form>
      {dirty && <p>Save your edits before refreshing metadata.</p>}
      <button
        disabled={action.busy || dirty}
        onClick={() =>
          void action.run(async () => {
            await metadataApi(api).refresh(item.Id, false);
          }, 'Refresh queued. Reload metadata after the server finishes.')
        }
      >
        Refresh metadata
      </button>
      <button
        disabled={action.busy || dirty || locked}
        onClick={() => {
          if (
            window.confirm(
              'Repair metadata from configured sources? This can replace unlocked fields. Existing media files and field locks are preserved.',
            )
          )
            void action.run(async () => {
              await metadataApi(api).refresh(item.Id, true);
            }, 'Source repair queued. Reload metadata after the server finishes.');
        }}
      >
        Repair from source
      </button>
      <button disabled={action.busy || dirty} onClick={changed}>
        Reload metadata
      </button>
      <ActionStatus message={action.message} />
    </section>
  );
}

export default function Metadata() {
  const { api } = useApp();
  const [refresh, setRefresh] = useState(0);
  const [libraryId, setLibraryId] = useState('');
  const [query, setQuery] = useState('');
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState('');
  const libraries = useLoad(() => adminApi(api).libraries(), [api, refresh]);
  const items = useLoad(
    () =>
      search
        ? api.items({
            SearchTerm: search,
            IncludeItemTypes: 'Video,Movie,Series,Episode,MusicVideo',
            Limit: 20,
          })
        : Promise.resolve({ Items: [], TotalRecordCount: 0 }),
    [api, search],
  );
  const item = useLoad(
    () => (selected ? metadataApi(api).item(selected) : Promise.resolve(undefined)),
    [api, selected, refresh],
  );
  const library = libraries.value?.find((value) => value.ItemId === libraryId);
  const reload = () => setRefresh((value) => value + 1);
  return (
    <div className="page">
      <h1>Metadata Management</h1>
      <p>
        Source-aware local metadata and manual corrections. Online enrichment, ambiguity review,
        bulk repair and duplicate management are not yet implemented.
      </p>
      <Feedback
        busy={libraries.busy || items.busy || item.busy}
        error={libraries.error || items.error || item.error}
      />
      <label htmlFor="metadata-library">Metadata library</label>
      <select
        id="metadata-library"
        value={libraryId}
        onChange={(e) => setLibraryId(e.target.value)}
      >
        <option value="">Choose a library</option>
        {libraries.value?.map((value) => (
          <option key={value.ItemId} value={value.ItemId}>
            {value.Name}
          </option>
        ))}
      </select>
      {library && (
        <ReaderSettings key={library.ItemId + ':' + refresh} library={library} changed={reload} />
      )}
      <form
        className="search-box"
        onSubmit={(e) => {
          e.preventDefault();
          setSearch(query.trim());
        }}
      >
        <label>
          Find media to correct
          <input
            required
            minLength={2}
            maxLength={256}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
        </label>
        <button>Search metadata</button>
      </form>
      {items.value && (
        <div className="admin-cards">
          {items.value.Items.map((value) => (
            <button key={value.Id} onClick={() => setSelected(value.Id)}>
              {value.Name} · {value.Type}
            </button>
          ))}
        </div>
      )}
      {search && items.value && (
        <p>
          {items.value.TotalRecordCount} matches; showing the first 20. Refine the search to narrow
          results.
        </p>
      )}
      {item.value && (
        <MetadataEditor key={item.value.Id + ':' + refresh} item={item.value} changed={reload} />
      )}
    </div>
  );
}
