// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AppContext } from '../state';
import type { AppState } from '../state';
import { Api } from '../api/client';
import { defaultLayout } from '../config/schema';
import ImportJobs from './ImportJobs';
import type { ImportJob, ImportQueue } from './importerApi';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});
function setup(jobs: ImportJob[] = []) {
  const api = new Api('https://example.test/base');
  const queue: ImportQueue = {
    available: true,
    message: 'Direct video preview',
    destinations: [{ id: 'library', name: 'Owned videos' }],
    jobs,
  };
  const request = vi
    .spyOn(api, 'request')
    .mockImplementation(async (path) =>
      path === 'Castlumora/Imports/Queue' ? queue : { id: 'saved' },
    );
  const app = {
    api,
    layout: defaultLayout,
    preferences: { watchlist: [], hiddenResume: [] },
    session: {
      server: api.server,
      token: 'test',
      deviceId: 'd',
      user: { Id: 'admin', Name: 'Admin', Policy: { IsAdministrator: true } },
    },
    setPreferences: vi.fn(),
    refreshLayout: vi.fn(),
    logout: vi.fn(),
    notice: '',
  } as AppState;
  render(
    <AppContext value={app}>
      <ImportJobs />
    </AppContext>,
  );
  return request;
}
it('requires rights confirmation and submits typed library identity without filesystem/executable options', async () => {
  const request = setup();
  const submit = await screen.findByRole('button', { name: 'Queue authorized video' });
  expect((submit as HTMLButtonElement).disabled).toBe(true);
  fireEvent.change(screen.getByLabelText('Source URL'), {
    target: { value: 'https://example.test/owned.mp4?private-token' },
  });
  fireEvent.change(screen.getByLabelText('Imported title'), { target: { value: 'Owned clip' } });
  fireEvent.change(screen.getByLabelText('Rights / permission record'), {
    target: { value: 'My own export' },
  });
  fireEvent.click(screen.getByRole('checkbox', { name: /I have the rights/ }));
  fireEvent.click(submit);
  await waitFor(() =>
    expect(request).toHaveBeenCalledWith('Castlumora/Imports/Jobs', {
      method: 'POST',
      body: JSON.stringify({
        url: 'https://example.test/owned.mp4?private-token',
        title: 'Owned clip',
        destinationId: 'library',
        rights: 'My own export',
        rightsConfirmed: true,
        automaticRetries: false,
      }),
    }),
  );
  await waitFor(() =>
    expect((screen.getByLabelText('Source URL') as HTMLInputElement).value).toBe(''),
  );
  expect(
    (screen.getByRole('checkbox', { name: /I have the rights/ }) as HTMLInputElement).checked,
  ).toBe(false);
});
it('renders untrusted titles as text and invokes server cancellation/retry only for eligible states', async () => {
  const base = {
    sourceHost: 'source.example',
    destinationId: 'library',
    attempts: 1,
    bytes: 2048,
    createdAt: '2026-11-11T00:00:00Z',
  };
  const request = setup([
    { ...base, id: 'active', title: '<script>owned</script>', state: 'running' },
    { ...base, id: 'failed', title: 'Failed clip', state: 'failed' },
    { ...base, id: 'done', title: 'Completed clip', state: 'completed' },
  ]);
  const cancel = await screen.findByRole('button', { name: 'Cancel <script>owned</script>' });
  expect(document.querySelector('script')).toBeNull();
  fireEvent.click(cancel);
  await waitFor(() =>
    expect(request).toHaveBeenCalledWith('Castlumora/Imports/Jobs/active/Cancel', {
      method: 'POST',
    }),
  );
  await waitFor(() =>
    expect(
      (screen.getByRole('button', { name: 'Retry Failed clip' }) as HTMLButtonElement).disabled,
    ).toBe(false),
  );
  fireEvent.click(screen.getByRole('button', { name: 'Retry Failed clip' }));
  await waitFor(() =>
    expect(request).toHaveBeenCalledWith('Castlumora/Imports/Jobs/failed/Retry', {
      method: 'POST',
    }),
  );
  expect(screen.queryByRole('button', { name: 'Cancel Completed clip' })).toBeNull();
  expect(screen.getByText(/Imported · library scan requested/)).toBeTruthy();
});

it('explains whitelisted failures and does not echo unknown diagnostic text', async () => {
  setup([
    {
      id: 'network',
      title: 'Network clip',
      sourceHost: 'example.test',
      destinationId: 'library',
      state: 'failed',
      attempts: 1,
      bytes: 0,
      createdAt: '2026-11-13T00:00:00Z',
      error: 'sourceNetwork',
    },
    {
      id: 'unknown',
      title: 'Unknown clip',
      sourceHost: 'example.test',
      destinationId: 'library',
      state: 'failed',
      attempts: 1,
      bytes: 0,
      createdAt: '2026-11-13T00:00:00Z',
      error: 'https://private.invalid/?token=do-not-display',
    },
  ]);
  expect(await screen.findByText(/Check DNS, firewall and direct HTTPS egress/)).toBeTruthy();
  expect(screen.getByText(/do not share private URLs or tokens/)).toBeTruthy();
  expect(document.body.textContent).not.toContain('do-not-display');
});

it('submits explicit opt-in and displays cancellable scheduled retry time', async () => {
  const request = setup([
    {
      id: 'waiting',
      title: 'Waiting clip',
      sourceHost: 'example.test',
      destinationId: 'library',
      state: 'queued',
      attempts: 1,
      bytes: 0,
      createdAt: '2026-11-13T00:00:00Z',
      error: 'sourceRateLimited',
      automaticRetries: true,
      nextAttemptAt: '2030-01-01T00:01:00Z',
    },
  ]);
  expect(await screen.findByText(/Automatic retry scheduled for/)).toBeTruthy();
  expect(document.querySelector('time')?.getAttribute('datetime')).toBe('2030-01-01T00:01:00Z');
  fireEvent.click(screen.getByRole('button', { name: 'Cancel Waiting clip' }));
  await waitFor(() =>
    expect(request).toHaveBeenCalledWith('Castlumora/Imports/Jobs/waiting/Cancel', {
      method: 'POST',
    }),
  );
  fireEvent.change(screen.getByLabelText('Source URL'), {
    target: { value: 'https://example.test/own.mp4' },
  });
  fireEvent.change(screen.getByLabelText('Imported title'), { target: { value: 'Owned' } });
  fireEvent.change(screen.getByLabelText('Rights / permission record'), {
    target: { value: 'My own media' },
  });
  fireEvent.click(screen.getByRole('checkbox', { name: /I have the rights/ }));
  fireEvent.click(screen.getByRole('checkbox', { name: /Automatically retry/ }));
  await waitFor(() =>
    expect(
      (screen.getByRole('button', { name: 'Queue authorized video' }) as HTMLButtonElement)
        .disabled,
    ).toBe(false),
  );
  fireEvent.click(screen.getByRole('button', { name: 'Queue authorized video' }));
  await waitFor(() =>
    expect(request).toHaveBeenCalledWith('Castlumora/Imports/Jobs', {
      method: 'POST',
      body: JSON.stringify({
        url: 'https://example.test/own.mp4',
        title: 'Owned',
        destinationId: 'library',
        rights: 'My own media',
        rightsConfirmed: true,
        automaticRetries: true,
      }),
    }),
  );
});
