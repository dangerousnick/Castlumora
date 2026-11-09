import type { Category, Layout, RowDefinition } from '../config/schema';
import { parsePublished } from '../config/schema';
import type { Item, PlaybackInfo, Preferences, QueryResult, Session, User } from './types';
export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}
export function serverUrl(value: string) {
  const u = new URL(value);
  if (!['http:', 'https:'].includes(u.protocol) || u.username || u.password || u.search || u.hash)
    throw new Error('Use an HTTP(S) server URL without credentials, query, or fragment.');
  return u.href.replace(/\/$/, '');
}
export const sameId = (a: string, b: string) =>
  a.replaceAll('-', '').toLowerCase() === b.replaceAll('-', '').toLowerCase();
export function visibleResume(items: Item[], hidden: string[]) {
  return items.filter(
    (i) => !hidden.some((id) => sameId(i.Id, id) || (!!i.SeriesId && sameId(i.SeriesId, id))),
  );
}
export class Api {
  readonly server: string;
  constructor(
    server: string,
    public session?: Session,
  ) {
    this.server = serverUrl(server);
  }
  url(path: string, params: Record<string, string | number | boolean | undefined> = {}) {
    const url = new URL(this.server + '/' + path.replace(/^\//, ''));
    for (const [k, v] of Object.entries(params))
      if (v !== undefined) url.searchParams.set(k, String(v));
    return url;
  }
  async request<T>(
    path: string,
    options: RequestInit = {},
    params: Record<string, string | number | boolean | undefined> = {},
  ): Promise<T> {
    const device =
      this.session?.deviceId ?? localStorage.getItem('castlumora-device') ?? crypto.randomUUID();
    localStorage.setItem('castlumora-device', device);
    const auth = `MediaBrowser Client="Castlumora Web", Device="Browser", DeviceId="${device}", Version="1.0.0"${this.session ? `, Token="${this.session.token}"` : ''}`;
    const response = await fetch(this.url(path, params), {
      ...options,
      headers: { 'Content-Type': 'application/json', Authorization: auth, ...options.headers },
      referrerPolicy: 'no-referrer',
    });
    if (!response.ok)
      throw new ApiError(
        response.status,
        response.status === 401
          ? 'Session expired. Please sign in again.'
          : response.status === 403
            ? 'Your profile cannot access this feature.'
            : `Server returned ${response.status}. Please try again.`,
      );
    return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
  }
  publicInfo() {
    return this.request<{ ServerName: string; StartupWizardCompleted: boolean }>(
      'System/Info/Public',
    );
  }
  profiles() {
    return this.request<User[]>('Users/Public');
  }
  async login(username: string, password: string) {
    const result = await this.request<{ AccessToken: string; User: User }>(
      'Users/AuthenticateByName',
      { method: 'POST', body: JSON.stringify({ Username: username, Pw: password }) },
    );
    return {
      server: this.server,
      token: result.AccessToken,
      user: result.User,
      deviceId: localStorage.getItem('castlumora-device')!,
    };
  }
  me() {
    return this.request<User>('Users/Me');
  }
  logout() {
    return this.request<void>('Sessions/Logout', { method: 'POST' });
  }
  items(params: Record<string, string | number | boolean | undefined> = {}) {
    return this.request<QueryResult>(
      'Items',
      {},
      {
        UserId: this.session?.user.Id,
        Recursive: true,
        IncludeItemTypes: 'Movie,Series',
        Fields: 'Overview,Genres',
        EnableImages: true,
        Limit: 48,
        ...params,
      },
    );
  }
  item(id: string) {
    return this.request<Item>(`Users/${this.session!.user.Id}/Items/${id}`);
  }
  views() {
    return this.request<QueryResult>(`Users/${this.session!.user.Id}/Views`);
  }
  seasons(seriesId: string) {
    return this.request<QueryResult>(
      `Shows/${seriesId}/Seasons`,
      {},
      { UserId: this.session?.user.Id },
    );
  }
  episodes(seriesId: string, seasonId: string) {
    return this.request<QueryResult>(
      `Shows/${seriesId}/Episodes`,
      {},
      { UserId: this.session?.user.Id, SeasonId: seasonId, Fields: 'Overview' },
    );
  }
  preferences() {
    return this.request<Preferences>('Castlumora/Presentation/Preferences');
  }
  preference(kind: 'watchlist' | 'hiddenResume', id: string, enabled: boolean) {
    return this.request<Preferences>(
      `Castlumora/Presentation/Preferences/${kind}/${id}`,
      { method: 'PUT' },
      { enabled },
    );
  }
  async layout() {
    try {
      return {
        config: parsePublished(
          await this.request<unknown>(
            'Castlumora/Presentation',
            {},
            { schemaVersion: 1, components: 'resume,next,recent,movies,series,category' },
          ),
        ),
        fallback: false,
      };
    } catch (error) {
      if (error instanceof ApiError && error.status === 404)
        return { config: parsePublished(null), fallback: true };
      throw error;
    }
  }
  draft() {
    return this.request<Layout>('Castlumora/Presentation/Draft');
  }
  saveDraft(config: Layout) {
    return this.request<Layout>('Castlumora/Presentation/Draft', {
      method: 'PUT',
      body: JSON.stringify(config),
    });
  }
  publish(config: Layout) {
    return this.request<Layout>('Castlumora/Presentation/Publish', {
      method: 'POST',
      body: JSON.stringify(config),
    });
  }
  async category(category: Category) {
    const r = category.rules;
    const types = r.contentTypes?.join(',') || 'Movie,Series';
    const requests: Promise<QueryResult>[] = [];
    if (r.itemIds.length)
      requests.push(this.items({ Ids: r.itemIds.join(','), IncludeItemTypes: types, Limit: 200 }));
    for (const folderId of r.folderIds)
      requests.push(this.items({ ParentId: folderId, IncludeItemTypes: types }));
    if (r.genres.length)
      requests.push(this.items({ Genres: r.genres.join('|'), IncludeItemTypes: types }));
    if (r.studios.length)
      requests.push(this.items({ Studios: r.studios.join('|'), IncludeItemTypes: types }));
    return [
      ...new Map(
        (await Promise.all(requests))
          .flatMap((r) => r.Items)
          .filter((i) => !r.excludeIds?.some((id) => sameId(id, i.Id)))
          .map((i) => [i.Id, i]),
      ).values(),
    ].slice(0, 48);
  }
  async row(row: RowDefinition, categories: Category[], preferences: Preferences) {
    if (row.kind === 'resume')
      return visibleResume(
        (
          await this.request<QueryResult>(
            'UserItems/Resume',
            {},
            { UserId: this.session?.user.Id, MediaTypes: 'Video', Limit: 100, Fields: 'Overview' },
          )
        ).Items,
        preferences.hiddenResume,
      );
    if (row.kind === 'next')
      return (
        await this.request<QueryResult>(
          'Shows/NextUp',
          {},
          { UserId: this.session?.user.Id, Limit: 20, Fields: 'Overview' },
        )
      ).Items;
    if (row.kind === 'category') {
      const c = categories.find((c) => c.id === row.categoryId);
      return c ? this.category(c) : [];
    }
    return (
      await this.items({
        IncludeItemTypes:
          row.kind === 'movies' ? 'Movie' : row.kind === 'series' ? 'Series' : 'Movie,Series',
        SortBy: 'DateCreated',
        SortOrder: 'Descending',
        Limit: 20,
      })
    ).Items;
  }
  image(item: Item, type: 'Primary' | 'Backdrop' = 'Primary', width = 480) {
    const tag = type === 'Backdrop' ? item.BackdropImageTags?.[0] : item.ImageTags?.Primary;
    return tag
      ? this.url(`Items/${item.Id}/Images/${type}`, { tag, maxWidth: width, quality: 85 }).href
      : undefined;
  }
  channels() {
    return this.request<QueryResult>(
      'LiveTv/Channels',
      {},
      { UserId: this.session?.user.Id, EnableImages: true, AddCurrentProgram: true, Limit: 200 },
    );
  }
  programs() {
    const start = new Date();
    return this.request<QueryResult>(
      'LiveTv/Programs',
      {},
      {
        UserId: this.session?.user.Id,
        MinEndDate: start.toISOString(),
        MaxStartDate: new Date(+start + 3 * 3600000).toISOString(),
        SortBy: 'StartDate',
        Limit: 500,
      },
    );
  }
  playback(
    id: string,
    profile: object,
    position: number,
    audio?: number,
    subtitle?: number,
    forceTranscode = false,
  ) {
    return this.request<PlaybackInfo>(`Items/${id}/PlaybackInfo`, {
      method: 'POST',
      body: JSON.stringify({
        UserId: this.session?.user.Id,
        DeviceProfile: profile,
        StartTimeTicks: position,
        AudioStreamIndex: audio,
        SubtitleStreamIndex: subtitle,
        AutoOpenLiveStream: true,
        EnableDirectPlay: !forceTranscode,
        EnableDirectStream: !forceTranscode,
        EnableTranscoding: true,
        MaxStreamingBitrate: 20000000,
      }),
    });
  }
  report(kind: 'Playing' | 'Playing/Progress' | 'Playing/Stopped', body: object) {
    return this.request<void>(`Sessions/${kind}`, { method: 'POST', body: JSON.stringify(body) });
  }
  async cleanup(playSessionId: string, liveStreamId?: string) {
    const tasks = [
      this.request<void>(
        'Videos/ActiveEncodings',
        { method: 'DELETE' },
        { DeviceId: this.session?.deviceId, PlaySessionId: playSessionId },
      ),
    ];
    if (liveStreamId)
      tasks.push(
        this.request<void>('LiveStreams/Close', { method: 'POST' }, { LiveStreamId: liveStreamId }),
      );
    await Promise.allSettled(tasks);
  }
  legacy() {
    // Let the installed Web version choose its own router and login flow.
    return this.server + '/web/';
  }
}
