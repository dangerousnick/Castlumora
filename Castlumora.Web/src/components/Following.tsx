import { useState } from 'react';
import { ApiError } from '../api/client';
import { useApp, useLoad } from '../state';
import { ActionStatus, useAction } from '../admin/Controls';
import { Feedback, Poster } from './Media';

export function FollowSeriesButton({ id }: { id: string }) {
  const { api } = useApp();
  const [revision, setRevision] = useState(0);
  const action = useAction();
  const state = useLoad(async () => {
    try {
      const value = await api.seriesFollowState(id);
      if (typeof value.followed !== 'boolean') throw new Error('Invalid following response.');
      return value;
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null;
      throw error;
    }
  }, [api, id, revision]);
  if (state.value === null) return null;
  return (
    <div>
      <button
        disabled={state.busy || action.busy || !state.value}
        aria-pressed={state.value?.followed ?? false}
        onClick={() => {
          const enabled = !state.value?.followed;
          void action.run(
            async () => {
              await api.followSeries(id, enabled);
              setRevision((value) => value + 1);
            },
            enabled ? 'Series followed.' : 'Series unfollowed.',
          );
        }}
      >
        {state.value?.followed ? 'Unfollow series' : 'Follow series'}
      </button>
      <Feedback busy={false} error={state.error} />
      <ActionStatus message={action.message} />
    </div>
  );
}

export function FollowedSeriesPanel() {
  const { api } = useApp();
  const [page, setPage] = useState(0);
  const [revision, setRevision] = useState(0);
  const action = useAction();
  const state = useLoad(async () => {
    try {
      const result = await api.followedSeries(page * 48, 48);
      if (
        !Array.isArray(result.seriesIds) ||
        result.seriesIds.length > 48 ||
        !Number.isSafeInteger(result.total) ||
        result.total < 0
      )
        throw new Error('Invalid following response.');
      const items = result.seriesIds.length
        ? (
            await api.items({
              Ids: result.seriesIds.join(','),
              IncludeItemTypes: 'Series',
              Limit: 48,
              SortBy: 'SortName',
            })
          ).Items
        : [];
      return { items, total: result.total };
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null;
      throw error;
    }
  }, [api, page, revision]);
  if (state.value === null) return null;
  const refresh = () => {
    setPage(0);
    setRevision((value) => value + 1);
  };
  return (
    <section className="panel" aria-label="Followed series">
      <h2>Followed series</h2>
      <p>Keep your favorite shows together.</p>
      <Feedback busy={state.busy} error={state.error} />
      {state.value && (
        <>
          {!state.value.items.length && (
            <p>No followed series are currently available to this profile.</p>
          )}
          <div className="poster-grid">
            {state.value.items.map((item) => (
              <article key={item.Id}>
                <Poster item={item} />
                <button
                  disabled={action.busy}
                  onClick={() =>
                    void action.run(async () => {
                      await api.followSeries(item.Id, false);
                      refresh();
                    }, 'Series unfollowed.')
                  }
                >
                  Unfollow {item.Name}
                </button>
              </article>
            ))}
          </div>
          <div className="actions">
            <button
              disabled={page === 0 || action.busy}
              onClick={() => setPage((value) => value - 1)}
            >
              Previous followed series
            </button>
            <button
              disabled={(page + 1) * 48 >= state.value.total || action.busy}
              onClick={() => setPage((value) => value + 1)}
            >
              Next followed series
            </button>
            <button
              disabled={action.busy}
              onClick={() => {
                if (
                  !window.confirm(
                    'Stop following all series, including unavailable titles? Your watchlist and viewing history stay unchanged.',
                  )
                )
                  return;
                void action.run(async () => {
                  await api.clearFollowing();
                  refresh();
                }, 'All series unfollowed.');
              }}
            >
              Stop following all series
            </button>
          </div>
        </>
      )}
      <ActionStatus message={action.message} />
    </section>
  );
}
