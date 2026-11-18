// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { Api } from '../api/client';
import { AppContext, type AppState } from '../state';
import AccountPassword from './AccountPassword';

afterEach(() => {
  cleanup();
  localStorage.clear();
  sessionStorage.clear();
});
function fixture({ owner = true, hasPassword = true, admin = false, canEdit = true } = {}) {
  const api = new Api('https://example.test');
  const request = vi.spyOn(api, 'request').mockResolvedValue(undefined);
  const session = {
    server: api.server,
    token: 'retained-session-token',
    user: {
      Id: 'current-user',
      Name: 'Account',
      HasPassword: hasPassword,
      Policy: { IsAdministrator: admin, EnableUserPreferenceAccess: canEdit },
    },
  };
  render(
    <AppContext value={{ api, session } as unknown as AppState}>
      <AccountPassword accountOwner={owner} />
    </AppContext>,
  );
  return { request, session };
}
function fill(current = 'Old account secret', next = 'New account secret', confirmation = next) {
  fireEvent.change(screen.getByLabelText('Current account password'), {
    target: { value: current },
  });
  fireEvent.change(screen.getByLabelText('New account password'), { target: { value: next } });
  fireEvent.change(screen.getByLabelText('Confirm new account password'), {
    target: { value: confirmation },
  });
}
function assertCleared() {
  for (const label of [
    'Current account password',
    'New account password',
    'Confirm new account password',
  ])
    expect((screen.getByLabelText(label) as HTMLInputElement).value).toBe('');
}
it('sends only self-scoped credentials, retains identity/token and clears secrets without browser storage', async () => {
  const { request, session } = fixture();
  localStorage.setItem('unrelated-pairing', 'preserve');
  fill();
  fireEvent.click(screen.getByRole('button', { name: 'Update account password' }));
  await screen.findByText(/Password updated\. This session remains signed in/);
  expect(request).toHaveBeenCalledExactlyOnceWith('Users/current-user/Password', {
    method: 'POST',
    body: JSON.stringify({
      CurrentPw: 'Old account secret',
      NewPw: 'New account secret',
      ResetPassword: false,
    }),
  });
  assertCleared();
  expect(session.token).toBe('retained-session-token');
  expect(session.user.Id).toBe('current-user');
  expect(localStorage.length).toBe(1);
  expect(localStorage.getItem('unrelated-pairing')).toBe('preserve');
  expect(sessionStorage.length).toBe(0);
});
it('does not submit short or mismatched passwords even on a direct form submit', () => {
  const { request } = fixture();
  fill('Old account secret', 'short', 'different');
  expect(
    (screen.getByRole('button', { name: 'Update account password' }) as HTMLButtonElement).disabled,
  ).toBe(true);
  fireEvent.submit(screen.getByRole('form', { name: 'Change account password' }));
  expect(request).not.toHaveBeenCalled();
  expect(screen.getByRole('alert').textContent).toContain('must match');
});
it('reports rejection and clears all secret inputs without claiming success', async () => {
  const { request } = fixture();
  request.mockRejectedValue(new Error('Server rejected password change'));
  fill();
  fireEvent.click(screen.getByRole('button', { name: 'Update account password' }));
  await screen.findByText('Server rejected password change');
  expect(screen.queryByText(/Password updated\./)).toBeNull();
  assertCleared();
});
it.each([
  { owner: false, canEdit: true, message: /viewing profile uses its owning account/ },
  { owner: true, canEdit: false, message: /disabled by your server permissions/ },
])(
  'preserves managed identity and server permission boundaries: %o',
  ({ owner, canEdit, message }) => {
    const { request } = fixture({ owner, canEdit });
    expect(screen.getByText(message)).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Update account password' })).toBeNull();
    expect(request).not.toHaveBeenCalled();
  },
);
it('allows an explicitly passwordless account to set its first password then requires the current password', async () => {
  const { request, session } = fixture({ hasPassword: false });
  fill('');
  fireEvent.click(screen.getByRole('button', { name: 'Update account password' }));
  await screen.findByText(/Password updated\./);
  expect(JSON.parse(request.mock.calls[0][1]!.body as string).CurrentPw).toBe('');
  expect((screen.getByLabelText('Current account password') as HTMLInputElement).required).toBe(
    true,
  );
  expect(session.user.HasPassword).toBe(true);
  assertCleared();
});
it('retains the inherited administrator ability while still supplying the explicit self ID and current password', async () => {
  const { request } = fixture({ admin: true, canEdit: false });
  fill();
  fireEvent.click(screen.getByRole('button', { name: 'Update account password' }));
  await waitFor(() => expect(request).toHaveBeenCalledOnce());
  expect(request.mock.calls[0][0]).toBe('Users/current-user/Password');
});
