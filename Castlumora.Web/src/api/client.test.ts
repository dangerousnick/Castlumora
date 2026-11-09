// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Api, serverUrl, visibleResume } from './client';
import { defaultLayout } from '../config/schema';
import { authenticatedMediaUrl, ticks } from '../player/profile';
const id = '11111111111111111111111111111111';
const session = {
  server: 'https://example.test/media',
  token: 'test-token',
  user: { Id: id, Name: 'Viewer' },
  deviceId: 'test-device',
};
beforeEach(() => localStorage.clear());
afterEach(() => vi.unstubAllGlobals());
describe('API adapter', () => {
  it('preserves reverse proxy base path and authenticates via header, not API query strings', async () => {
    const fetcher = vi.fn().mockResolvedValue(Response.json({ Items: [], TotalRecordCount: 0 }));
    vi.stubGlobal('fetch', fetcher);
    const api = new Api(session.server, session);
    await api.items({ SearchTerm: 'space & time' });
    const [url, options] = fetcher.mock.calls[0];
    expect(url.pathname).toBe('/media/Items');
    expect(url.searchParams.get('SearchTerm')).toBe('space & time');
    expect(url.searchParams.has('api_key')).toBe(false);
    expect(options.headers.Authorization).toContain('Token="test-token"');
  });
  it('only changes authenticated users preference through additive endpoint', async () => {
    const fetcher = vi.fn().mockResolvedValue(Response.json({ hiddenResume: [id], watchlist: [] }));
    vi.stubGlobal('fetch', fetcher);
    await new Api(session.server, session).preference('hiddenResume', id, true);
    expect(fetcher.mock.calls[0][0].pathname).toBe(
      '/media/Castlumora/Presentation/Preferences/hiddenResume/' + id,
    );
    expect(fetcher.mock.calls[0][1].method).toBe('PUT');
    expect(fetcher.mock.calls).toHaveLength(1);
  });
  it('combines category rule groups, deduplicates and applies exclusions', async () => {
    const api = new Api(session.server, session);
    vi.spyOn(api, 'items').mockResolvedValue({
      Items: [
        { Id: id, Name: 'Excluded', Type: 'Movie' },
        { Id: '22222222222222222222222222222222', Name: 'Kept', Type: 'Movie' },
      ],
      TotalRecordCount: 2,
    });
    const items = await api.category({
      id: 'winter',
      title: 'Winter',
      rules: { itemIds: [id], folderIds: [], genres: ['Adventure'], studios: [], excludeIds: [id] },
    });
    expect(items.map((i) => i.Name)).toEqual(['Kept']);
    expect(api.items).toHaveBeenCalledTimes(2);
  });
  it('surfaces authorization errors and never returns response secrets', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('secret', { status: 403 })));
    await expect(new Api(session.server, session).draft()).rejects.toThrow(
      'Your profile cannot access',
    );
  });
  it('falls back only for unavailable presentation APIs, not forbidden access', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 404 })));
    expect((await new Api(session.server, session).layout()).config).toEqual(defaultLayout);
  });
  it('hides episode or entire series from resume without changing watch history', () => {
    const items = [
      { Id: id, SeriesId: '22222222222222222222222222222222', Name: 'Episode', Type: 'Episode' },
    ];
    expect(visibleResume(items, ['22222222-2222-2222-2222-222222222222'])).toEqual([]);
    expect(items).toHaveLength(1);
  });
  it('rejects credential-bearing and executable server URLs', () => {
    for (const url of [
      'javascript:alert(1)',
      'https://user:pass@example.com',
      'https://example.com?token=secret',
    ])
      expect(() => serverUrl(url)).toThrow();
  });
  it('rejects off-server playback URLs before appending a token', () => {
    const api = new Api(session.server, session);
    expect(() => authenticatedMediaUrl(api, 'https://evil.test/video')).toThrow();
    expect(() => authenticatedMediaUrl(api, 'https://example.test/outside/video')).toThrow();
    expect(authenticatedMediaUrl(api, 'Videos/test/stream')).toContain(
      '/media/Videos/test/stream?api_key=test-token',
    );
    expect(ticks(12.5)).toBe(125000000);
  });
});

it('opens the installed legacy client without assuming a Web router version', () => {
  expect(new Api('https://example.test/media', session).legacy()).toBe(
    'https://example.test/media/web/',
  );
});
it('passes source, track, bitrate and exact zero resume selection to playback negotiation', async () => {
  const fetcher = vi
    .fn()
    .mockResolvedValue(Response.json({ MediaSources: [], PlaySessionId: 'p' }));
  vi.stubGlobal('fetch', fetcher);
  await new Api(session.server, session).playback(id, {}, 0, 2, -1, true, {
    mediaSourceId: 'source-version',
    maxBitrate: 2000000,
  });
  const body = JSON.parse(fetcher.mock.calls[0][1].body);
  expect(body).toMatchObject({
    StartTimeTicks: 0,
    AudioStreamIndex: 2,
    SubtitleStreamIndex: -1,
    MediaSourceId: 'source-version',
    MaxStreamingBitrate: 2000000,
    EnableDirectPlay: false,
  });
});
