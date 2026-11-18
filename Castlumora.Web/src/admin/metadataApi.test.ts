// @vitest-environment jsdom
import { afterEach, expect, it, vi } from 'vitest';
import { Api } from '../api/client';
import { metadataApi, sidecarReader } from './metadataApi';
afterEach(() => vi.restoreAllMocks());
it('fetches a full fresh DTO and preserves unrelated metadata and locks when saving corrections', async () => {
  const api = new Api('https://example.test/base');
  const original = {
    Id: 'id',
    Name: 'Before',
    Type: 'Video',
    OfficialRating: 'PG',
    LockedFields: ['OfficialRating'],
    ProviderIds: { YouTube: 'AbCdEf123_-' },
    FutureField: { keep: true },
  };
  const request = vi
    .spyOn(api, 'request')
    .mockResolvedValueOnce(original)
    .mockResolvedValueOnce(undefined);
  await metadataApi(api).saveCorrections('id', {
    Name: 'Corrected',
    Overview: 'Description',
    LockData: true,
  });
  expect(request.mock.calls[0]).toEqual(['Items/id']);
  expect(JSON.parse(request.mock.calls[1][1]!.body as string)).toEqual({
    ...original,
    Name: 'Corrected',
    Overview: 'Description',
    LockData: true,
  });
});
it('preserves unknown library options and other disabled readers when toggling the sidecar provider', async () => {
  const api = new Api('https://example.test/base');
  const options = {
    DisabledLocalMetadataReaders: ['Other reader', sidecarReader.toUpperCase()],
    FutureOption: { keep: true },
  };
  const request = vi
    .spyOn(api, 'request')
    .mockResolvedValueOnce([{ ItemId: 'library', LibraryOptions: options }])
    .mockResolvedValueOnce(undefined);
  await metadataApi(api).saveReaders('library', true, ['Nfo', sidecarReader]);
  expect(JSON.parse(request.mock.calls[1][1]!.body as string)).toEqual({
    Id: 'library',
    LibraryOptions: {
      ...options,
      DisabledLocalMetadataReaders: ['Other reader'],
      LocalMetadataReaderOrder: ['Nfo', sidecarReader],
    },
  });
});
it('keeps ordinary refresh distinct from explicit source replacement and never requests image replacement', async () => {
  const api = new Api('https://example.test/base');
  const request = vi.spyOn(api, 'request').mockResolvedValue(undefined);
  await metadataApi(api).refresh('id', false);
  await metadataApi(api).refresh('id', true);
  expect(request.mock.calls.map((call) => call[2])).toEqual([
    {
      metadataRefreshMode: 'FullRefresh',
      imageRefreshMode: 'None',
      replaceAllMetadata: false,
      replaceAllImages: false,
    },
    {
      metadataRefreshMode: 'FullRefresh',
      imageRefreshMode: 'None',
      replaceAllMetadata: true,
      replaceAllImages: false,
    },
  ]);
});
