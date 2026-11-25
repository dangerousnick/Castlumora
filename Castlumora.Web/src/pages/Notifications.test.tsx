// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { Api, ApiError } from '../api/client';
import { AppContext, type AppState } from '../state';
import Notifications from './Notifications';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});
function mount(api: Api) {
  render(
    <MemoryRouter>
      <AppContext value={{ api } as unknown as AppState}>
        <Notifications />
      </AppContext>
    </MemoryRouter>,
  );
}
it('loads private settings and opts in only after server success', async () => {
  const api = new Api('https://example.test');
  let enabled = false;
  const request = vi.spyOn(api, 'request').mockImplementation(async (_path, options) => {
    if (options?.method === 'PUT') enabled = JSON.parse(options.body as string).enabled;
    return { enabled, unread: 0, items: [] };
  });
  mount(api);
  fireEvent.click(await screen.findByRole('button', { name: 'Enable episode notifications' }));
  await screen.findByRole('button', { name: 'Pause episode notifications' });
  expect(request).toHaveBeenCalledWith('Castlumora/Notifications/Settings', {
    method: 'PUT',
    body: '{"enabled":true}',
  });
});
it('shows a denied update without claiming success or losing the notification', async () => {
  const api = new Api('https://example.test');
  vi.spyOn(api, 'request').mockImplementation(async (_path, options) => {
    if (options?.method === 'PUT') throw new ApiError(403, 'Update denied');
    return {
      enabled: true,
      unread: 1,
      items: [
        {
          id: 'episode',
          seriesId: 'series',
          title: 'Arrival',
          seriesName: 'Our show',
          read: false,
        },
      ],
    };
  });
  mount(api);
  fireEvent.click(await screen.findByRole('button', { name: 'Mark read' }));
  await screen.findByText('Update denied');
  expect(screen.queryByText('Notification updated.')).toBeNull();
  expect(screen.getByRole('link', { name: 'Our show — Arrival' }).getAttribute('href')).toBe(
    '/item/episode',
  );
});
it('persists read and dismissal with exact payloads and refreshes only after success', async () => {
  const api = new Api('https://example.test');
  let read = false;
  let dismissed = false;
  const request = vi.spyOn(api, 'request').mockImplementation(async (_path, options) => {
    if (options?.method === 'PUT') {
      const body = JSON.parse(options.body as string);
      if ('read' in body) read = body.read;
      dismissed ||= !!body.dismissed;
    }
    return {
      enabled: true,
      unread: read || dismissed ? 0 : 1,
      items: dismissed ? [] : [{ id: 'episode', title: 'Arrival', seriesName: 'Our show', read }],
    };
  });
  mount(api);
  fireEvent.click(await screen.findByRole('button', { name: 'Mark read' }));
  await screen.findByRole('button', { name: 'Mark unread' });
  fireEvent.click(screen.getByRole('button', { name: 'Dismiss notification' }));
  await screen.findByText('No episode notifications are currently available.');
  expect(request).toHaveBeenCalledWith('Castlumora/Notifications/episode', {
    method: 'PUT',
    body: '{"read":true}',
  });
  expect(request).toHaveBeenCalledWith('Castlumora/Notifications/episode', {
    method: 'PUT',
    body: '{"dismissed":true}',
  });
  fireEvent.click(screen.getByRole('button', { name: 'Refresh notifications' }));
  await waitFor(() => expect(screen.getByText('0 unread notifications')).toBeTruthy());
});
