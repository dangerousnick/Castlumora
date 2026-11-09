// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { Link, MemoryRouter, Route, Routes } from 'react-router-dom';
import { Api } from '../api/client';
import { AppContext } from '../state';
import type { AppState } from '../state';
import { defaultLayout } from '../config/schema';
import { MusicProvider, useMusic } from './MusicProvider';
const tracks = [
  { Id: 'one', Name: 'Track One', Type: 'Audio', RunTimeTicks: 300000000 },
  { Id: 'two', Name: 'Track Two', Type: 'Audio', RunTimeTicks: 300000000 },
];
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});
function Controls() {
  const music = useMusic()!;
  return (
    <>
      <button onClick={() => music.play(tracks)}>Start album</button>
      <button onClick={() => music.enqueue([tracks[1]])}>Queue second track</button>
      <Link to="/browse">Browse elsewhere</Link>
    </>
  );
}
function setup() {
  const session = {
    server: 'https://example.test',
    token: 'fixture',
    user: { Id: 'u', Name: 'Viewer' },
    deviceId: 'd',
  };
  const api = new Api(session.server, session);
  vi.spyOn(HTMLMediaElement.prototype, 'canPlayType').mockReturnValue('probably');
  vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => {});
  vi.spyOn(HTMLMediaElement.prototype, 'play').mockImplementation(async function (
    this: HTMLMediaElement,
  ) {
    Object.defineProperty(this, 'paused', { configurable: true, value: false });
    this.dispatchEvent(new Event('playing'));
  });
  vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(function (
    this: HTMLMediaElement,
  ) {
    Object.defineProperty(this, 'paused', { configurable: true, value: true });
    this.dispatchEvent(new Event('pause'));
  });
  vi.spyOn(HTMLMediaElement.prototype, 'readyState', 'get').mockReturnValue(1);
  const playback = vi.spyOn(api, 'playback').mockImplementation(async (id) => ({
    PlaySessionId: 'session-' + id,
    MediaSources: [{ Id: 'source-' + id, SupportsDirectPlay: true, MediaStreams: [] }],
  }));
  const report = vi.spyOn(api, 'report').mockResolvedValue(undefined);
  const clean = vi.spyOn(api, 'cleanup').mockResolvedValue(undefined);
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
  render(
    <MemoryRouter>
      <AppContext.Provider value={state}>
        <MusicProvider>
          <Routes>
            <Route path="/" element={<Controls />} />
            <Route path="/browse" element={<h1>Other library</h1>} />
          </Routes>
        </MusicProvider>
      </AppContext.Provider>
    </MemoryRouter>,
  );
  return { playback, report, clean };
}
it('keeps audio alive across navigation and closes only the negotiated session on track change', async () => {
  const { playback, report, clean } = setup();
  fireEvent.click(screen.getByRole('button', { name: 'Start album' }));
  await waitFor(() =>
    expect(report).toHaveBeenCalledWith(
      'Playing',
      expect.objectContaining({ ItemId: 'one', PlaySessionId: 'session-one' }),
    ),
  );
  fireEvent.click(screen.getByRole('link', { name: 'Browse elsewhere' }));
  expect(screen.getByRole('region', { name: 'Music player' })).toBeTruthy();
  expect(clean).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Next track' }));
  await waitFor(() =>
    expect(playback).toHaveBeenCalledWith(
      'two',
      expect.anything(),
      0,
      undefined,
      undefined,
      false,
      expect.anything(),
    ),
  );
  expect(clean).toHaveBeenCalledExactlyOnceWith('session-one');
  expect(report).toHaveBeenCalledWith(
    'Playing/Stopped',
    expect.objectContaining({ ItemId: 'one' }),
  );
});
it('repeats the selected track from exact zero and retains volume across track changes', async () => {
  const { playback } = setup();
  fireEvent.click(screen.getByRole('button', { name: 'Start album' }));
  await screen.findByRole('region', { name: 'Music player' });
  await waitFor(() => expect(playback).toHaveBeenCalledTimes(1));
  fireEvent.change(screen.getByRole('slider', { name: 'Music volume' }), {
    target: { value: '0.4' },
  });
  fireEvent.change(screen.getByRole('combobox', { name: 'Repeat' }), { target: { value: 'one' } });
  fireEvent.ended(document.querySelector('audio')!);
  await waitFor(() => expect(playback).toHaveBeenCalledTimes(2));
  expect(playback.mock.calls[1][2]).toBe(0);
  fireEvent.click(screen.getByRole('button', { name: 'Next track' }));
  await waitFor(() => expect(document.querySelector('audio')!.volume).toBe(0.4));
});
