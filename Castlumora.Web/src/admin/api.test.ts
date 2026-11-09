// @vitest-environment jsdom
import { afterEach, expect, it, vi } from 'vitest';
import { Api } from '../api/client';
import { adminApi } from './api';
const api = new Api('https://example.test/base', {
  server: 'https://example.test/base',
  token: 'fixture',
  deviceId: 'd',
  user: { Id: 'u', Name: 'Admin' },
});
afterEach(() => vi.restoreAllMocks());
it('creates a library using body path infos while preserving the server base URL', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue(undefined);
  await adminApi(api).createLibrary('Family & Friends', 'movies', '/media/A, B');
  expect(request).toHaveBeenCalledWith(
    'Library/VirtualFolders',
    expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({
        LibraryOptions: { PathInfos: [{ Path: '/media/A, B' }], EnableRealtimeMonitor: true },
      }),
    }),
    { name: 'Family & Friends', collectionType: 'movies', refreshLibrary: true },
  );
});
it('saves complete existing options without dropping unknown backend fields', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue(undefined);
  const original = {
    HardwareAccelerationType: 'none',
    FutureFeature: { enabled: true },
    UnknownCodecs: ['a', 'b'],
  };
  await adminApi(api).saveConfig({ ...original, HardwareAccelerationType: 'vaapi' }, 'encoding');
  expect(JSON.parse(request.mock.calls[0][1]!.body as string)).toEqual({
    ...original,
    HardwareAccelerationType: 'vaapi',
  });
});
it('uses existing administrator policy endpoints for permissions and task commands', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue(undefined);
  await adminApi(api).savePolicy('user-id', { EnableLiveTvAccess: false });
  await adminApi(api).runTask('task-id');
  await adminApi(api).stopTask('task-id');
  expect(request.mock.calls.map((c) => [c[0], c[1]?.method])).toEqual([
    ['Users/user-id/Policy', 'POST'],
    ['ScheduledTasks/Running/task-id', 'POST'],
    ['ScheduledTasks/Running/task-id', 'DELETE'],
  ]);
});
