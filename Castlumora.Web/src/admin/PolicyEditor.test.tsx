// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { Api } from '../api/client';
import { AppContext } from '../state';
import type { AppState } from '../state';
import { defaultLayout } from '../config/schema';
import { PolicyEditor } from './PolicyEditor';
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});
it('edits explicit library scope without losing unknown policy fields or offering administrator signup', async () => {
  const session = {
    server: 'https://example.test',
    token: 'fixture',
    user: { Id: 'admin', Name: 'Admin' },
    deviceId: 'd',
  };
  const api = new Api(session.server, session);
  const id = '11111111111111111111111111111111';
  vi.spyOn(api, 'request').mockResolvedValue([{ Name: 'Allowed fixture', ItemId: id }]);
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
  const save = vi.fn().mockResolvedValue(undefined);
  render(
    <AppContext.Provider value={state}>
      <PolicyEditor
        title="Signup policy"
        value={{
          EnableAllFolders: false,
          EnabledFolders: [],
          FutureField: { enabled: true },
          IsAdministrator: false,
        }}
        allowAdministrator={false}
        save={save}
      />
    </AppContext.Provider>,
  );
  fireEvent.click(await screen.findByRole('checkbox', { name: 'Allowed fixture' }));
  expect(screen.queryByRole('checkbox', { name: 'Administrator' })).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Save signup policy' }));
  await waitFor(() =>
    expect(save).toHaveBeenCalledWith(
      expect.objectContaining({
        EnabledFolders: [id],
        FutureField: { enabled: true },
        IsAdministrator: false,
      }),
    ),
  );
});
