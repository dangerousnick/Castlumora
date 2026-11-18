// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { Api } from '../api/client';
import { AppContext } from '../state';
import type { AppState } from '../state';
import Profiles from './Profiles';

afterEach(cleanup);
it('switches only using account password and replaces the active user-scoped session', async () => {
  const api = new Api('https://example.test');
  const request = vi.spyOn(api, 'request').mockImplementation(async (path) =>
    path.endsWith('/Switch')
      ? {
          AccessToken: 'child-token',
          User: { Id: 'child', Name: 'Kids', Policy: { IsAdministrator: false } },
        }
      : {
          accountId: 'owner',
          accountName: 'Account',
          profiles: [{ id: 'child', name: 'Kids', preset: 'kids', ready: true }],
        },
  );
  const replaceSession = vi.fn();
  const state = {
    api,
    replaceSession,
    session: {
      server: api.server,
      token: 'parent',
      user: { Id: 'owner', Name: 'Account' },
      deviceId: 'device',
    },
  } as unknown as AppState;
  render(
    <MemoryRouter>
      <AppContext value={state}>
        <Profiles />
      </AppContext>
    </MemoryRouter>,
  );
  const select = await screen.findByRole('combobox', { name: 'Viewing profile' });
  fireEvent.change(select, { target: { value: 'child' } });
  fireEvent.change(screen.getByLabelText('Account password'), {
    target: { value: 'account secret' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Switch viewing profile' }));
  await waitFor(() => expect(replaceSession).toHaveBeenCalledOnce());
  expect(request).toHaveBeenCalledWith('Castlumora/Profiles/Switch', {
    method: 'POST',
    body: JSON.stringify({ profileId: 'child', accountPassword: 'account secret' }),
  });
  expect(replaceSession).toHaveBeenCalledWith({
    server: api.server,
    token: 'child-token',
    profileName: 'Kids',
    user: { Id: 'child', Name: 'Kids', Policy: { IsAdministrator: false } },
    deviceId: 'device',
  });
  expect((screen.getByLabelText('Account password') as HTMLInputElement).value).toBe('');
});

it('does not replace the session after denied authentication', async () => {
  const api = new Api('https://example.test');
  vi.spyOn(api, 'request').mockImplementation(async (path) => {
    if (path.endsWith('/Switch')) throw new Error('Denied');
    return {
      accountName: 'Account',
      profiles: [{ id: 'child', name: 'Kids', preset: 'kids', ready: true }],
    };
  });
  const replaceSession = vi.fn();
  render(
    <MemoryRouter>
      <AppContext
        value={
          {
            api,
            replaceSession,
            session: { server: api.server, deviceId: 'device' },
          } as unknown as AppState
        }
      >
        <Profiles />
      </AppContext>
    </MemoryRouter>,
  );
  fireEvent.change(await screen.findByRole('combobox', { name: 'Viewing profile' }), {
    target: { value: 'child' },
  });
  fireEvent.change(screen.getByLabelText('Account password'), { target: { value: 'wrong' } });
  fireEvent.click(screen.getByRole('button', { name: 'Switch viewing profile' }));
  await waitFor(() => expect(screen.getByRole('status').textContent).toContain('Denied'));
  expect(replaceSession).not.toHaveBeenCalled();
});
