// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AppContext } from '../state';
import type { AppState } from '../state';
import { Api } from '../api/client';
import { defaultLayout } from '../config/schema';
import Administration from './Administration';
import { MetadataEditor } from './Metadata';
import type { MetadataItem } from './metadataApi';
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});
function setup(item: MetadataItem, admin = true, routeGuard = false) {
  const api = new Api('https://example.test/base');
  const request = vi.spyOn(api, 'request').mockResolvedValue(item);
  const changed = vi.fn();
  const state = {
    api,
    session: {
      server: api.server,
      token: 'test',
      deviceId: 'd',
      user: { Id: 'u', Name: 'User', Policy: { IsAdministrator: admin } },
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
      {routeGuard ? <Administration /> : <MetadataEditor item={item} changed={changed} />}
    </AppContext>,
  );
  return { request, changed };
}
it('saves corrections with a fresh server snapshot and reloads the selected item', async () => {
  const item = {
    Id: 'owned',
    Name: 'Original',
    Type: 'Video',
    LockData: false,
    FutureField: 'preserve',
  };
  const { request, changed } = setup(item);
  fireEvent.change(screen.getByLabelText('Metadata title'), {
    target: { value: 'Corrected creator' },
  });
  fireEvent.change(screen.getByLabelText('Description', { exact: true }), {
    target: { value: 'Corrected description' },
  });
  fireEvent.click(screen.getByLabelText('Lock manual metadata corrections'));
  expect(
    (screen.getByRole('button', { name: 'Repair from source' }) as HTMLButtonElement).disabled,
  ).toBe(true);
  fireEvent.click(screen.getByRole('button', { name: 'Save metadata corrections' }));
  await waitFor(() => expect(changed).toHaveBeenCalledOnce());
  expect(JSON.parse(request.mock.calls[1][1]!.body as string)).toMatchObject({
    ...item,
    Name: 'Corrected creator',
    Overview: 'Corrected description',
    LockData: true,
  });
});
it('requires explicit confirmation for replacement and does not offer replacement while locked', () => {
  const { request } = setup({ Id: 'owned', Name: 'Original', Type: 'Video', LockData: false });
  vi.spyOn(window, 'confirm').mockReturnValue(false);
  fireEvent.click(screen.getByRole('button', { name: 'Repair from source' }));
  expect(request).not.toHaveBeenCalled();
  cleanup();
  setup({ Id: 'owned', Name: 'Corrected', Type: 'Video', LockData: true });
  expect(
    (screen.getByRole('button', { name: 'Repair from source' }) as HTMLButtonElement).disabled,
  ).toBe(true);
});
it('denies the administrator workspace to ordinary viewers without fetching metadata', () => {
  const { request } = setup({ Id: 'owned', Name: 'Original', Type: 'Video' }, false, true);
  expect(screen.getByText('Administrator access required.')).toBeTruthy();
  expect(request).not.toHaveBeenCalled();
});
