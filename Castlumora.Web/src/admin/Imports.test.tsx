// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AppContext } from '../state';
import type { AppState } from '../state';
import { Api } from '../api/client';
import { defaultLayout } from '../config/schema';
import Imports from './Imports';
const state = {
  state: 'notInstalled',
  message: 'Install bundled asset',
  ready: false,
  supported: true,
  bundleAvailable: true,
  expectedVersion: '2026.10.26',
  platform: 'linux-x64',
  releaseUrl: 'https://github.com/yt-dlp/yt-dlp/releases/tag/2026.10.26',
  downloadJobsAvailable: false,
};
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});
function setup(value = state) {
  const api = new Api('https://example.test/base');
  const request = vi.spyOn(api, 'request').mockResolvedValue(value);
  const app = {
    api,
    session: {
      server: api.server,
      token: 'test',
      deviceId: 'd',
      user: { Id: 'admin', Name: 'Admin', Policy: { IsAdministrator: true } },
    },
    layout: defaultLayout,
    preferences: { watchlist: [], hiddenResume: [] },
    setPreferences: vi.fn(),
    refreshLayout: vi.fn(),
    logout: vi.fn(),
    notice: '',
  } as AppState;
  render(
    <AppContext value={app}>
      <Imports />
    </AppContext>,
  );
  return request;
}
it('installs only after confirmation and displays verified readiness without offering download jobs', async () => {
  const request = setup();
  await screen.findByRole('button', { name: 'Install bundled runtime' });
  request.mockResolvedValue({ ...state, state: 'ready', ready: true, message: 'Verified' });
  vi.spyOn(window, 'confirm').mockReturnValue(true);
  fireEvent.click(screen.getByRole('button', { name: 'Install bundled runtime' }));
  await screen.findByText('Ready');
  expect(request).toHaveBeenCalledWith('Castlumora/Imports/Runtime/Install', { method: 'POST' });
  expect(screen.queryByRole('textbox')).toBeNull();
});
it('does not install without confirmation or when the shipped asset is missing', async () => {
  const request = setup();
  await screen.findByRole('button', { name: 'Install bundled runtime' });
  vi.spyOn(window, 'confirm').mockReturnValue(false);
  fireEvent.click(screen.getByRole('button', { name: 'Install bundled runtime' }));
  await waitFor(() => expect(request).toHaveBeenCalledTimes(1));
  cleanup();
  setup({ ...state, bundleAvailable: false });
  await screen.findByText(/bundled asset is missing/);
  expect(
    (screen.getByRole('button', { name: 'Install bundled runtime' }) as HTMLButtonElement).disabled,
  ).toBe(true);
});
