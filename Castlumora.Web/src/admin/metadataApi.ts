import type { Api } from '../api/client';
import { adminApi } from './api';
import type { ObjectData } from './api';
export const sidecarReader = 'Castlumora YouTube sidecar';
export interface MetadataItem extends ObjectData {
  Id: string;
  Name: string;
  Type: string;
  Overview?: string;
  LockData?: boolean;
  LockedFields?: string[];
  ProviderIds?: Record<string, string>;
}
export function metadataApi(api: Api) {
  const item = (id: string) => api.request<MetadataItem>(`Items/${encodeURIComponent(id)}`);
  return {
    item,
    saveCorrections: async (
      id: string,
      changes: Pick<MetadataItem, 'Name' | 'Overview' | 'LockData'>,
    ) => {
      // The inherited endpoint replaces DTO fields. Fetch a complete fresh DTO, never save a search result.
      const current = await item(id);
      return api.request<void>(`Items/${encodeURIComponent(id)}`, {
        method: 'POST',
        body: JSON.stringify({ ...current, ...changes }),
      });
    },
    refresh: (id: string, replace: boolean) =>
      api.request<void>(
        `Items/${encodeURIComponent(id)}/Refresh`,
        { method: 'POST' },
        {
          metadataRefreshMode: 'FullRefresh',
          imageRefreshMode: 'None',
          replaceAllMetadata: replace,
          replaceAllImages: false,
        },
      ),
    saveReaders: async (id: string, enabled: boolean, order: string[]) => {
      const library = (await adminApi(api).libraries()).find((value) => value.ItemId === id);
      if (!library) throw new Error('Library no longer exists. Reload before saving.');
      const disabled = library.LibraryOptions.DisabledLocalMetadataReaders;
      const next = Array.isArray(disabled)
        ? disabled.filter(
            (value): value is string =>
              typeof value === 'string' && value.toLowerCase() !== sidecarReader.toLowerCase(),
          )
        : [];
      if (!enabled) next.push(sidecarReader);
      return adminApi(api).saveLibrary(id, {
        ...library.LibraryOptions,
        DisabledLocalMetadataReaders: next,
        LocalMetadataReaderOrder: order,
      });
    },
  };
}
