// @vitest-environment jsdom
import { afterEach, expect, it, vi } from 'vitest';
import { Api } from '../api/client';
import { musicApi, audioProfile } from './api';
const api = new Api('https://example.test/media');
afterEach(() => vi.restoreAllMocks());
it('creates private audio playlists and renames without replacing sharing settings', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue({ Id: 'playlist' });
  const music = musicApi(api, 'viewer');
  await music.createPlaylist('A quiet evening');
  await music.renamePlaylist('playlist', 'New name');
  expect(JSON.parse(request.mock.calls[0][1]!.body as string)).toEqual({
    Name: 'A quiet evening',
    UserId: 'viewer',
    MediaType: 'Audio',
    IsPublic: false,
    Ids: [],
  });
  expect(JSON.parse(request.mock.calls[1][1]!.body as string)).toEqual({ Name: 'New name' });
});
it('removes and reorders playlist entry IDs rather than confusing them with track IDs', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue(undefined);
  const music = musicApi(api, 'viewer');
  await music.remove('playlist', 'entry-1');
  await music.move('playlist', 'entry-2', 0);
  expect(request.mock.calls).toEqual([
    ['Playlists/playlist/Items', { method: 'DELETE' }, { entryIds: 'entry-1' }],
    ['Playlists/playlist/Items/entry-2/Move/0', { method: 'POST' }],
  ]);
});
it('negotiates only audio profiles and a bounded MP3 HTTP fallback', () => {
  vi.spyOn(HTMLMediaElement.prototype, 'canPlayType').mockReturnValue('probably');
  const profile = audioProfile();
  expect(profile.DirectPlayProfiles.every((value) => value.Type === 'Audio')).toBe(true);
  expect(profile.TranscodingProfiles).toEqual([
    expect.objectContaining({ Type: 'Audio', Container: 'mp3', Protocol: 'http' }),
  ]);
});
