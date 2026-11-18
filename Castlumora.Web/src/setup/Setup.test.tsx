// @vitest-environment jsdom
import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import Setup from './Setup';
import { Api, ApiError, integratedServer } from '../api/client';
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  localStorage.clear();
  sessionStorage.clear();
});
it('discovers only integrated server base paths, preserving reverse proxy prefixes', () => {
  expect(integratedServer('https://example.test/media/web/#/browse')).toBe(
    'https://example.test/media',
  );
  expect(integratedServer('http://localhost:5173/')).toBe('');
});
it('uses protected startup endpoints in order and does not persist administrator secrets', async () => {
  vi.spyOn(Api.prototype, 'publicInfo').mockResolvedValue({
    ServerName: '',
    StartupWizardCompleted: false,
  });
  const request = vi.spyOn(Api.prototype, 'request').mockResolvedValue(undefined);
  const completed = vi.fn();
  render(<Setup server="http://localhost:28096" completed={completed} back={() => undefined} />);
  fireEvent.change(screen.getByLabelText('Administrator username'), { target: { value: 'Owner' } });
  fireEvent.change(screen.getByLabelText('Administrator password'), {
    target: { value: 'private-test-password' },
  });
  fireEvent.change(screen.getByLabelText('Confirm administrator password'), {
    target: { value: 'private-test-password' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Create administrator and finish setup' }));
  await waitFor(() => expect(completed).toHaveBeenCalledOnce());
  expect(request.mock.calls.map((call) => call[0])).toEqual([
    'Startup/User',
    'Startup/Configuration',
    'Startup/RemoteAccess',
    'Startup/User',
    'Startup/Complete',
  ]);
  expect(JSON.parse(request.mock.calls[2][1]!.body as string)).toEqual({
    EnableRemoteAccess: false,
  });
  expect(JSON.stringify({ ...localStorage, ...sessionStorage })).not.toContain(
    'private-test-password',
  );
});
it('can finish interrupted setup without overwriting an already configured administrator password', async () => {
  vi.spyOn(Api.prototype, 'publicInfo').mockResolvedValue({
    ServerName: 'Castle',
    StartupWizardCompleted: false,
  });
  const request = vi.spyOn(Api.prototype, 'request').mockImplementation(async (path, options) => {
    if (path === 'Startup/User' && options?.method === 'POST')
      throw new ApiError(403, 'Already set');
  });
  const completed = vi.fn();
  render(<Setup server="http://localhost:28096" completed={completed} back={() => undefined} />);
  fireEvent.change(screen.getByLabelText('Administrator username'), { target: { value: 'Owner' } });
  fireEvent.change(screen.getByLabelText('Administrator password'), {
    target: { value: 'private-test-password' },
  });
  fireEvent.change(screen.getByLabelText('Confirm administrator password'), {
    target: { value: 'private-test-password' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Create administrator and finish setup' }));
  await screen.findByText(/administrator password is already configured/);
  fireEvent.click(screen.getByRole('button', { name: 'Finish setup' }));
  await waitFor(() => expect(completed).toHaveBeenCalledOnce());
  expect(
    request.mock.calls.filter((call) => call[0] === 'Startup/User' && call[1]?.method === 'POST'),
  ).toHaveLength(1);
});
