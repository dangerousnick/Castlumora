import { verifySurprise } from './surprise-proof';
import { copyFileSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { tmpdir } from 'node:os';
import { expect, type Page, type APIRequestContext } from '@playwright/test';
import { readPreview } from './preview-read';

const canonical = (id: string) => id.replaceAll('-', '').toLowerCase();
async function context(page: Page, request: APIRequestContext, preview: string) {
  const url = new URL(preview);
  if (url.hostname !== '127.0.0.1' || url.port !== '28096')
    throw new Error('Dedicated following fixture required');
  const owner = await page.evaluate(() =>
    JSON.parse(sessionStorage.getItem('castlumora-session')!),
  );
  const headers = { Authorization: `MediaBrowser Token="${owner.token}"` };
  const endpoint = (path: string) => new URL('../' + path, preview).href;
  const info = await (await readPreview(request, endpoint('System/Info'), { headers })).json();
  if (
    !['/tmp/castlumora-preview-fresh/data', '/tmp/castlumora-following-local/data'].includes(
      info.ProgramDataPath,
    )
  )
    throw new Error('Refusing an unrecognized following fixture');
  return { owner, headers, endpoint, root: dirname(info.ProgramDataPath) };
}

export async function verifyFollowing(page: Page, request: APIRequestContext, preview: string) {
  const { owner, headers, endpoint, root } = await context(page, request, preview);
  const source =
    root === '/tmp/castlumora-preview-fresh'
      ? '/tmp/castlumora-preview-fresh-media/Castlumora Test (2026).mp4'
      : '/tmp/castlumora-following-local/media/Follow Test.mp4';
  const directory = mkdtempSync(join(tmpdir(), 'castlumora-following-series-'));
  const show = join(directory, 'Castlumora Following Fixture');
  mkdirSync(join(show, 'Season 01'), { recursive: true });
  writeFileSync(
    join(show, 'tvshow.nfo'),
    '<tvshow><title>Castlumora Following Fixture</title><mpaa>G</mpaa><lockdata>true</lockdata></tvshow>',
  );
  copyFileSync(source, join(show, 'Season 01', 'Castlumora Following Fixture - S01E01.mp4'));
  writeFileSync(
    join(show, 'Season 01', 'Castlumora Following Fixture - S01E01.nfo'),
    '<episodedetails><title>Owned follow fixture</title><season>1</season><episode>1</episode><mpaa>G</mpaa><lockdata>true</lockdata></episodedetails>',
  );
  const name = 'Following proof ' + directory.split('-').at(-1);
  const added = await request.post(
    endpoint(
      'Library/VirtualFolders?name=' +
        encodeURIComponent(name) +
        '&collectionType=tvshows&paths=' +
        encodeURIComponent(directory),
    ),
    {
      headers,
      data: {
        LibraryOptions: {
          EnableRealtimeMonitor: false,
          TypeOptions: ['Series', 'Season', 'Episode'].map((Type) => ({
            Type,
            MetadataFetchers: [],
            ImageFetchers: [],
          })),
        },
      },
    },
  );
  expect(added.ok()).toBe(true);
  expect((await request.post(endpoint('Library/Refresh'), { headers })).ok()).toBe(true);
  let series: { Id: string; Name: string } | undefined;
  await expect
    .poll(
      async () => {
        const response = await readPreview(
          request,
          endpoint('Items?recursive=true&includeItemTypes=Series'),
          { headers },
        );
        series = (await response.json()).Items.find(
          (item: { Name: string }) => item.Name === 'Castlumora Following Fixture',
        );
        return Boolean(series);
      },
      { timeout: 25000 },
    )
    .toBe(true);
  const id = series!.Id;
  const before = await (
    await readPreview(request, endpoint('Castlumora/Presentation/Preferences'), { headers })
  ).json();
  await page.goto(preview + '#/item/' + id);
  const follow = page.getByRole('button', { name: 'Follow series', exact: true });
  await expect(follow).toBeEnabled();
  await follow.click();
  await expect(page.getByRole('button', { name: 'Unfollow series', exact: true })).toBeEnabled();
  await page.reload();
  await expect(page.getByRole('button', { name: 'Unfollow series', exact: true })).toBeEnabled();
  await page.getByRole('link', { name: 'My List', exact: true }).click();
  const panel = page.getByRole('region', { name: 'Followed series', exact: true });
  await expect(
    panel.getByRole('link', { name: 'Details for Castlumora Following Fixture', exact: true }),
  ).toBeVisible();
  page.once('dialog', (dialog) => dialog.dismiss());
  await panel.getByRole('button', { name: 'Stop following all series', exact: true }).click();
  expect(
    (
      await (await readPreview(request, endpoint('Castlumora/Following'), { headers })).json()
    ).seriesIds.map(canonical),
  ).toContain(canonical(id));
  await panel
    .getByRole('button', { name: 'Unfollow Castlumora Following Fixture', exact: true })
    .click();
  await expect(
    panel.getByText('No followed series are currently available to this profile.', { exact: true }),
  ).toBeVisible();
  await page.goto(preview + '#/item/' + id);
  await expect(page.getByRole('button', { name: 'Follow series', exact: true })).toBeEnabled();
  await page.getByRole('button', { name: 'Follow series', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Unfollow series', exact: true })).toBeEnabled();
  await page.goto(preview + '#/notifications');
  await page.getByRole('button', { name: 'Enable episode notifications', exact: true }).click();
  await expect(
    page.getByRole('button', { name: 'Pause episode notifications', exact: true }),
  ).toBeEnabled();
  await expect(page.getByText('0 unread notifications', { exact: true })).toBeVisible();
  copyFileSync(source, join(show, 'Season 01', 'Castlumora Following Fixture - S01E02.mp4'));
  writeFileSync(
    join(show, 'Season 01', 'Castlumora Following Fixture - S01E02.nfo'),
    '<episodedetails><title>Owned new episode notification</title><season>1</season><episode>2</episode><mpaa>G</mpaa><lockdata>true</lockdata></episodedetails>',
  );
  expect((await request.post(endpoint('Library/Refresh'), { headers })).ok()).toBe(true);
  let notificationId = '';
  await expect
    .poll(
      async () => {
        const data = await (
          await readPreview(request, endpoint('Castlumora/Notifications'), { headers })
        ).json();
        const arrival = data.items.find(
          (item: { seriesId: string }) => canonical(item.seriesId) === canonical(id),
        );
        notificationId = arrival?.id || '';
        return Boolean(notificationId);
      },
      { timeout: 25000 },
    )
    .toBe(true);
  const nativeArrival = await (
    await readPreview(
      request,
      endpoint('Items?Ids=' + notificationId + '&Fields=Path&IncludeItemTypes=Episode'),
      { headers },
    )
  ).json();
  expect(nativeArrival.Items).toHaveLength(1);
  expect(nativeArrival.Items[0].Path).toBe(
    join(show, 'Season 01', 'Castlumora Following Fixture - S01E02.mp4'),
  );
  await page.getByRole('button', { name: 'Refresh notifications', exact: true }).click();
  await expect(page.locator('a[href="#/item/' + notificationId + '"]')).toBeVisible();
  await expect(page.getByText('1 unread notifications', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Mark read', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Mark unread', exact: true })).toBeEnabled();
  await page.reload();
  await expect(page.getByText('0 unread notifications', { exact: true })).toBeVisible();
  await verifySurprise(page, request, preview, {
    userId: owner.user.Id,
    headers,
    libraryName: name,
    directory,
  });
  const after = await (
    await readPreview(request, endpoint('Castlumora/Presentation/Preferences'), { headers })
  ).json();
  expect(after.watchlist).toEqual(before.watchlist);
  expect(after.hiddenResume).toEqual(before.hiddenResume);
  writeFileSync(
    join(root, 'following-proof.json'),
    JSON.stringify({ id, ownerId: owner.user.Id, directory, libraryName: name, notificationId }),
    { mode: 0o600 },
  );
}

export async function verifyFollowingRestart(
  page: Page,
  request: APIRequestContext,
  preview: string,
  finishNotifications = false,
) {
  const { owner, headers, endpoint, root } = await context(page, request, preview);
  const witness = JSON.parse(readFileSync(join(root, 'following-proof.json'), 'utf8'));
  expect(canonical(owner.user.Id)).toBe(canonical(witness.ownerId));
  const current = await (
    await readPreview(request, endpoint('Castlumora/Following'), { headers })
  ).json();
  expect(current.seriesIds.map(canonical)).toContain(canonical(witness.id));
  const notices = await (
    await readPreview(request, endpoint('Castlumora/Notifications'), { headers })
  ).json();
  expect(notices.enabled).toBe(true);
  expect(
    notices.items.find(
      (item: { id: string }) => canonical(item.id) === canonical(witness.notificationId),
    ).read,
  ).toBe(true);
  await page.goto(preview + '#/notifications');
  await expect(page.getByRole('button', { name: 'Mark unread', exact: true })).toBeEnabled();
  if (finishNotifications) {
    await page.getByRole('button', { name: 'Dismiss notification', exact: true }).click();
    await expect(
      page.getByText('No episode notifications are currently available.', { exact: true }),
    ).toBeVisible();
    await page.reload();
    await expect(page.getByText('0 unread notifications', { exact: true })).toBeVisible();
    await expect(
      page.getByText('No episode notifications are currently available.', { exact: true }),
    ).toBeVisible();
  }

  await page.getByRole('link', { name: 'My List', exact: true }).click();
  await expect(
    page
      .getByRole('region', { name: 'Followed series', exact: true })
      .getByRole('link', { name: 'Details for Castlumora Following Fixture', exact: true }),
  ).toBeVisible();
}
