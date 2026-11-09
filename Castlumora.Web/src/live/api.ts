import type { Api } from '../api/client';
import type { QueryResult } from '../api/types';
export interface Timer {
  [key: string]: unknown;
  Id?: string;
  Name?: string;
  ChannelName?: string;
  ProgramId?: string;
  StartDate?: string;
  EndDate?: string;
  Status?: string;
}
export function liveApi(api: Api, userId: string) {
  return {
    programs: (start: string, end: string, channelId: string, offset: number) =>
      api.request<QueryResult>(
        'LiveTv/Programs',
        {},
        {
          UserId: userId,
          MinEndDate: start,
          MaxStartDate: end,
          ChannelIds: channelId || undefined,
          SortBy: 'StartDate',
          StartIndex: offset,
          Limit: 100,
          EnableTotalRecordCount: true,
        },
      ),
    recordings: (offset: number) =>
      api.request<QueryResult>(
        'LiveTv/Recordings',
        {},
        {
          UserId: userId,
          StartIndex: offset,
          Limit: 48,
          EnableTotalRecordCount: true,
        },
      ),
    timers: (series = false) =>
      api.request<{ Items: Timer[] }>(`LiveTv/${series ? 'SeriesTimers' : 'Timers'}`),
    defaults: async (programId: string) => {
      const value = await api.request<Timer>('LiveTv/Timers/Defaults', {}, { programId });
      // The server supplies a derived ID even before a timer exists. Creation must use POST /Timers.
      return { ...value, Id: undefined };
    },
    save: (timer: Timer, series = false) =>
      api.request<void>(
        `LiveTv/${series ? 'SeriesTimers' : 'Timers'}${timer.Id ? '/' + encodeURIComponent(timer.Id) : ''}`,
        { method: 'POST', body: JSON.stringify(timer) },
      ),
    cancel: (id: string, series = false) =>
      api.request<void>(`LiveTv/${series ? 'SeriesTimers' : 'Timers'}/${encodeURIComponent(id)}`, {
        method: 'DELETE',
      }),
    removeRecording: (id: string) =>
      api.request<void>(`LiveTv/Recordings/${id}`, { method: 'DELETE' }),
  };
}
