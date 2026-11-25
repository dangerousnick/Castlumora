import { verifyDiscovery } from './discover-proof';
import { copyFileSync, mkdtempSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { expect, type Page, type APIRequestContext, type Request } from '@playwright/test';
import { readPreview } from './preview-read';

const canonical = (id: string) => id.replaceAll('-', '').toLowerCase();
type NativeItem = {
  Id: string;
  Path: string;
  Type: string;
  UserData?: { Played?: boolean; PlayCount?: number; PlaybackPositionTicks?: number };
};
export async function verifySurprise(
  page: Page,
  request: APIRequestContext,
  preview: string,
  fixture: {
    userId: string;
    headers: Record<string, string>;
    libraryName: string;
    directory: string;
  },
) {
  const target = new URL(preview);
  if (
    target.hostname !== '127.0.0.1' ||
    target.port !== '28096' ||
    !fixture.directory.startsWith('/tmp/castlumora-following-series-')
  )
    throw new Error('Owned Surprise Me fixture required');
  const endpoint = (path: string) => new URL('../' + path, preview).href;
  const views = await (
    await readPreview(request, endpoint('Users/' + fixture.userId + '/Views'), {
      headers: fixture.headers,
    })
  ).json();
  const library = views.Items.find((item: { Name: string }) => item.Name === fixture.libraryName);
  expect(library).toBeTruthy();
  const catalog = async (): Promise<NativeItem[]> =>
    (
      await (
        await readPreview(
          request,
          endpoint(
            'Items?UserId=' +
              fixture.userId +
              '&ParentId=' +
              library.Id +
              '&Recursive=true&IncludeItemTypes=Episode&Fields=Path',
          ),
          { headers: fixture.headers },
        )
      ).json()
    ).Items;
  const before = await catalog();
  expect(before).toHaveLength(2);
  expect(before.every((item) => item.Path.startsWith(fixture.directory + '/'))).toBe(true);
  const movieDirectory = mkdtempSync(join(tmpdir(), 'castlumora-surprise-movies-'));
  const moviePath = join(movieDirectory, 'Castlumora Surprise Movie (2026).mp4');
  copyFileSync(before[0].Path, moviePath);
  writeFileSync(
    join(movieDirectory, 'Castlumora Surprise Movie (2026).nfo'),
    '<movie><title>Castlumora Surprise Movie</title><mpaa>G</mpaa><lockdata>true</lockdata></movie>',
  );
  const movieName = 'Surprise movie ' + movieDirectory.split('-').at(-1);
  expect(
    (
      await request.post(
        endpoint(
          'Library/VirtualFolders?name=' +
            encodeURIComponent(movieName) +
            '&collectionType=movies&paths=' +
            encodeURIComponent(movieDirectory),
        ),
        {
          headers: fixture.headers,
          data: {
            LibraryOptions: {
              EnableRealtimeMonitor: false,
              TypeOptions: [{ Type: 'Movie', MetadataFetchers: [], ImageFetchers: [] }],
            },
          },
        },
      )
    ).ok(),
  ).toBe(true);
  expect((await request.post(endpoint('Library/Refresh'), { headers: fixture.headers })).ok()).toBe(
    true,
  );
  let movie: NativeItem | undefined;
  await expect
    .poll(
      async () => {
        const data = await (
          await readPreview(
            request,
            endpoint(
              'Items?UserId=' +
                fixture.userId +
                '&Recursive=true&IncludeItemTypes=Movie&Fields=Path',
            ),
            { headers: fixture.headers },
          )
        ).json();
        movie = data.Items.find((item: NativeItem) => item.Path === moviePath);
        return Boolean(movie);
      },
      { timeout: 25000 },
    )
    .toBe(true);
  const movieViews = await (
    await readPreview(request, endpoint('Users/' + fixture.userId + '/Views'), {
      headers: fixture.headers,
    })
  ).json();
  const movieLibrary = movieViews.Items.find((item: { Name: string }) => item.Name === movieName);
  expect(movieLibrary).toBeTruthy();
  const history = (items: NativeItem[]) =>
    items
      .map((item) => ({
        id: canonical(item.Id),
        played: item.UserData?.Played ?? false,
        count: item.UserData?.PlayCount ?? 0,
        ticks: item.UserData?.PlaybackPositionTicks ?? 0,
      }))
      .sort((a, b) => a.id.localeCompare(b.id));
  const calls: Record<string, string | null>[] = [];
  const observe = (request: Request) => {
    const url = new URL(request.url());
    if (
      url.origin !== target.origin ||
      url.pathname !== '/Items' ||
      url.searchParams.get('SortBy') !== 'Random'
    )
      return;
    calls.push(
      Object.fromEntries(
        ['UserId', 'ParentId', 'IncludeItemTypes', 'IsPlayed', 'ExcludeItemIds', 'Limit'].map(
          (key) => [key, url.searchParams.get(key)],
        ),
      ),
    );
  };
  page.on('request', observe);
  try {
    await page.getByRole('button', { name: 'Surprise Me', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Surprise Me', level: 1 })).toBeVisible();
    await expect(page.getByRole('combobox', { name: 'Library', exact: true })).toBeEnabled();
    await page.getByRole('combobox', { name: 'Media type', exact: true }).selectOption('Episode');
    await page.getByRole('combobox', { name: 'Library', exact: true }).selectOption(library.Id);
    await page
      .getByRole('combobox', { name: 'Watch status', exact: true })
      .selectOption('unplayed');
    const picked = async () => {
      const link = page
        .getByRole('region', { name: 'Surprise pick', exact: true })
        .getByRole('link', { name: 'Play pick', exact: true });
      await expect(link).toBeVisible();
      const id = (await link.getAttribute('href'))!.split('/').at(-1)!;
      const native = before.find((item) => canonical(item.Id) === canonical(id));
      expect(native?.Type).toBe('Episode');
      expect(native?.Path.startsWith(fixture.directory + '/')).toBe(true);
      return id;
    };
    await page.getByRole('button', { name: 'Choose a title', exact: true }).click();
    const first = await picked();
    await page.getByRole('button', { name: 'Reroll', exact: true }).click();
    const second = await picked();
    expect(canonical(second)).not.toBe(canonical(first));
    await page.getByRole('button', { name: 'Reroll', exact: true }).click();
    await expect(
      page.getByText(
        'No other matching titles are available. Change filters or start over to include previous picks.',
        { exact: true },
      ),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Start over', exact: true }).click();
    const restarted = await picked();
    expect(calls).toHaveLength(4);
    for (const call of calls) {
      expect(canonical(call.UserId!)).toBe(canonical(fixture.userId));
      expect(canonical(call.ParentId!)).toBe(canonical(library.Id));
      expect(call.IncludeItemTypes).toBe('Episode');
      expect(call.IsPlayed).toBe('false');
      expect(call.Limit).toBe('1');
    }
    expect(calls[0].ExcludeItemIds).toBeNull();
    expect(calls[1].ExcludeItemIds!.split(',').map(canonical)).toEqual([canonical(first)]);
    expect(calls[2].ExcludeItemIds!.split(',').map(canonical)).toEqual([
      canonical(first),
      canonical(second),
    ]);
    expect(calls[3].ExcludeItemIds).toBeNull();
    expect(history(await catalog())).toEqual(history(before));
    await page.getByRole('link', { name: 'Open picked details', exact: true }).click();
    await expect(page).toHaveURL(preview + '#/item/' + restarted);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.getByRole('button', { name: 'Surprise Me', exact: true }).click();
    await expect(page.getByRole('combobox', { name: 'Library', exact: true })).toBeEnabled();
    await page
      .getByRole('combobox', { name: 'Library', exact: true })
      .selectOption(movieLibrary.Id);
    await page
      .getByRole('combobox', { name: 'Watch status', exact: true })
      .selectOption('unplayed');
    await page.getByRole('button', { name: 'Choose a title', exact: true }).click();
    const movieLink = page
      .getByRole('region', { name: 'Surprise pick', exact: true })
      .getByRole('link', { name: 'Play pick', exact: true });
    await expect(movieLink).toHaveAttribute('href', '#/play/' + movie!.Id);
    expect(calls).toHaveLength(5);
    expect(calls[4].IncludeItemTypes).toBe('Movie');
    expect(canonical(calls[4].ParentId!)).toBe(canonical(movieLibrary.Id));
    expect(calls[4].IsPlayed).toBe('false');
    await page.getByRole('link', { name: 'Open picked details', exact: true }).click();
    await expect(page).toHaveURL(preview + '#/item/' + movie!.Id);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.getByRole('button', { name: 'Surprise Me', exact: true }).click();
    await expect(page.getByRole('combobox', { name: 'Library', exact: true })).toBeEnabled();
    await page
      .getByRole('combobox', { name: 'Library', exact: true })
      .selectOption(movieLibrary.Id);
    await page.getByRole('combobox', { name: 'Watch status', exact: true }).selectOption('played');
    await page.getByRole('button', { name: 'Choose a title', exact: true }).click();
    await expect(
      page.getByText(
        'No other matching titles are available. Change filters or start over to include previous picks.',
        { exact: true },
      ),
    ).toBeVisible();
    expect(calls).toHaveLength(6);
    expect(calls[5].IncludeItemTypes).toBe('Movie');
    expect(canonical(calls[5].ParentId!)).toBe(canonical(movieLibrary.Id));
    expect(calls[5].IsPlayed).toBe('true');
    const retainedMovie = await (
      await readPreview(
        request,
        endpoint('Items?UserId=' + fixture.userId + '&Ids=' + movie!.Id + '&Fields=Path'),
        { headers: fixture.headers },
      )
    ).json();
    expect(history(retainedMovie.Items)).toEqual(history([movie!]));
    return await verifyDiscovery(page, request, preview, {
      userId: fixture.userId,
      headers: fixture.headers,
      movieId: movie!.Id,
      movieDirectory,
      movieLibraryName: movieName,
    });
  } finally {
    page.off('request', observe);
  }
}
