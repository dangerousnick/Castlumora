// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { Api, ApiError } from '../api/client';
import type { Item, Session } from '../api/types';
import { AppContext, type AppState } from '../state';
import Discover from './Discover';
const genre: Item = { Id: '11111111111111111111111111111111', Type: 'Genre', Name: 'Adventure' };
const movie: Item = { Id: '22222222222222222222222222222222', Type: 'Movie', Name: 'Native pick' };
const category = '33333333333333333333333333333333';
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});
function api(id = 'viewer') {
  return new Api('https://example.test', { user: { Id: id }, token: 'fixture-only' } as Session);
}
function View({ client }: { client: Api }) {
  return (
    <MemoryRouter>
      <AppContext
        value={
          {
            api: client,
            preferences: { watchlist: [], hiddenResume: [] },
            setPreferences: vi.fn(),
          } as unknown as AppState
        }
      >
        <Discover />
      </AppContext>
    </MemoryRouter>
  );
}
it('persists explicit genre interests only after server success and diversifies native movie rows', async () => {
  const client = api();
  let favorite = false;
  const request = vi.spyOn(client, 'request').mockImplementation(async (path, options, params) => {
    if (path.startsWith('UserFavoriteItems/')) {
      favorite = options?.method === 'POST';
      return { IsFavorite: favorite };
    }
    if (path === 'Genres')
      return {
        Items: params?.IsFavorite ? (favorite ? [genre] : []) : [genre],
        TotalRecordCount: params?.IsFavorite ? Number(favorite) : 1,
      };
    if (path === 'Movies/Recommendations')
      return [
        {
          CategoryId: category,
          RecommendationType: 'SimilarToRecentlyPlayed',
          BaselineItemName: 'Watched fixture',
          Items: [movie],
        },
      ];
    return { Items: [movie], TotalRecordCount: 1 };
  });
  render(<View client={client} />);
  await screen.findByRole('heading', { name: 'Because you watched — Watched fixture' });
  fireEvent.click(screen.getByRole('checkbox', { name: 'Adventure' }));
  await screen.findByRole('heading', { name: 'Explore Adventure' });
  expect((screen.getByRole('checkbox', { name: 'Adventure' }) as HTMLInputElement).checked).toBe(
    true,
  );
  expect(screen.getAllByRole('link', { name: 'Details for Native pick' })).toHaveLength(1);
  expect(request).toHaveBeenCalledWith(
    'Items',
    {},
    expect.objectContaining({
      UserId: 'viewer',
      GenreIds: genre.Id,
      IncludeItemTypes: 'Movie',
      IsPlayed: false,
      Limit: 8,
    }),
  );
  fireEvent.click(screen.getByRole('checkbox', { name: 'Adventure' }));
  await screen.findByRole('heading', { name: 'Because you watched — Watched fixture' });
  expect((screen.getByRole('checkbox', { name: 'Adventure' }) as HTMLInputElement).checked).toBe(
    false,
  );
  expect(request).toHaveBeenCalledWith(
    'UserFavoriteItems/' + genre.Id,
    { method: 'DELETE' },
    { UserId: 'viewer' },
  );
});
it('does not pretend a denied interest was saved', async () => {
  const client = api();
  vi.spyOn(client, 'request').mockImplementation(async (path, _options, params) => {
    if (path.startsWith('UserFavoriteItems/')) throw new ApiError(403, 'Interest denied');
    if (path === 'Genres')
      return {
        Items: params?.IsFavorite ? [] : [genre],
        TotalRecordCount: params?.IsFavorite ? 0 : 1,
      };
    return [];
  });
  render(<View client={client} />);
  fireEvent.click(await screen.findByRole('checkbox', { name: 'Adventure' }));
  await screen.findByText('Interest denied');
  expect((screen.getByRole('checkbox', { name: 'Adventure' }) as HTMLInputElement).checked).toBe(
    false,
  );
  expect(screen.queryByText('Interests updated.')).toBeNull();
});
it('offers honest cold-start states without requesting unfiltered media', async () => {
  const client = api();
  const request = vi
    .spyOn(client, 'request')
    .mockImplementation(async (path) =>
      path === 'Genres' ? { Items: [], TotalRecordCount: 0 } : [],
    );
  render(<View client={client} />);
  await screen.findByText('No movie genres are currently available to this profile.');
  expect(screen.getByText(/No additional history-based/)).toBeTruthy();
  expect(request.mock.calls.some(([path]) => path === 'Items')).toBe(false);
});
it('paginates genre options without replacing preferences or overriding identity', async () => {
  const client = api();
  const request = vi.spyOn(client, 'request').mockImplementation(async (path, _options, params) => {
    if (path !== 'Genres') return [];
    if (params?.IsFavorite) return { Items: [], TotalRecordCount: 0 };
    return {
      Items: [{ ...genre, Name: params?.StartIndex ? 'Drama' : 'Adventure' }],
      TotalRecordCount: 101,
    };
  });
  render(<View client={client} />);
  await screen.findByRole('checkbox', { name: 'Adventure' });
  fireEvent.click(screen.getByRole('button', { name: 'Next genres' }));
  await screen.findByRole('checkbox', { name: 'Drama' });
  expect(request).toHaveBeenCalledWith(
    'Genres',
    {},
    expect.objectContaining({ UserId: 'viewer', StartIndex: 100, Limit: 100 }),
  );
  expect(request.mock.calls.every(([, options]) => !options?.method)).toBe(true);
});
it('never renders an old identity’s late recommendation response', async () => {
  const owner = api();
  const child = api('child');
  let finish!: (value: unknown) => void;
  vi.spyOn(owner, 'request').mockImplementation(async (path) =>
    path === 'Genres'
      ? { Items: [], TotalRecordCount: 0 }
      : new Promise((resolve) => {
          finish = resolve;
        }),
  );
  const request = vi
    .spyOn(child, 'request')
    .mockImplementation(async (path) =>
      path === 'Genres' ? { Items: [], TotalRecordCount: 0 } : [],
    );
  const view = render(<View client={owner} />);
  await waitFor(() => expect(finish).toBeTypeOf('function'));
  view.rerender(<View client={child} />);
  await screen.findByText('No movie genres are currently available to this profile.');
  await act(async () => finish([{ CategoryId: category, RecommendationType: 0, Items: [movie] }]));
  expect(screen.queryByRole('link', { name: 'Details for Native pick' })).toBeNull();
  expect(request.mock.calls.every(([, , params]) => params?.UserId === 'child')).toBe(true);
});
