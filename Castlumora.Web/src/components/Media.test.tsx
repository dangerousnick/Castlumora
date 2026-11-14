// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { MediaRow, Poster } from './Media';
import { AppContext } from '../state';
import type { AppState } from '../state';
import { Api } from '../api/client';
import { defaultLayout } from '../config/schema';
afterEach(cleanup);
const session = {
  server: 'https://example.test',
  token: 'fixture-token',
  user: { Id: 'u', Name: 'Viewer' },
  deviceId: 'd',
};
const item = { Id: '11111111111111111111111111111111', Name: 'A real API title', Type: 'Movie' };
it('does not render an empty row', () => {
  const { container } = render(<MediaRow title="Empty" items={[]} />);
  expect(container.innerHTML).toBe('');
});
it('uses the dedicated resume preference rather than marking played', async () => {
  const api = new Api(session.server, session);
  const preference = vi
    .spyOn(api, 'preference')
    .mockResolvedValue({ watchlist: [], hiddenResume: [item.Id] });
  const setPreferences = vi.fn();
  const state: AppState = {
    api,
    session,
    layout: defaultLayout,
    preferences: { watchlist: [], hiddenResume: [] },
    setPreferences,
    refreshLayout: vi.fn(),
    logout: vi.fn(),
    notice: '',
  };
  render(
    <MemoryRouter>
      <AppContext.Provider value={state}>
        <Poster item={item} resume />
      </AppContext.Provider>
    </MemoryRouter>,
  );
  fireEvent.click(screen.getByText('Remove from Continue Watching'));
  await waitFor(() => expect(preference).toHaveBeenCalledWith('hiddenResume', item.Id, true));
  expect(setPreferences).toHaveBeenCalledWith({ watchlist: [], hiddenResume: [item.Id] });
});
