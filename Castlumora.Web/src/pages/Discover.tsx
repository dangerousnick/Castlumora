import { useState } from 'react';
import { Link } from 'react-router-dom';
import { sameId } from '../api/client';
import { useApp, useLoad } from '../state';
import { Feedback, MediaRow } from '../components/Media';
import { ActionStatus, useAction } from '../admin/Controls';
import {
  distinctMovies,
  genres,
  recommendations,
  reason,
  setGenreInterest,
} from '../discovery/api';

export default function Discover() {
  const { api } = useApp();
  const [page, setPage] = useState(0);
  const [revision, setRevision] = useState(0);
  const action = useAction();
  const state = useLoad(async () => {
    const [options, favorites, native] = await Promise.all([
      genres(api, page * 100),
      genres(api, 0, true),
      recommendations(api),
    ]);
    const interests = await Promise.all(
      favorites.Items.slice(0, 4).map(async (genre) => ({
        id: genre.Id,
        title: 'Explore ' + genre.Name,
        items: await (async () => {
          const result = await api.items({
            IncludeItemTypes: 'Movie',
            GenreIds: genre.Id,
            IsPlayed: false,
            LocationTypes: 'FileSystem',
            IsMissing: false,
            Limit: 8,
            SortBy: 'Random',
            EnableTotalRecordCount: false,
          });
          if (!Array.isArray(result.Items) || result.Items.length > 8)
            throw new Error('Invalid interest recommendation response.');
          return result.Items;
        })(),
      })),
    );
    const seen: string[] = [];
    const interestRows = interests
      .map((row) => ({ ...row, items: distinctMovies(row.items, seen) }))
      .filter((row) => row.items.length);
    const historyRows = native
      .map((group) => ({
        id: group.CategoryId + ':' + group.RecommendationType,
        title: reason(group),
        items: distinctMovies(group.Items, seen),
      }))
      .filter((row) => row.items.length);
    return { options, favorites, interestRows, historyRows };
  }, [api, page, revision]);
  const refresh = () => setRevision((value) => value + 1);
  return (
    <main className="page">
      <div className="page-heading">
        <div>
          <h1>Discover your next movie</h1>
          <p>
            Local recommendations from your interests, favorites and watched history. No external AI
            service is used.
          </p>
        </div>
        <Link to="/surprise" className="button secondary">
          Surprise Me filters
        </Link>
      </div>
      <Feedback busy={state.busy} error={state.error} />
      {state.value && (
        <>
          <section className="panel" aria-label="Movie genre interests">
            <h2>Your interests</h2>
            <p>
              Choose favorite genres for this viewing profile. Only genres attached to movies you
              can currently access are listed. These interests also appear as genre favorites in
              compatible clients.
            </p>
            {!state.value.options.Items.length && (
              <p>No movie genres are currently available to this profile.</p>
            )}
            <div className="actions">
              {state.value.options.Items.map((genre) => {
                const selected = state.value!.favorites.Items.some((favorite) =>
                  sameId(favorite.Id, genre.Id),
                );
                return (
                  <label key={genre.Id}>
                    <input
                      type="checkbox"
                      checked={selected}
                      disabled={action.busy || state.busy}
                      onChange={(event) => {
                        const enabled = event.target.checked;
                        void action.run(async () => {
                          await setGenreInterest(api, genre.Id, enabled);
                          refresh();
                        }, 'Interests updated.');
                      }}
                    />
                    {genre.Name}
                  </label>
                );
              })}
            </div>
            <div className="actions">
              <button
                disabled={page === 0 || action.busy}
                onClick={() => setPage((value) => value - 1)}
              >
                Previous genres
              </button>
              <button
                disabled={(page + 1) * 100 >= state.value.options.TotalRecordCount || action.busy}
                onClick={() => setPage((value) => value + 1)}
              >
                Next genres
              </button>
              <button disabled={action.busy || state.busy} onClick={refresh}>
                Refresh discovery
              </button>
            </div>
            <p>
              Up to four visible favorite genres guide these rows. Unwatched interest picks and
              history-based suggestions avoid repeating the same media item on this page.
            </p>
          </section>
          <ActionStatus message={action.message} />
          <section aria-label="Interest recommendations">
            <h2>From your interests</h2>
            {!state.value.interestRows.length && (
              <p>
                No unplayed interest picks are available yet. Choose a genre or add matching movies.
              </p>
            )}
            {state.value.interestRows.map((row) => (
              <MediaRow key={row.id} title={row.title} items={row.items} />
            ))}
          </section>
          <section aria-label="History recommendations">
            <h2>From your viewing</h2>
            {!state.value.historyRows.length && (
              <p>
                No additional history-based recommendations are available yet. Watch or favorite a
                movie, or explore genres above.
              </p>
            )}
            {state.value.historyRows.map((row) => (
              <MediaRow key={row.id} title={row.title} items={row.items} />
            ))}
          </section>
        </>
      )}
    </main>
  );
}
