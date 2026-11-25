// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { Api, ApiError } from '../api/client';
import type { Item, Session } from '../api/types';
import { AppContext, type AppState } from '../state';
import Surprise from './Surprise';

const library = '11111111111111111111111111111111';
const first = '22222222222222222222222222222222';
const second = '33333333333333333333333333333333';
const item = (id = first): Item => ({
  Id: id,
  Name: id === first ? 'First pick' : 'Second pick',
  Type: 'Movie',
});
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});
function createApi(id = 'viewer') {
  return new Api('https://example.test', {
    user: { Id: id, Name: id },
    token: 'fixture-only',
  } as Session);
}
function View({ api }: { api: Api }) {
  return (
    <MemoryRouter>
      <AppContext
        value={
          {
            api,
            preferences: { watchlist: [], hiddenResume: [] },
            setPreferences: vi.fn(),
          } as unknown as AppState
        }
      >
        <Surprise />
      </AppContext>
    </MemoryRouter>
  );
}
it('uses the viewing identity and filters, excludes recent picks and resets them when filters change', async () => {
  const api = createApi();
  let chosen = first;
  const request = vi
    .spyOn(api, 'request')
    .mockImplementation(async (path) =>
      path.endsWith('/Views')
        ? { Items: [{ Id: library, Name: 'Allowed library' }] }
        : { Items: [{ ...item(chosen), Type: 'Episode' }] },
    );
  render(<View api={api} />);
  await waitFor(() =>
    expect((screen.getByLabelText('Library') as HTMLSelectElement).disabled).toBe(false),
  );
  fireEvent.change(screen.getByLabelText('Media type'), { target: { value: 'Episode' } });
  fireEvent.change(screen.getByLabelText('Library'), { target: { value: library } });
  fireEvent.change(screen.getByLabelText('Watch status'), { target: { value: 'unplayed' } });
  fireEvent.click(screen.getByRole('button', { name: 'Choose a title' }));
  await screen.findByRole('link', { name: 'Details for First pick' });
  expect(request).toHaveBeenLastCalledWith(
    'Items',
    {},
    expect.objectContaining({
      UserId: 'viewer',
      IncludeItemTypes: 'Episode',
      ParentId: library,
      IsPlayed: false,
      IsMissing: false,
      LocationTypes: 'FileSystem',
      SortBy: 'Random',
      Limit: 1,
      ExcludeItemIds: undefined,
    }),
  );
  chosen = second;
  fireEvent.click(screen.getByRole('button', { name: 'Reroll' }));
  await screen.findByRole('link', { name: 'Details for Second pick' });
  expect(request).toHaveBeenLastCalledWith(
    'Items',
    {},
    expect.objectContaining({ ExcludeItemIds: first }),
  );
  expect(screen.getByRole('link', { name: 'Play pick' }).getAttribute('href')).toBe(
    '/play/' + second,
  );
  fireEvent.change(screen.getByLabelText('Watch status'), { target: { value: 'played' } });
  fireEvent.click(screen.getByRole('button', { name: 'Choose a title' }));
  await screen.findByRole('link', { name: 'Details for Second pick' });
  expect(request).toHaveBeenLastCalledWith(
    'Items',
    {},
    expect.objectContaining({ IsPlayed: true, ExcludeItemIds: undefined }),
  );
  expect(request.mock.calls.every(([, options]) => !options?.method)).toBe(true);
});
it('does not query items when this profile has no libraries', async () => {
  const api = createApi();
  const request = vi.spyOn(api, 'request').mockResolvedValue({ Items: [] });
  render(<View api={api} />);
  await screen.findByText('No libraries are available to this profile.');
  expect(
    (screen.getByRole('button', { name: 'Choose a title' }) as HTMLButtonElement).disabled,
  ).toBe(true);
  expect(request.mock.calls.map(([path]) => path)).toEqual(['Users/viewer/Views']);
});
it('handles exhausted matches and explicitly starts over without exclusions', async () => {
  const api = createApi();
  let calls = 0;
  const request = vi
    .spyOn(api, 'request')
    .mockImplementation(async (path) =>
      path.endsWith('/Views')
        ? { Items: [{ Id: library, Name: 'Allowed' }] }
        : { Items: ++calls === 2 ? [] : [item()] },
    );
  render(<View api={api} />);
  await waitFor(() =>
    expect(
      (screen.getByRole('button', { name: 'Choose a title' }) as HTMLButtonElement).disabled,
    ).toBe(false),
  );
  fireEvent.click(screen.getByRole('button', { name: 'Choose a title' }));
  await screen.findByRole('link', { name: 'Details for First pick' });
  fireEvent.click(screen.getByRole('button', { name: 'Reroll' }));
  await screen.findByText(
    'No other matching titles are available. Change filters or start over to include previous picks.',
  );
  fireEvent.click(screen.getByRole('button', { name: 'Start over' }));
  await screen.findByRole('link', { name: 'Details for First pick' });
  expect(request).toHaveBeenLastCalledWith(
    'Items',
    {},
    expect.objectContaining({ ExcludeItemIds: undefined }),
  );
});
it.each(['denied', 'wrong-type', 'repeat'] as const)(
  'rejects %s responses instead of showing a false new selection',
  async (mode) => {
    const api = createApi();
    let calls = 0;
    vi.spyOn(api, 'request').mockImplementation(async (path) => {
      if (path.endsWith('/Views')) return { Items: [{ Id: library, Name: 'Allowed' }] };
      if (mode === 'denied') throw new ApiError(403, 'Selection denied');
      if (mode === 'wrong-type') return { Items: [{ ...item(), Type: 'Series' }] };
      calls++;
      return { Items: [item()] };
    });
    render(<View api={api} />);
    await waitFor(() =>
      expect(
        (screen.getByRole('button', { name: 'Choose a title' }) as HTMLButtonElement).disabled,
      ).toBe(false),
    );
    fireEvent.click(screen.getByRole('button', { name: 'Choose a title' }));
    if (mode === 'repeat') {
      await screen.findByRole('link', { name: 'Details for First pick' });
      fireEvent.click(screen.getByRole('button', { name: 'Reroll' }));
    }
    await screen.findByRole('alert');
    expect(screen.queryByRole('link', { name: 'Play pick' })).toBeNull();
    if (mode === 'repeat') expect(calls).toBe(2);
  },
);
it('drops a late result on identity change and keeps prior picks out of the new identity', async () => {
  const api = createApi();
  const next = createApi('child');
  let finish!: (value: unknown) => void;
  vi.spyOn(api, 'request').mockImplementation(async (path) =>
    path.endsWith('/Views')
      ? { Items: [{ Id: library, Name: 'Allowed' }] }
      : new Promise((resolve) => {
          finish = resolve;
        }),
  );
  const request = vi.spyOn(next, 'request').mockResolvedValue({ Items: [] });
  const view = render(<View api={api} />);
  await waitFor(() =>
    expect(
      (screen.getByRole('button', { name: 'Choose a title' }) as HTMLButtonElement).disabled,
    ).toBe(false),
  );
  fireEvent.click(screen.getByRole('button', { name: 'Choose a title' }));
  view.rerender(<View api={next} />);
  await screen.findByText('No libraries are available to this profile.');
  await act(async () => finish({ Items: [item()] }));
  expect(screen.queryByRole('link', { name: 'Play pick' })).toBeNull();
  expect(request.mock.calls.map(([path]) => path)).toEqual(['Users/child/Views']);
});

it('bounds reroll query history to the last 50 successful picks', async () => {
  const api = createApi();
  let count = 0;
  const request = vi.spyOn(api, 'request').mockImplementation(async (path) => {
    if (path.endsWith('/Views')) return { Items: [{ Id: library, Name: 'Allowed' }] };
    count++;
    return {
      Items: [{ Id: count.toString(16).padStart(32, '0'), Name: 'Pick ' + count, Type: 'Movie' }],
    };
  });
  render(<View api={api} />);
  await waitFor(() =>
    expect(
      (screen.getByRole('button', { name: 'Choose a title' }) as HTMLButtonElement).disabled,
    ).toBe(false),
  );
  for (let index = 1; index <= 52; index++) {
    fireEvent.click(
      screen.getByRole('button', { name: index === 1 ? 'Choose a title' : 'Reroll' }),
    );
    await screen.findByRole('link', { name: 'Details for Pick ' + index });
  }
  const excluded = request.mock.calls.at(-1)![2]!.ExcludeItemIds as string;
  expect(excluded.split(',')).toHaveLength(50);
  expect(excluded.split(',')).not.toContain('1'.padStart(32, '0'));
  expect(excluded.split(',')).toContain((51).toString(16).padStart(32, '0'));
}, 15000);
