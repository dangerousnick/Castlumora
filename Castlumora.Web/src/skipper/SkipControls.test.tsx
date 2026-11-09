// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { Api } from '../api/client';
import { AppContext } from '../state';
import type { AppState } from '../state';
import { defaultLayout } from '../config/schema';
import { SkipControls } from './SkipControls';
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});
function setup(mode: 'manual' | 'automatic' | 'disabled', position = 150000000) {
  const session = {
    server: 'https://example.test',
    token: 'fixture',
    user: { Id: 'u', Name: 'Viewer' },
    deviceId: 'd',
  };
  const api = new Api(session.server, session);
  const request = vi.spyOn(api, 'request').mockImplementation(async (path) => {
    if (path.endsWith('Settings')) return { analysisEnabled: false, defaultMode: 'manual' };
    if (path.endsWith('Profile')) return { mode };
    return { Items: [{ Type: 'Intro', StartTicks: 100000000, EndTicks: 300000000 }] };
  });
  const state: AppState = {
    api,
    session,
    layout: defaultLayout,
    preferences: { watchlist: [], hiddenResume: [] },
    setPreferences: vi.fn(),
    refreshLayout: vi.fn(),
    logout: vi.fn(),
    notice: '',
  };
  const seek = vi.fn();
  render(
    <AppContext.Provider value={state}>
      <SkipControls id="episode" positionTicks={position} onSeek={seek} />
    </AppContext.Provider>,
  );
  return { seek, request };
}
it('shows a manual skip only inside the approved interval and seeks to its exact end', async () => {
  const { seek } = setup('manual');
  fireEvent.click(await screen.findByRole('button', { name: 'Skip Intro' }));
  expect(seek).toHaveBeenCalledExactlyOnceWith(30);
});
it('automatic mode skips once while disabled mode never skips', async () => {
  const { seek } = setup('automatic');
  await waitFor(() => expect(seek).toHaveBeenCalledExactlyOnceWith(30));
  cleanup();
  const disabled = setup('disabled');
  await screen.findByRole('combobox', { name: 'Episode skipping' });
  expect(screen.queryByRole('button', { name: 'Skip Intro' })).toBeNull();
  expect(disabled.seek).not.toHaveBeenCalled();
});
it('does not skip unique content outside the interval and stores profile choice on the server', async () => {
  const { seek, request } = setup('manual', 350000000);
  const choice = await screen.findByRole('combobox', { name: 'Episode skipping' });
  expect(screen.queryByRole('button', { name: 'Skip Intro' })).toBeNull();
  expect(seek).not.toHaveBeenCalled();
  fireEvent.change(choice, { target: { value: 'disabled' } });
  await waitFor(() =>
    expect(request).toHaveBeenCalledWith(
      'Castlumora/Skipper/Profile',
      expect.objectContaining({ method: 'PUT', body: '{"mode":"disabled"}' }),
    ),
  );
});
