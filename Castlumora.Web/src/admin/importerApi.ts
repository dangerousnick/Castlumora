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
