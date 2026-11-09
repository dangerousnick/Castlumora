import { useState } from 'react';
import { Link, useLocation, useSearchParams } from 'react-router-dom';
import { Search } from 'lucide-react';
import { useApp, useLoad } from '../state';
import { Feedback, Poster } from '../components/Media';
export default function Browse() {
  const { api, preferences, layout } = useApp();
  const path = useLocation().pathname;
  const [params, setParams] = useSearchParams();
  const [type, setType] = useState('Movie,Series');
  const [library, setLibrary] = useState('');
  const [page, setPage] = useState(0);
  const search = params.get('q') ?? '';
  const categoryId = params.get('category');
  const category = layout.categories.find((c) => c.id === categoryId);
  const list = path === '/list';
  const searching = path === '/search';
  const views = useLoad(() => api.views(), [api]);
  const result = useLoad(async () => {
    if (category) return api.categoryPage(category.id, page * 48, 48, search || undefined);
    if (list && !preferences.watchlist.length) return { Items: [], TotalRecordCount: 0 };
    return api.items({
      IncludeItemTypes: type,
      ParentId: library || undefined,
      SearchTerm: search || undefined,
      Ids: list ? preferences.watchlist.join(',') : undefined,
      StartIndex: page * 48,
      SortBy: 'SortName',
      SortOrder: 'Ascending',
    });
  }, [api, type, library, page, search, category, list, preferences]);
  return (
    <main className="page">
      <div className="page-heading">
        <div>
          <span className="eyebrow">A WORLD WORTH EXPLORING</span>
          <h1>
            {searching
              ? 'Find your next favorite'
              : list
                ? 'My List'
                : (category?.title ?? 'Browse your world')}
          </h1>
          <p>
            {list
              ? 'Saved for the perfect moment.'
              : category
                ? 'Curated by your server. Always your media.'
                : 'Pick a story. Make it a night.'}
          </p>
        </div>
        <Link to="/shelf/photos" className="button secondary">
          Photos
        </Link>
        <Link to="/shelf/books" className="button secondary">
          Books
        </Link>
        <Link to="/shelf/collections" className="button secondary">
          Collections
        </Link>
        <Link to="/music" className="button secondary">
          Music & playlists
        </Link>
        <Link to="/live" className="button secondary">
          Live TV & guide
        </Link>
      </div>
      {searching && (
        <form className="search-box" onSubmit={(e) => e.preventDefault()}>
          <Search />
          <input
            type="search"
            aria-label="Search media"
            autoFocus
            placeholder="Movies, series, and more…"
            value={search}
            onChange={(e) => {
              setPage(0);
              setParams({ q: e.target.value });
            }}
          />
        </form>
      )}
      {!list && !category && (
        <div className="filters">
          <label className="sr-only" htmlFor="media-type">
            Media type
          </label>
          <select
            id="media-type"
            value={type}
            onChange={(e) => {
              setType(e.target.value);
              setPage(0);
            }}
          >
            <option value="Movie,Series">Movies & series</option>
            <option value="Movie">Movies</option>
            <option value="Series">TV shows</option>
          </select>
          <label className="sr-only" htmlFor="library">
            Library
          </label>
          <select
            id="library"
            value={library}
            onChange={(e) => {
              setLibrary(e.target.value);
              setPage(0);
            }}
          >
            <option value="">All libraries</option>
            {views.value?.Items.map((i) => (
              <option key={i.Id} value={i.Id}>
                {i.Name}
              </option>
            ))}
          </select>
          <Link to={api.legacy()} target="_blank" rel="noopener noreferrer">
            Music, photos & more ↗
          </Link>
        </div>
      )}
      <Feedback busy={result.busy} error={result.error} />
      {result.value && (
        <>
          <div className="poster-grid">
            {result.value.Items.map((item) => (
              <Poster key={item.Id} item={item} />
            ))}
          </div>
          {result.value.Items.length === 0 && (
            <div className="empty">
              <h2>{list ? 'Your list is waiting' : 'No stories found'}</h2>
              <p>
                {list
                  ? 'Save a title from its details or options menu.'
                  : 'Try another search or library.'}
              </p>
            </div>
          )}
          {
            <div className="pagination">
              <button disabled={page === 0} onClick={() => setPage(page - 1)}>
                Previous
              </button>
              <span>Page {page + 1}</span>
              <button
                disabled={(page + 1) * 48 >= result.value.TotalRecordCount}
                onClick={() => setPage(page + 1)}
              >
                Next
              </button>
            </div>
          }
        </>
      )}
    </main>
  );
}
