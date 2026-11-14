// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AppContext } from '../state';
import type { AppState } from '../state';
import { Api, ApiError } from '../api/client';
import YouTubeImport from './YouTubeImport';

afterEach(cleanup);
function setup() {
  const api = new Api('https://example.test');
  const request = vi.spyOn(api, 'request').mockImplementation(async (path) =>
    path.endsWith('Preview')
      ? {
          previewId: 'opaque',
          videoId: '4Xsn6oe-AEg',
          channelId: 'UCabcdefghijklmnopqrstuv',
          title: 'Reviewed video',
          creator: '<script>creator</script>',
          description: 'Description',
          height: 720,
          maximumBytes: 134217728,
          duration: 60,
          expiresAt: '2030-01-01T00:00:00Z',
        }
      : { id: 'queued' },
  );
  const onQueued = vi.fn(async () => {});
  render(
    <AppContext value={{ api } as AppState}>
      <YouTubeImport
        destinations={[{ id: 'library', name: 'Creator videos' }]}
        onQueued={onQueued}
      />
    </AppContext>,
  );
  return { request, onQueued };
}

it('previews before rights confirmation and submits only its opaque receipt with the destination', async () => {
  const { request, onQueued } = setup();
  expect(screen.queryByRole('button', { name: 'Queue confirmed YouTube video' })).toBeNull();
  fireEvent.change(screen.getByLabelText('YouTube video URL'), {
    target: { value: 'https://youtu.be/4Xsn6oe-AEg' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Preview YouTube video' }));
  const queue = await screen.findByRole('button', { name: 'Queue confirmed YouTube video' });
  expect((queue as HTMLButtonElement).disabled).toBe(true);
  expect(document.querySelector('script')).toBeNull();
  fireEvent.change(screen.getByLabelText('YouTube rights / permission record'), {
    target: { value: 'My own upload' },
  });
  fireEvent.click(screen.getByRole('checkbox', { name: /I have permission/ }));
  fireEvent.click(queue);
  await waitFor(() => expect(onQueued).toHaveBeenCalledOnce());
  expect(request).toHaveBeenCalledWith('Castlumora/Imports/Jobs/YouTube', {
    method: 'POST',
    body: JSON.stringify({
      previewId: 'opaque',
      destinationId: 'library',
      title: 'Reviewed video',
      rights: 'My own upload',
      rightsConfirmed: true,
      automaticRetries: false,
    }),
  });
});

it('invalidates a confirmation whenever the source or resource choices change', async () => {
  setup();
  fireEvent.change(screen.getByLabelText('YouTube video URL'), {
    target: { value: 'https://youtu.be/4Xsn6oe-AEg' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Preview YouTube video' }));
  await screen.findByRole('button', { name: 'Queue confirmed YouTube video' });
  fireEvent.change(screen.getByLabelText('Maximum import size'), { target: { value: '256' } });
  expect(screen.queryByRole('button', { name: 'Queue confirmed YouTube video' })).toBeNull();
});

it('explains a rejected preview without reflecting arbitrary server diagnostics or offering a queue action', async () => {
  const { request } = setup();
  request.mockRejectedValueOnce(
    new ApiError(400, 'untrusted diagnostic containing private details'),
  );
  fireEvent.change(screen.getByLabelText('YouTube video URL'), {
    target: { value: 'https://127.0.0.1/private' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Preview YouTube video' }));
  await waitFor(() =>
    expect(screen.getByRole('status').textContent).toContain('Preview rejected.'),
  );
  expect(screen.getByRole('status').textContent).not.toContain('private details');
  expect(screen.queryByRole('button', { name: 'Queue confirmed YouTube video' })).toBeNull();
});
