// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { Api, ApiError } from '../api/client';
import { AppContext, type AppState } from '../state';
import AccountSessions from './AccountSessions';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});
const row = (id: number, current = false, deviceName = current ? 'Phone' : 'TV') => ({
  id,
  current,
  deviceName,
  appName: 'Castlumora Web',
  appVersion: '1.0',
  lastActive: '2026-11-01T12:00:00Z',
});
function view(api: Api) {
  render(
    <AppContext value={{ api } as unknown as AppState}>
      <AccountSessions />
    </AppContext>,
  );
}
it('marks the current sign-in and confirms revocation of only the selected authorization', async () => {
  const api = new Api('https://example.test');
  let sessions = [row(1, true), row(2)];
  const request = vi.spyOn(api, 'request').mockImplementation(async (path) => {
    if (path.endsWith('/2')) {
      sessions = [sessions[0]];
      return;
    }
    return { identityId: 'owner', total: sessions.length, sessions };
  });
  const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
  view(api);
  await screen.findByText('Current sign-in');
  expect(
    within(screen.getByRole('article', { name: 'Phone · Castlumora Web' })).queryByRole('button'),
  ).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Sign out this device' }));
  expect(request).not.toHaveBeenCalledWith('Castlumora/Account/Sessions/2', expect.anything());
  confirm.mockReturnValue(true);
  fireEvent.click(screen.getByRole('button', { name: 'Sign out this device' }));
  await screen.findByText('Saved sign-in revoked.');
  expect(request).toHaveBeenCalledWith('Castlumora/Account/Sessions/2', { method: 'DELETE' });
  await waitFor(() =>
    expect(screen.queryByRole('button', { name: 'Sign out this device' })).toBeNull(),
  );
  expect(screen.getByText('Current sign-in')).toBeTruthy();
});
it('confirms sign-out of other devices without sending an identity or token override', async () => {
  const api = new Api('https://example.test');
  let sessions = [row(1, true), row(2)];
  const request = vi.spyOn(api, 'request').mockImplementation(async (path) => {
    if (path.endsWith('/RevokeOthers')) {
      sessions = [sessions[0]];
      return;
    }
    return { identityId: 'owner', total: sessions.length, sessions };
  });
  vi.spyOn(window, 'confirm').mockReturnValue(true);
  view(api);
  fireEvent.click(await screen.findByRole('button', { name: 'Sign out other devices' }));
  await screen.findByText(
    'Other sign-ins for this identity were revoked. This device remains signed in.',
  );
  expect(request).toHaveBeenCalledWith('Castlumora/Account/Sessions/RevokeOthers', {
    method: 'POST',
    body: '{}',
  });
  await waitFor(() =>
    expect(
      (screen.getByRole('button', { name: 'Sign out other devices' }) as HTMLButtonElement)
        .disabled,
    ).toBe(true),
  );
});
it('retains a previous-page control when concurrent revocation shrinks the inventory', async () => {
  const api = new Api('https://example.test');
  const request = vi
    .spyOn(api, 'request')
    .mockImplementation(async (_path, _options, params) => ({
      identityId: 'owner',
      total: params?.startIndex === 20 ? 1 : 21,
      sessions:
        params?.startIndex === 20 ? [] : Array.from({ length: 20 }, (_, i) => row(i + 1, i === 0)),
    }));
  view(api);
  fireEvent.click(await screen.findByRole('button', { name: 'Next devices' }));
  await screen.findByText('No saved sign-ins on this page.');
  expect(
    (screen.getByRole('button', { name: 'Previous devices' }) as HTMLButtonElement).disabled,
  ).toBe(false);
  fireEvent.click(screen.getByRole('button', { name: 'Previous devices' }));
  await screen.findByText('Current sign-in');
  expect(request).toHaveBeenCalledWith(
    'Castlumora/Account/Sessions',
    {},
    { startIndex: 20, limit: 20 },
  );
});
it('shows failures without claiming an empty or successfully revoked inventory', async () => {
  const api = new Api('https://example.test');
  vi.spyOn(api, 'request').mockRejectedValue(new Error('Owned server unavailable'));
  view(api);
  await screen.findByText('Owned server unavailable');
  expect(screen.queryByText('No saved sign-ins on this page.')).toBeNull();
  expect(screen.queryByRole('button', { name: 'Sign out other devices' })).toBeNull();
});
it('offers a safe fallback on servers without the new endpoint', async () => {
  const api = new Api('https://example.test');
  vi.spyOn(api, 'request').mockRejectedValue(new ApiError(404, 'Not found'));
  view(api);
  await screen.findByText(/Device management requires a newer Castlumora server/);
  expect(screen.queryByRole('button', { name: 'Sign out other devices' })).toBeNull();
});
it('renders untrusted client-supplied names as text', async () => {
  const api = new Api('https://example.test');
  const name = '<img src=x onerror=alert(1)>';
  vi.spyOn(api, 'request').mockResolvedValue({
    identityId: 'owner',
    total: 1,
    sessions: [row(1, true, name)],
  });
  view(api);
  await screen.findByText(name);
  expect(document.querySelector('img')).toBeNull();
});
it('rejects malformed inventory without rendering authorization controls', async () => {
  const api = new Api('https://example.test');
  vi.spyOn(api, 'request').mockResolvedValue({ total: 2, sessions: [{ id: -2 }] });
  view(api);
  await screen.findByText('Invalid server sign-in inventory.');
  expect(screen.queryByRole('button', { name: 'Sign out this device' })).toBeNull();
});
