import type { Api } from '../api/client';
export interface ReadingState {
  schemaVersion?: number;
  format?: 'pdf' | 'epub';
  location: string;
  theme: 'dark' | 'light' | 'sepia';
  fontSize: number;
}
export function readerApi(api: Api) {
  return {
    state: (id: string) => api.request<ReadingState>(`Castlumora/Reader/${id}`),
    save: (id: string, state: ReadingState) =>
      api.request<ReadingState>(`Castlumora/Reader/${id}`, {
        method: 'PUT',
        body: JSON.stringify(state),
      }),
    probe: async (id: string) => {
      const response = await api.response(`Items/${id}/File`, { headers: { Range: 'bytes=0-3' } });
      const type = response.headers.get('content-type')?.split(';')[0].toLowerCase();
      await response.body?.cancel();
      return type;
    },
    epub: async (id: string) => {
      const response = await api.response(`Items/${id}/File`);
      const reader = response.body?.getReader();
      if (!reader) throw new Error('The book stream is unavailable.');
      const chunks: Uint8Array[] = [];
      let size = 0;
      try {
        for (;;) {
          const { done, value } = await reader.read();
          if (done) break;
          size += value.byteLength;
          if (size > 64 * 1024 * 1024)
            throw new Error('This EPUB exceeds the safe in-browser reader size.');
          chunks.push(value);
        }
      } finally {
        await reader.cancel();
      }
      const bytes = new Uint8Array(size);
      let offset = 0;
      for (const chunk of chunks) {
        bytes.set(chunk, offset);
        offset += chunk.length;
      }
      return bytes.buffer;
    },
  };
}
