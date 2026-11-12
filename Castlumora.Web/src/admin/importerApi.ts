import type { Api } from '../api/client';
export interface ImporterRuntime {
  state: string;
  message: string;
  ready: boolean;
  supported: boolean;
  platform: string;
  expectedVersion: string;
  expectedSha256?: string;
  bundleAvailable: boolean;
  downloadJobsAvailable: boolean;
  releaseUrl: string;
}
export function importerApi(api: Api) {
  return {
    status: () => api.request<ImporterRuntime>('Castlumora/Imports/Runtime'),
    install: () =>
      api.request<ImporterRuntime>('Castlumora/Imports/Runtime/Install', { method: 'POST' }),
  };
}

export interface ImportJob {
  id: string;
  title: string;
  sourceHost: string;
  destinationId: string;
  state: 'queued' | 'running' | 'cancelling' | 'cancelled' | 'failed' | 'completed';
  attempts: number;
  bytes: number;
  sha256?: string;
  error?: string;
  createdAt: string;
}
export interface ImportQueue {
  available: boolean;
  message: string;
  destinations: { id: string; name: string }[];
  jobs: ImportJob[];
}
export function importQueueApi(api: Api) {
  return {
    state: () => api.request<ImportQueue>('Castlumora/Imports/Queue'),
    submit: (value: {
      url: string;
      destinationId: string;
      title: string;
      rights: string;
      rightsConfirmed: boolean;
    }) =>
      api.request<ImportJob>('Castlumora/Imports/Jobs', {
        method: 'POST',
        body: JSON.stringify(value),
      }),
    cancel: (id: string) =>
      api.request<ImportJob>(`Castlumora/Imports/Jobs/${encodeURIComponent(id)}/Cancel`, {
        method: 'POST',
      }),
    retry: (id: string) =>
      api.request<ImportJob>(`Castlumora/Imports/Jobs/${encodeURIComponent(id)}/Retry`, {
        method: 'POST',
      }),
  };
}
