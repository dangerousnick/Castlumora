import type { Api } from '../api/client';
export type SkipMode = 'default' | 'manual' | 'automatic' | 'disabled';
export interface Segment {
  Id?: string;
  Type: string;
  StartTicks: number;
  EndTicks: number;
}
export interface ReviewState {
  revision: number;
  protectedFromTicks?: number | null;
  segments: {
    type: 'Intro' | 'Outro';
    status: 'suggested' | 'approved';
    startTicks: number;
    endTicks: number;
  }[];
}
export function skipperApi(api: Api) {
  return {
    settings: () =>
      api.request<{ analysisEnabled: boolean; defaultMode: SkipMode }>(
        'Castlumora/Skipper/Settings',
      ),
    saveSettings: (value: { analysisEnabled: boolean; defaultMode: SkipMode }) =>
      api.request<void>('Castlumora/Skipper/Settings', {
        method: 'PUT',
        body: JSON.stringify(value),
      }),
    profile: () => api.request<{ mode: SkipMode }>('Castlumora/Skipper/Profile'),
    saveProfile: (mode: SkipMode) =>
      api.request<void>('Castlumora/Skipper/Profile', {
        method: 'PUT',
        body: JSON.stringify({ mode }),
      }),
    segments: (id: string) =>
      api.request<{ Items: Segment[] }>(
        `MediaSegments/${id}`,
        {},
        { includeSegmentTypes: 'Intro,Outro' },
      ),
    review: (id: string) => api.request<ReviewState>(`Castlumora/Skipper/Items/${id}`),
    analyze: (id: string) =>
      api.request<ReviewState>(`Castlumora/Skipper/Items/${id}/Analyze`, { method: 'POST' }),
    save: (id: string, state: ReviewState) =>
      api.request<ReviewState>(`Castlumora/Skipper/Items/${id}`, {
        method: 'PUT',
        body: JSON.stringify(state),
      }),
  };
}
