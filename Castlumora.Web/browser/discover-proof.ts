import { copyFileSync, writeFileSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { expect, type Page, type APIRequestContext } from '@playwright/test';
import { readPreview } from './preview-read';

const canonical = (id: string) => id.replaceAll('-', '').toLowerCase();
type Fixture = {
  userId: string;
  headers: Record<string, string>;
  movieId: string;
  movieDirectory: string;
  movieLibraryName: string;
};
async function session(page: Page) {
  return page.evaluate(() => JSON.parse(sessionStorage.getItem('castlumora-session')!));
}
export async function verifyDiscovery(
  page: Page,
  request: APIRequestContext,
  preview: string,
  fixture: Fixture,
) {
  const target = new URL(preview);
  const password = process.env.CASTLUMORA_PREVIEW_PASSWORD;
  if (
    target.hostname !== '127.0.0.1' ||
    target.port !== '28096' ||
    !fixture.movieDirectory.startsWith('/tmp/castlumora-surprise-movies-') ||
    !password
  )
    throw new Error('Owned private discovery fixture required');
  const endpoint = (path: string) => new URL('../' + path, preview).href;
  const owner = await session(page);
  const candidatePath = join(fixture.movieDirectory, 'Castlumora Discovery Candidate (2026).mp4');
  const restrictedPath = join(fixture.movieDirectory, 'Castlumora Restricted Discovery (2026).mp4');
  copyFileSync(
    join(fixture.movieDirectory, 'Castlumora Surprise Movie (2026).mp4'),
    restrictedPath,
  );
  writeFileSync(
    join(fixture.movieDirectory, 'Castlumora Restricted Discovery (2026).nfo'),
    '<movie><title>Restricted discovery movie</title><genre>Restricted discovery fixture</genre><mpaa>R</mpaa><lockdata>true</lockdata></movie>',
  );
  copyFileSync(join(fixture.movieDirectory, 'Castlumora Surprise Movie (2026).mp4'), candidatePath);
  writeFileSync(
    join(fixture.movieDirectory, 'Castlumora Discovery Candidate (2026).nfo'),
    '<movie><title>Discovery candidate</title><genre>Adventure</genre><mpaa>G</mpaa><lockdata>true</lockdata></movie>',
  );
  expect((await request.post(endpoint('Library/Refresh'), { headers: fixture.headers })).ok()).toBe(
    true,
  );
  let candidateId = '';
  let restrictedId = '';
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
        candidateId =
          data.Items.find((item: { Path: string }) => item.Path === candidatePath)?.Id || '';
        restrictedId =
          data.Items.find((item: { Path: string }) => item.Path === restrictedPath)?.Id || '';
        return Boolean(candidateId && restrictedId);
      },
      { timeout: 25000 },
    )
    .toBe(true);
  for (const [id, title] of [
    [fixture.movieId, 'Discovery baseline'],
    [candidateId, 'Discovery candidate'],
    [restrictedId, 'Restricted discovery movie'],
  ]) {
    const item = await (
      await readPreview(request, endpoint('Items/' + id + '?UserId=' + fixture.userId), {
        headers: fixture.headers,
      })
    ).json();
    item.Name = title;
    item.Genres = [id === restrictedId ? 'Restricted discovery fixture' : 'Adventure'];
    item.OfficialRating = id === restrictedId ? 'R' : 'G';
    expect(
      (await request.post(endpoint('Items/' + id), { headers: fixture.headers, data: item })).ok(),
    ).toBe(true);
  }
  // Seed only owned synthetic viewing data, before measuring the discovery controls.
  expect(
    (
      await request.post(endpoint('UserPlayedItems/' + fixture.movieId), {
        headers: fixture.headers,
      })
    ).ok(),
  ).toBe(true);
  const genreData = await (
    await readPreview(
      request,
      endpoint(
        'Genres?UserId=' + fixture.userId + '&IncludeItemTypes=Movie&Limit=1000&EnableImages=false',
      ),
      { headers: fixture.headers },
    )
  ).json();
  const genre = genreData.Items.find((item: { Name: string }) => item.Name === 'Adventure');
  expect(genre).toBeTruthy();
  await expect
    .poll(
      async () => {
        const groups = await (
          await readPreview(
            request,
            endpoint(
              'Movies/Recommendations?UserId=' + fixture.userId + '&CategoryLimit=4&ItemLimit=8',
            ),
            { headers: fixture.headers },
          )
        ).json();
        return groups.some((group: { Items: { Id: string }[] }) =>
          group.Items.some((item) => canonical(item.Id) === canonical(candidateId)),
        );
      },
      { timeout: 20000 },
    )
    .toBe(true);
  const progress = async () => {
    const data = await (
      await readPreview(
        request,
        endpoint('Items?UserId=' + fixture.userId + '&Ids=' + fixture.movieId + ',' + candidateId),
        { headers: fixture.headers },
      )
    ).json();
    return data.Items.map(
      (item: {
        Id: string;
        UserData: { Played: boolean; PlayCount: number; PlaybackPositionTicks: number };
      }) => ({
        id: canonical(item.Id),
        played: item.UserData.Played,
        count: item.UserData.PlayCount,
        ticks: item.UserData.PlaybackPositionTicks,
      }),
    ).sort((a: { id: string }, b: { id: string }) => a.id.localeCompare(b.id));
  };
  const before = await progress();
  await page.getByRole('link', { name: 'Browse', exact: true }).click();
  await page.getByRole('link', { name: 'Discover recommendations', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Discover your next movie', exact: true }),
  ).toBeVisible();
  await expect(
    page
      .getByRole('region', { name: 'History recommendations', exact: true })
      .getByRole('link', { name: 'Details for Discovery candidate', exact: true }),
  ).toBeVisible();
  const interest = page.getByRole('checkbox', { name: 'Adventure', exact: true });
  await expect(
    page.getByRole('checkbox', { name: 'Restricted discovery fixture', exact: true }),
  ).toBeVisible();
  await expect(interest).not.toBeChecked();
  await interest.click();
  await expect(interest).toBeChecked();
  await expect(
    page
      .getByRole('region', { name: 'Interest recommendations', exact: true })
      .getByRole('link', { name: 'Details for Discovery candidate', exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole('link', { name: 'Details for Discovery candidate', exact: true }),
  ).toHaveCount(1);
  await page.reload();
  await expect(page.getByRole('checkbox', { name: 'Adventure', exact: true })).toBeChecked();
  const libraries = await (
    await readPreview(request, endpoint('Library/VirtualFolders'), { headers: fixture.headers })
  ).json();
  const libraryId = libraries.find(
    (item: { Name: string }) => item.Name === fixture.movieLibraryName,
  ).ItemId;
  const childName = 'Discovery kids fixture';
  const created = await request.post(endpoint('Castlumora/Profiles'), {
    headers: fixture.headers,
    data: { accountId: fixture.userId, name: childName, preset: 'kids', libraries: [libraryId] },
  });
  expect(created.ok()).toBe(true);
  const childId = (await created.json()).id;
  const switchTo = async (label: string) => {
    await page.locator('.profile-menu > summary').click();
    await page.getByRole('link', { name: 'Viewing profiles', exact: true }).click();
    await page
      .getByRole('combobox', { name: 'Viewing profile', exact: true })
      .selectOption({ label });
    await page.getByLabel('Account password', { exact: true }).fill(password);
    await page.getByRole('button', { name: 'Switch viewing profile', exact: true }).click();
  };
  await switchTo(childName + ' (kids)');
  await expect(page.locator('.profile-menu > summary')).toContainText(childName);
  const child = await session(page);
  expect(canonical(child.user.Id)).toBe(canonical(childId));
  await page.getByRole('link', { name: 'Browse', exact: true }).click();
  await page.getByRole('link', { name: 'Discover recommendations', exact: true }).click();
  await expect(page.getByRole('checkbox', { name: 'Adventure', exact: true })).not.toBeChecked();
  await expect(
    page.getByRole('checkbox', { name: 'Restricted discovery fixture', exact: true }),
  ).toHaveCount(0);
  const childFavorites = await (
    await readPreview(
      request,
      endpoint('Genres?UserId=' + child.user.Id + '&IncludeItemTypes=Movie&IsFavorite=true'),
      { headers: { Authorization: `MediaBrowser Token="${child.token}"` } },
    )
  ).json();
  expect(
    childFavorites.Items.some((item: { Id: string }) => canonical(item.Id) === canonical(genre.Id)),
  ).toBe(false);
  await switchTo(owner.user.Name + ' (account)');
  await expect(page.getByRole('link', { name: 'Administration', exact: true })).toBeVisible();
  const returned = await session(page);
  expect(canonical(returned.user.Id)).toBe(canonical(fixture.userId));
  fixture.headers.Authorization = `MediaBrowser Token="${returned.token}"`;
  await page.getByRole('link', { name: 'Browse', exact: true }).click();
  await page.getByRole('link', { name: 'Discover recommendations', exact: true }).click();
  await expect(page.getByRole('checkbox', { name: 'Adventure', exact: true })).toBeChecked();
  expect(await progress()).toEqual(before);
  expect(
    (
      await request.delete(endpoint('Castlumora/Profiles/Managed/' + childId), {
        headers: fixture.headers,
      })
    ).ok(),
  ).toBe(true);
  return { genreId: genre.Id, candidateId };
}
export async function verifyDiscoveryRestart(
  page: Page,
  request: APIRequestContext,
  preview: string,
  root: string,
  headers: Record<string, string>,
) {
  const witness = JSON.parse(readFileSync(join(root, 'following-proof.json'), 'utf8'));
  const endpoint = (path: string) => new URL('../' + path, preview).href;
  const current = await session(page);
  expect(canonical(current.user.Id)).toBe(canonical(witness.ownerId));
  const favorite = await (
    await readPreview(
      request,
      endpoint('Genres?UserId=' + current.user.Id + '&IncludeItemTypes=Movie&IsFavorite=true'),
      { headers },
    )
  ).json();
  expect(
    favorite.Items.some(
      (item: { Id: string }) => canonical(item.Id) === canonical(witness.discovery.genreId),
    ),
  ).toBe(true);
  await page.getByRole('link', { name: 'Browse', exact: true }).click();
  await page.getByRole('link', { name: 'Discover recommendations', exact: true }).click();
  await expect(page.getByRole('checkbox', { name: 'Adventure', exact: true })).toBeChecked();
  await expect(
    page
      .getByRole('region', { name: 'Interest recommendations', exact: true })
      .getByRole('link', { name: 'Details for Discovery candidate', exact: true }),
  ).toBeVisible();
}
