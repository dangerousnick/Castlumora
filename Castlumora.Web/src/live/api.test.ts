// @vitest-environment jsdom
import { afterEach, expect, it, vi } from 'vitest';
import { Api } from '../api/client';
import { liveApi } from './api';
const api = new Api('https://example.test/base');
afterEach(() => vi.restoreAllMocks());
it('requests paginated guide and recordings through the permission-aware user APIs', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue({ Items: [], TotalRecordCount: 0 });
  await liveApi(api, 'viewer').programs('start', 'end', 'channel', 100);
  await liveApi(api, 'viewer').recordings(48);
  expect(request.mock.calls[0]).toEqual([
    'LiveTv/Programs',
    {},
    expect.objectContaining({
      UserId: 'viewer',
      ChannelIds: 'channel',
      StartIndex: 100,
      Limit: 100,
    }),
  ]);
  expect(request.mock.calls[1]).toEqual([
    'LiveTv/Recordings',
    {},
    expect.objectContaining({ UserId: 'viewer', StartIndex: 48, Limit: 48 }),
  ]);
});
it('preserves server timer defaults and distinguishes create, update, cancellation and series rules', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue(undefined);
  const live = liveApi(api, 'viewer');
  await live.save({ ProgramId: 'p', FutureOption: true });
  await live.save({ Id: 'existing', ProgramId: 'p' });
  await live.save({ ProgramId: 'p', RecordNewOnly: true }, true);
  await live.cancel('existing');
  await live.cancel('series', true);
  expect(request.mock.calls.map(([path, options]) => [path, options?.method])).toEqual([
    ['LiveTv/Timers', 'POST'],
    ['LiveTv/Timers/existing', 'POST'],
    ['LiveTv/SeriesTimers', 'POST'],
    ['LiveTv/Timers/existing', 'DELETE'],
    ['LiveTv/SeriesTimers/series', 'DELETE'],
  ]);
  expect(JSON.parse(request.mock.calls[0][1]!.body as string).FutureOption).toBe(true);
});

it('treats server defaults as a new schedule even when the server includes a derived ID', async () => {
  vi.spyOn(api, 'request').mockResolvedValue({
    Id: 'derived-before-creation',
    ProgramId: 'p',
    PrePaddingSeconds: 30,
  });
  expect(await liveApi(api, 'viewer').defaults('p')).toEqual({
    Id: undefined,
    ProgramId: 'p',
    PrePaddingSeconds: 30,
  });
});

it('requests server deletion permission and keeps destructive media deletion distinct from library-only removal', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue(undefined);
  const live = liveApi(api, 'viewer');
  await live.recordings(0);
  await live.removeRecording('recording');
  await live.deleteRecordingFile('recording');
  expect(request.mock.calls[0][2]).toMatchObject({ Fields: 'CanDelete', UserId: 'viewer' });
  expect(request.mock.calls.slice(1).map(([path, options]) => [path, options?.method])).toEqual([
    ['LiveTv/Recordings/recording', 'DELETE'],
    ['Items/recording', 'DELETE'],
  ]);
});
