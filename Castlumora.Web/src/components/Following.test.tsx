// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { Api, ApiError } from '../api/client';
import { AppContext, type AppState } from '../state';
import { FollowSeriesButton, FollowedSeriesPanel } from './Following';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});
function mount(api: Api, panel = false) {
  render(
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
        {panel ? <FollowedSeriesPanel /> : <FollowSeriesButton id="show" />}
      </AppContext>
    </MemoryRouter>,
  );
}

it.each([false, true])(
  'changes follow state only after server success (denied=%s)',
  async (denied) => {
    const api = new Api('https://example.test');
    let followed = false;
    const request = vi.spyOn(api, 'request').mockImplementation(async (_path, options) => {
      if (options?.method === 'PUT') {
        if (denied) throw new ApiError(403, 'Following denied');
        followed = JSON.parse(options.body as string).enabled;
      }
      return { followed };
    });
    mount(api);
    const button = await screen.findByRole('button', { name: 'Follow series' });
    await waitFor(() => expect((button as HTMLButtonElement).disabled).toBe(false));
    fireEvent.click(button);
    if (denied) {
      await screen.findByText('Following denied');
      expect(screen.queryByText('Series followed.')).toBeNull();
      expect(screen.queryByRole('button', { name: 'Unfollow series' })).toBeNull();
    } else {
      await screen.findByRole('button', { name: 'Unfollow series' });
      await waitFor(() =>
        expect(request).toHaveBeenCalledWith('Castlumora/Following/show', {
          method: 'PUT',
          body: '{"enabled":true}',
        }),
      );
      await waitFor(() =>
        expect(
          (screen.getByRole('button', { name: 'Unfollow series' }) as HTMLButtonElement).disabled,
        ).toBe(false),
      );
      fireEvent.click(screen.getByRole('button', { name: 'Unfollow series' }));
      await screen.findByText('Series unfollowed.');
    }
    expect(request.mock.calls.every(([path]) => path.startsWith('Castlumora/Following/'))).toBe(
      true,
    );
  },
);

it('does not query all media for an empty visible list and confirms clearing unavailable follows', async () => {
  const api = new Api('https://example.test');
  const request = vi.spyOn(api, 'request').mockResolvedValue({ seriesIds: [], total: 0 });
  const items = vi.spyOn(api, 'items');
  mount(api, true);
  await screen.findByText('No followed series are currently available to this profile.');
  expect(items).not.toHaveBeenCalled();
  const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
  fireEvent.click(screen.getByRole('button', { name: 'Stop following all series' }));
  expect(request.mock.calls.some(([path]) => path.endsWith('/Clear'))).toBe(false);
  confirm.mockReturnValue(true);
  fireEvent.click(screen.getByRole('button', { name: 'Stop following all series' }));
  await screen.findByText('All series unfollowed.');
  expect(request).toHaveBeenCalledWith('Castlumora/Following/Clear', {
    method: 'POST',
    body: '{}',
  });
  expect(items).not.toHaveBeenCalled();
});

it('paginates followed IDs through user-scoped item queries and safely unfollows a visible series', async () => {
  const api = new Api('https://example.test');
  let removed = false;
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options) => {
    if (options?.method === 'PUT') {
      removed = true;
      return { followed: false };
    }
    if (path === 'Items')
      return { Items: [{ Id: 'show', Name: 'Owned show', Type: 'Series' }], TotalRecordCount: 1 };
    return { seriesIds: removed ? [] : ['show'], total: removed ? 0 : 49 };
  });
  mount(api, true);
  await screen.findByRole('button', { name: 'Unfollow Owned show' });
  fireEvent.click(screen.getByRole('button', { name: 'Next followed series' }));
  await waitFor(() =>
    expect(request).toHaveBeenCalledWith('Castlumora/Following', {}, { startIndex: 48, limit: 48 }),
  );
  fireEvent.click(await screen.findByRole('button', { name: 'Unfollow Owned show' }));
  await screen.findByText('Series unfollowed.');
  await screen.findByText('No followed series are currently available to this profile.');
  expect(request).toHaveBeenCalledWith('Castlumora/Following/show', {
    method: 'PUT',
    body: '{"enabled":false}',
  });
});
