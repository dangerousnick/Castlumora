import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import type { Item } from '../api/types';
import { sameId } from '../api/client';
import { errorText, useApp, useLoad } from '../state';
import { Feedback, Poster } from '../components/Media';

export default function Surprise() {
  const { api } = useApp();
  const libraries = useLoad(async () => {
    const result = await api.views();
    if (!Array.isArray(result.Items)) throw new Error('Invalid library response.');
    return result;
  }, [api]);
  const [kind, setKind] = useState('Movie');
  const [folder, setFolder] = useState('');
  const [watched, setWatched] = useState('all');
  const [recent, setRecent] = useState<string[]>([]);
  const [pick, setPick] = useState<Item | null>(null);
  const [searched, setSearched] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const requests = useRef({ version: 0, pending: false }).current;
  useEffect(() => {
    requests.version++;
    requests.pending = false;
    setBusy(false);
    setPick(null);
    setSearched(false);
    setRecent([]);
    setError('');
    return () => {
      requests.version++;
    };
  }, [api, kind, folder, watched, requests]);

  async function choose(startOver = false) {
    if (requests.pending || !libraries.value?.Items.length) return;
    if (folder && !libraries.value.Items.some((item) => sameId(item.Id, folder))) {
      setError('Choose a library available to this profile.');
      return;
    }
    const version = ++requests.version;
    const exclusions = startOver ? [] : recent;
    requests.pending = true;
    setBusy(true);
    setError('');
    setPick(null);
    setSearched(false);
    try {
      const result = await api.items({
        IncludeItemTypes: kind,
        ParentId: folder || undefined,
        IsPlayed: watched === 'all' ? undefined : watched === 'played',
        LocationTypes: 'FileSystem',
        IsMissing: false,
        SortBy: 'Random',
        Limit: 1,
        EnableTotalRecordCount: false,
        ExcludeItemIds: exclusions.length ? exclusions.join(',') : undefined,
      });
      if (version !== requests.version) return;
      if (!Array.isArray(result.Items) || result.Items.length > 1)
        throw new Error('Invalid selection response.');
      const item = result.Items[0];
      if (
        item &&
        (item.Type !== kind ||
          typeof item.Name !== 'string' ||
          typeof item.Id !== 'string' ||
          !/^[a-f0-9]{32}$/i.test(item.Id.replaceAll('-', '')))
      )
        throw new Error('The server returned an unsupported pick.');
      if (item && exclusions.some((id) => sameId(id, item.Id)))
        throw new Error('No different title was returned. Start over or change your filters.');
      setPick(item ?? null);
      setSearched(true);
      if (item) setRecent([...exclusions, item.Id].slice(-50));
      else if (startOver) setRecent([]);
    } catch (e) {
      if (version === requests.version) setError(errorText(e));
    } finally {
      if (version === requests.version) {
        requests.pending = false;
        setBusy(false);
      }
    }
  }

  const available = !!libraries.value?.Items.length && !libraries.error && !libraries.busy;
  return (
    <main className="page">
      <h1>Surprise Me</h1>
      <p>
        Find a local movie or episode available to this viewing profile. Choosing and rerolling do
        not change watch history.
      </p>
      <div className="panel">
        <label>
          Media type
          <select value={kind} disabled={busy} onChange={(event) => setKind(event.target.value)}>
            <option value="Movie">Movies</option>
            <option value="Episode">Episodes</option>
          </select>
        </label>
        <label>
          Library
          <select
            value={folder}
            disabled={busy || !available}
            onChange={(event) => setFolder(event.target.value)}
          >
            <option value="">All allowed libraries</option>
            {libraries.value?.Items.map((library) => (
              <option key={library.Id} value={library.Id}>
                {library.Name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Watch status
          <select
            value={watched}
            disabled={busy}
            onChange={(event) => setWatched(event.target.value)}
          >
            <option value="all">Any watch status</option>
            <option value="unplayed">Unwatched</option>
            <option value="played">Watched</option>
          </select>
        </label>
        <div className="actions">
          <button disabled={busy || !available} onClick={() => void choose()}>
            {recent.length ? 'Reroll' : 'Choose a title'}
          </button>
          {!!recent.length && (
            <button disabled={busy || !available} onClick={() => void choose(true)}>
              Start over
            </button>
          )}
        </div>
        <p>
          Rerolls exclude your last 50 successful picks for these filters. Filters and picks reset
          when you leave or switch profile.
        </p>
      </div>
      <Feedback busy={busy || libraries.busy} error={error || libraries.error} />
      {libraries.value && !libraries.value.Items.length && (
        <p>No libraries are available to this profile.</p>
      )}
      {searched && !pick && !busy && !error && (
        <p>
          No other matching titles are available. Change filters or start over to include previous
          picks.
        </p>
      )}
      {pick && (
        <section aria-label="Surprise pick">
          <h2>Your pick</h2>
          <div className="poster-grid">
            <Poster item={pick} />
          </div>
          <div className="actions">
            <Link className="button" to={'/play/' + pick.Id}>
              Play pick
            </Link>
            <Link to={'/item/' + pick.Id}>Open picked details</Link>
          </div>
        </section>
      )}
    </main>
  );
}
