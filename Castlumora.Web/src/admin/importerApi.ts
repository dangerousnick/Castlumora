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

export interface YouTubePreview {
  previewId: string;
  videoId: string;
  channelId: string;
  title: string;
  creator: string;
  description: string;
  height: number;
  maximumBytes: number;
  duration: number;
  expiresAt: string;
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
  automaticRetries?: boolean;
  nextAttemptAt?: string;
}
export interface ImportQueue {
  youtubeImportsAvailable?: boolean;
  batchImportsAvailable?: boolean;
  maxBatchEntries?: number;
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
      automaticRetries?: boolean;
    }) =>
      api.request<ImportJob>('Castlumora/Imports/Jobs', {
        method: 'POST',
        body: JSON.stringify(value),
      }),
    submitBatch: (value: {
      entries: { url: string; title: string }[];
      destinationId: string;
      rights: string;
      rightsConfirmed: boolean;
      automaticRetries?: boolean;
    }) =>
      api.request<{ jobs: ImportJob[] }>('Castlumora/Imports/Jobs/Batch', {
        method: 'POST',
        body: JSON.stringify(value),
      }),
    previewYouTube: (value: {
      url: string;
      destinationId: string;
      maximumHeight: number;
      maximumMiB: number;
    }) =>
      api.request<YouTubePreview>('Castlumora/Imports/YouTube/Preview', {
        method: 'POST',
        body: JSON.stringify(value),
      }),
    submitYouTube: (value: {
      previewId: string;
      destinationId: string;
      title: string;
      rights: string;
      rightsConfirmed: boolean;
      automaticRetries: boolean;
    }) =>
      api.request<ImportJob>('Castlumora/Imports/Jobs/YouTube', {
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

export function importFailureText(code: string): string {
  const messages: Record<string, string> = {
    certificateRejected:
      'The source HTTPS certificate was rejected. Fix source trust; do not disable certificate validation.',
    sizeLimit:
      'The source exceeds this preview’s size limit. Choose an authorized video within the displayed limit.',
    sourceDenied:
      'The source denied access. Verify your permission and source availability before retrying.',
    sourceNetwork:
      'The server could not reach the source. Check DNS, firewall and direct HTTPS egress; proxy-only networks are not supported yet.',
    sourceRateLimited: 'The source is rate limiting requests. Wait before retrying.',
    unsupportedSource:
      'Choose a supported direct HTTPS MP4 file, or preview a single authorized YouTube video. Playlists and channels are not supported yet.',
    incompleteOutput:
      'No complete media/metadata pair was produced. Check the source format and displayed size limit before retrying.',
    unexpectedOutput:
      'Unexpected downloader files were rejected. Retain the job ID and review server diagnostics; do not relax path checks.',
    downloadFailed:
      'The download failed. Verify source availability and the supported format before retrying.',
    timeLimit:
      'The import reached its execution time limit. Check the source and server connection before retrying.',
    permissionDenied:
      'The requesting administrator or destination permissions changed. Review them before retrying.',
    interrupted:
      'The previous attempt was interrupted. Review its state and attempt limit before retrying.',
  };
  return Object.hasOwn(messages, code)
    ? messages[code]
    : 'The import failed. Use the job ID to inspect server diagnostics; do not share private URLs or tokens.';
}
