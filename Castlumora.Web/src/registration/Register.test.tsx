// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { Api, ApiError } from '../api/client';
import Register from './Register';
import Login from '../pages/Login';
import { registrationApi } from './api';
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  localStorage.clear();
  sessionStorage.clear();
});
function Location() {
  return <output data-testid="location">{useLocation().search}</output>;
}
it('removes invitation from the URL, confirms the server and submits secrets only in the body', async () => {
  vi.spyOn(Api.prototype, 'publicInfo').mockResolvedValue({
    ServerName: 'Trusted castle',
    StartupWizardCompleted: true,
  });
  const request = vi.spyOn(Api.prototype, 'request').mockImplementation(async (path, options) => {
    if (path === 'Castlumora/Registration' && options?.method === 'POST') return { created: true };
    return { enabled: true, requireInvitation: true };
  });
  const login = vi.spyOn(Api.prototype, 'login');
  render(
    <MemoryRouter
      initialEntries={[
        '/register?server=https%3A%2F%2Fexample.test%2Fmedia&invite=one-time-secret',
      ]}
    >
      <Register />
      <Location />
    </MemoryRouter>,
  );
  await waitFor(() =>
    expect(screen.getByTestId('location').textContent).not.toContain('one-time-secret'),
  );
  fireEvent.click(screen.getByRole('button', { name: 'Check registration' }));
  await screen.findByRole('heading', { name: 'Join Trusted castle' });
  fireEvent.change(screen.getByLabelText('New username'), { target: { value: 'viewer' } });
  fireEvent.change(screen.getByLabelText('New password'), { target: { value: 'strong-password' } });
  fireEvent.change(screen.getByLabelText('Confirm new password'), {
    target: { value: 'strong-password' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Create account' }));
  await screen.findByRole('heading', { name: 'Your account is ready' });
  expect(request).toHaveBeenCalledWith('Castlumora/Registration', {
    method: 'POST',
    body: JSON.stringify({
      username: 'viewer',
      password: 'strong-password',
      invitation: 'one-time-secret',
    }),
  });
  expect(login).not.toHaveBeenCalled();
  expect(JSON.stringify(localStorage)).not.toContain('strong-password');
  expect(JSON.stringify(localStorage)).not.toContain('one-time-secret');
  expect(localStorage.getItem('castlumora-server')).toBe('https://example.test/media');
});
it('automatically reconnects a paired server without sending a password or bypassing sign-in', async () => {
  localStorage.setItem('castlumora-server', 'https://example.test/');
  vi.spyOn(Api.prototype, 'publicInfo').mockResolvedValue({
    ServerName: 'Paired castle',
    StartupWizardCompleted: true,
  });
  vi.spyOn(Api.prototype, 'profiles').mockResolvedValue([]);
  const login = vi.spyOn(Api.prototype, 'login');
  render(
    <MemoryRouter>
      <Login onLogin={vi.fn()} />
    </MemoryRouter>,
  );
  await screen.findByLabelText('Username');
  expect(screen.getByRole('button', { name: 'Sign in' })).toBeTruthy();
  expect((screen.getByLabelText('Password') as HTMLInputElement).value).toBe('');
  expect(login).not.toHaveBeenCalled();
});
it('explains anonymous signup throttling without exposing server response bodies', async () => {
  const api = new Api('https://example.test');
  vi.spyOn(api, 'request').mockRejectedValue(new ApiError(429, 'generic failure'));
  await expect(
    registrationApi(api).register('viewer', 'strong-password', 'invite'),
  ).rejects.toThrow('wait a minute');
});
