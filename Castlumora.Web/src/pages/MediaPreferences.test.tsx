// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { Api } from '../api/client';
import { AppContext, type AppState } from '../state';
import MediaPreferences from './MediaPreferences';
afterEach(cleanup);

it('saves only media defaults without an identity or permission payload', async () => {
  const api = new Api('https://example.test');
  const request = vi
    .spyOn(api, 'request')
    .mockImplementation(async (_path, options) =>
      options?.method === 'PUT'
        ? { canEdit: true, ...JSON.parse(options.body as string) }
        : {
            canEdit: true,
            audioLanguage: null,
            subtitleLanguage: null,
            useDefaultAudioTrack: true,
            subtitleMode: 'Default',
          },
    );
  render(
    <AppContext value={{ api } as unknown as AppState}>
      <MediaPreferences />
    </AppContext>,
  );
  fireEvent.change(await screen.findByLabelText('Preferred audio language'), {
    target: { value: 'spa' },
  });
  fireEvent.change(screen.getByLabelText('Preferred subtitle language'), {
    target: { value: 'eng' },
  });
  fireEvent.click(screen.getByRole('checkbox', { name: 'Use source default audio track' }));
  fireEvent.change(screen.getByRole('combobox', { name: 'Subtitle behavior' }), {
    target: { value: 'Always' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Save media preferences' }));
  await waitFor(() =>
    expect(screen.getByRole('status').textContent).toContain('Media preferences saved'),
  );
  expect(request).toHaveBeenCalledWith('Castlumora/Profiles/MediaPreferences', {
    method: 'PUT',
    body: JSON.stringify({
      audioLanguage: 'spa',
      subtitleLanguage: 'eng',
      useDefaultAudioTrack: false,
      subtitleMode: 'Always',
    }),
  });
});

it('does not expose editing when the administrator restricts preferences', async () => {
  const api = new Api('https://example.test');
  vi.spyOn(api, 'request').mockResolvedValue({ canEdit: false });
  render(
    <AppContext value={{ api } as unknown as AppState}>
      <MediaPreferences />
    </AppContext>,
  );
  await screen.findByText('Media preferences are controlled by the server administrator.');
  expect(screen.queryByRole('button', { name: 'Save media preferences' })).toBeNull();
});
