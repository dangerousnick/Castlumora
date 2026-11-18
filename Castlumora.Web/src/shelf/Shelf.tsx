import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { MediaPicker } from '../components/MediaPicker';
import { ActionStatus, useAction } from '../admin/Controls';
import { authenticatedMediaUrl } from '../player/profile';
import type { Item } from '../api/types';
export default function Shelf() {
  const { kind = 'photos' } = useParams();
  const { api, session } = useApp();
  const navigate = useNavigate();
  const [offset, setOffset] = useState(0);
  const [query, setQuery] = useState('');
  const [photos, setPhotos] = useState('PhotoAlbum');
  const [name, setName] = useState('');
  const action = useAction();
  const type = { photos, books: 'Book', collections: 'BoxSet' }[kind] ?? 'Book';
  const title =
    { photos: 'Your life in focus', books: 'Your reading room', collections: 'Your collections' }[
      kind
    ] ?? 'Library';
  const canManage =
    session.user.Policy?.IsAdministrator || session.user.Policy?.EnableCollectionManagement;
  const result = useLoad(
    () =>
      api.items({
        IncludeItemTypes: type,
        SearchTerm: query || undefined,
        StartIndex: offset,
        Limit: 48,
        SortBy: 'SortName',
      }),
    [api, type, query, offset],
  );
  return (
    <main className="page">
      <div className="page-heading">
        <div>
          <span className="eyebrow">YOUR PERSONAL LIBRARY</span>
          <h1>{title}</h1>
        </div>
        <Link to="/browse">Movies & series</Link>
      </div>
      <div className="filters">
        <label>
          Search {kind}
          <input
            type="search"
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setOffset(0);
            }}
          />
        </label>
        {kind === 'photos' && (
          <label>
            Photo view
            <select
              value={photos}
              onChange={(e) => {
                setPhotos(e.target.value);
                setOffset(0);
              }}
            >
              <option value="PhotoAlbum">Albums</option>
              <option value="Photo">All photos</option>
            </select>
          </label>
        )}
      </div>
      {kind === 'collections' && canManage && (
        <form
          className="filters"
          onSubmit={(event) => {
            event.preventDefault();
            void action.run(async () => {
              const result = await api.request<{ Id: string }>(
                'Collections',
                { method: 'POST' },
                { name },
              );
              navigate('/item/' + result.Id);
            }, 'Collection created.');
          }}
        >
          <label>
            New collection name
            <input required value={name} onChange={(e) => setName(e.target.value)} />
          </label>
          <button disabled={action.busy}>Create collection</button>
        </form>
      )}
      <ActionStatus message={action.message} />
      <Feedback busy={result.busy} error={result.error} />
      <div className="poster-grid">
        {result.value?.Items.map((item) => (
          <ShelfCard key={item.Id} item={item} />
        ))}
      </div>
      {!result.busy && !result.value?.Items.length && (
        <p className="empty">No accessible {kind} here yet.</p>
      )}
      <div className="pagination">
        <button disabled={!offset} onClick={() => setOffset(Math.max(0, offset - 48))}>
          Previous page
        </button>
        <span>{result.value?.TotalRecordCount ?? 0} results</span>
        <button
          disabled={
            !result.value || offset + result.value.Items.length >= result.value.TotalRecordCount
          }
          onClick={() => setOffset(offset + 48)}
        >
          Next page
        </button>
      </div>
    </main>
  );
}
export function ShelfCard({ item }: { item: Item }) {
  const { api } = useApp();
  const image = api.image(item);
  return (
    <Link
      className={'shelf-card ' + (item.Type === 'Book' ? 'book-card' : '')}
      aria-label={'Details for ' + item.Name}
      to={(item.Type === 'Photo' ? '/photo/' : '/item/') + item.Id}
    >
      <div>
        {image ? (
          <img src={authenticatedMediaUrl(api, image)} alt="" loading="lazy" />
        ) : (
          <span aria-hidden="true">{item.Type === 'Book' ? '▤' : '▧'}</span>
        )}
      </div>
      <strong>{item.Name}</strong>
    </Link>
  );
}
export function ShelfDetail({ item }: { item: Item }) {
  const { api, session } = useApp();
  const navigate = useNavigate();
  const [offset, setOffset] = useState(0);
  const [refresh, setRefresh] = useState(0);
  const action = useAction();
  const book = item.Type === 'Book';
  const collection = item.Type === 'BoxSet';
  const manage =
    collection &&
    (session.user.Policy?.IsAdministrator || session.user.Policy?.EnableCollectionManagement);
  const download = session.user.Policy?.EnableContentDownloading && item.CanDownload !== false;
  const result = useLoad(
    () =>
      book
        ? Promise.resolve({ Items: [], TotalRecordCount: 0 })
        : api.items({
            ParentId: item.Id,
            Recursive: false,
            IncludeItemTypes: collection ? 'Movie,Series,BoxSet' : 'Photo,PhotoAlbum',
            StartIndex: offset,
            Limit: 48,
            SortBy: 'SortName',
          }),
    [api, item.Id, book, collection, offset, refresh],
  );
  const change = async (id: string, add: boolean) => {
    if (add && id.replaceAll('-', '').toLowerCase() === item.Id.replaceAll('-', '').toLowerCase())
      throw new Error('A collection cannot contain itself.');
    await api.request<void>(
      `Collections/${item.Id}/Items`,
      { method: add ? 'POST' : 'DELETE' },
      { ids: id },
    );
    setRefresh((value) => value + 1);
  };
  return (
    <main className="page">
      <Link to={'/shelf/' + (book ? 'books' : collection ? 'collections' : 'photos')}>
        ← Back to library
      </Link>
      <h1>{item.Name}</h1>
      <p>{item.Overview}</p>
      <ActionStatus message={action.message} />
      {book && (
        <div className="actions">
          <Link className="button primary" to={'/read/' + item.Id}>
            Open reader
          </Link>
          {download && (
            <a
              className="button"
              href={authenticatedMediaUrl(api, api.url(`Items/${item.Id}/Download`).href)}
              rel="noopener noreferrer"
            >
              Download book
            </a>
          )}
        </div>
      )}
      <Feedback busy={result.busy} error={result.error} />
      <div className="poster-grid">
        {result.value?.Items.map((child) => (
          <div key={child.Id}>
            <ShelfCard item={child} />
            {manage && (
              <button
                disabled={action.busy}
                aria-label={'Remove ' + child.Name + ' from collection'}
                onClick={() =>
                  void action.run(() => change(child.Id, false), 'Item removed from collection.')
                }
              >
                Remove
              </button>
            )}
          </div>
        ))}
      </div>
      {manage && (
        <section className="panel">
          <MediaPicker
            label="Add media to collection"
            types="Movie,Series,BoxSet"
            onChoose={(value) =>
              void action.run(() => change(value.Id, true), 'Item added to collection.')
            }
          />
          <button
            disabled={action.busy}
            onClick={() => {
              if (window.confirm('Delete this collection? Its media files are retained.'))
                void action.run(async () => {
                  await api.request<void>(`Items/${item.Id}`, { method: 'DELETE' });
                  navigate('/shelf/collections');
                }, 'Collection deleted.');
            }}
          >
            Delete collection
          </button>
        </section>
      )}
      {!book && (
        <div className="pagination">
          <button disabled={!offset} onClick={() => setOffset(Math.max(0, offset - 48))}>
            Previous page
          </button>
          <span>{result.value?.TotalRecordCount ?? 0} results</span>
          <button
            disabled={
              !result.value || offset + result.value.Items.length >= result.value.TotalRecordCount
            }
            onClick={() => setOffset(offset + 48)}
          >
            Next page
          </button>
        </div>
      )}
    </main>
  );
}
