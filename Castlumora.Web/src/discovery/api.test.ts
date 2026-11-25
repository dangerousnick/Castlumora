import { expect, it, vi } from 'vitest';
import { Api } from '../api/client';
import type { Item, Session } from '../api/types';
import { distinctMovies, genres, reason, recommendations, setGenreInterest } from './api';
const genre = '11111111111111111111111111111111';
const movie = '22222222222222222222222222222222';
const category = '33333333333333333333333333333333';
const api = () =>
  new Api('https://example.test', { user: { Id: 'viewer' }, token: 'fixture-only' } as Session);
it('uses only the signed-in viewing identity and bounded native queries for genres/recommendations', async () => {
  const client = api();
  const request = vi
    .spyOn(client, 'request')
    .mockImplementation(async (path) =>
      path === 'Genres'
        ? { Items: [{ Id: genre, Name: 'Adventure', Type: 'Genre' }], TotalRecordCount: 1 }
        : [],
    );
  await genres(client, 100);
  await genres(client, 0, true);
  await recommendations(client);
  expect(request).toHaveBeenNthCalledWith(
    1,
    'Genres',
    {},
    expect.objectContaining({
      UserId: 'viewer',
      StartIndex: 100,
      Limit: 100,
      IncludeItemTypes: 'Movie',
      IsFavorite: undefined,
    }),
  );
  expect(request).toHaveBeenNthCalledWith(
    2,
    'Genres',
    {},
    expect.objectContaining({ UserId: 'viewer', Limit: 1000, IsFavorite: true }),
  );
  expect(request).toHaveBeenNthCalledWith(
    3,
    'Movies/Recommendations',
    {},
    expect.objectContaining({ UserId: 'viewer', CategoryLimit: 4, ItemLimit: 8 }),
  );
});
it('writes native genre favorites only with valid IDs and requires explicit server confirmation', async () => {
  const client = api();
  let favorite = true;
  const request = vi
    .spyOn(client, 'request')
    .mockImplementation(async () => ({ IsFavorite: favorite }));
  await setGenreInterest(client, genre, true);
  favorite = false;
  await setGenreInterest(client, genre, false);
  expect(request).toHaveBeenNthCalledWith(
    1,
    'UserFavoriteItems/' + genre,
    { method: 'POST' },
    { UserId: 'viewer' },
  );
  expect(request).toHaveBeenNthCalledWith(
    2,
    'UserFavoriteItems/' + genre,
    { method: 'DELETE' },
    { UserId: 'viewer' },
  );
  await expect(setGenreInterest(client, genre, true)).rejects.toThrow('did not confirm');
  await expect(setGenreInterest(client, '../other', true)).rejects.toThrow('Invalid genre');
  await expect(setGenreInterest(client, '0'.repeat(32), true)).rejects.toThrow('Invalid genre');
  expect(request).toHaveBeenCalledTimes(3);
});
it('rejects malformed/oversized state without reset or unscoped fallback calls', async () => {
  const client = api();
  const request = vi
    .spyOn(client, 'request')
    .mockResolvedValue({ Items: [], TotalRecordCount: 1001 });
  await expect(genres(client, 0, true)).rejects.toThrow('no interests were reset');
  request.mockResolvedValue({
    Items: [{ Id: movie, Type: 'Movie', Name: 'Wrong type' }],
    TotalRecordCount: 1,
  });
  await expect(genres(client)).rejects.toThrow('Invalid genre response');
  request.mockResolvedValue(Array.from({ length: 5 }, () => ({ CategoryId: category, Items: [] })));
  await expect(recommendations(client)).rejects.toThrow('Invalid recommendation response');
  const anonymous = new Api('https://example.test');
  const none = vi.spyOn(anonymous, 'request');
  await expect(genres(anonymous)).rejects.toThrow('Sign in');
  expect(none).not.toHaveBeenCalled();
});
it('keeps native explanations, gracefully labels unknown reasons and removes duplicate/non-movie cards', () => {
  expect(
    reason({
      CategoryId: category,
      RecommendationType: 0,
      BaselineItemName: 'My movie',
      Items: [],
    }),
  ).toBe('Because you watched — My movie');
  expect(reason({ CategoryId: category, RecommendationType: 'FutureKind', Items: [] })).toBe(
    'Suggested by your server',
  );
  const seen: string[] = [];
  const item: Item = { Id: movie, Name: 'One', Type: 'Movie' };
  expect(
    distinctMovies(
      [item, { ...item, Id: movie.toUpperCase() }, { ...item, Id: genre, Type: 'Trailer' }],
      seen,
    ),
  ).toEqual([item]);
  expect(distinctMovies([item], seen)).toEqual([]);
});
