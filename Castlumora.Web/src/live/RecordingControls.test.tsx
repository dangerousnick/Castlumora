// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AppContext } from '../state';
import type { AppState } from '../state';
import { Api } from '../api/client';
import { defaultLayout } from '../config/schema';
import { RecordingControls } from './RecordingControls';
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});
function setup(manage: boolean, canDelete: boolean) {
  const api = new Api('https://example.test/base');
  const request = vi.spyOn(api, 'request').mockResolvedValue(undefined);
  const changed = vi.fn();
  const state = {
    api,
    session: {
      server: api.server,
      token: 'test',
      deviceId: 'd',
      user: {
        Id: 'viewer',
        Name: 'Viewer',
        Policy: { IsAdministrator: false, EnableLiveTvManagement: manage },
      },
    },
    layout: defaultLayout,
    preferences: { watchlist: [], hiddenResume: [] },
    setPreferences: vi.fn(),
    refreshLayout: vi.fn(),
    logout: vi.fn(),
    notice: '',
  } as AppState;
  render(
    <AppContext value={state}>
      <RecordingControls
        item={{ Id: 'owned-recording', Name: 'Castle', Type: 'Video', CanDelete: canDelete }}
        changed={changed}
      />
    </AppContext>,
  );
  return { request, changed };
}
it('hides recording mutations from viewers and gates file deletion on the server item permission', () => {
  setup(false, true);
  expect(screen.queryByRole('button')).toBeNull();
  cleanup();
  setup(true, false);
  expect(screen.getByRole('button', { name: 'Remove from library' })).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Delete recording file' })).toBeNull();
});
it('keeps library removal and permission-checked physical deletion on separate inherited endpoints', async () => {
  const { request, changed } = setup(true, true);
  vi.spyOn(window, 'confirm').mockReturnValue(true);
  fireEvent.click(screen.getByRole('button', { name: 'Remove from library' }));
  await waitFor(() => expect(changed).toHaveBeenCalledTimes(1));
  expect(request).toHaveBeenCalledWith('LiveTv/Recordings/owned-recording', { method: 'DELETE' });
  fireEvent.click(screen.getByRole('button', { name: 'Delete recording file' }));
  await waitFor(() => expect(changed).toHaveBeenCalledTimes(2));
  expect(request).toHaveBeenCalledWith('Items/owned-recording', { method: 'DELETE' });
});
it('does not delete when the user declines confirmation', () => {
  const { request } = setup(true, true);
  vi.spyOn(window, 'confirm').mockReturnValue(false);
  fireEvent.click(screen.getByRole('button', { name: 'Delete recording file' }));
  expect(request).not.toHaveBeenCalled();
});
