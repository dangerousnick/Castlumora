// Explicitly opt-in. All files and accounts belong to the marked disposable loopback fixture.
import { test, expect } from '@playwright/test';
import { existsSync } from 'node:fs';
const server = process.env.CASTLUMORA_TEST_SERVER;
const username = process.env.CASTLUMORA_TEST_USER;
const password = process.env.CASTLUMORA_TEST_PASSWORD;
test.beforeEach(async ({ page, request }) => {
  test.setTimeout(90000);
  page.setDefaultTimeout(15000);
  page.setDefaultNavigationTimeout(30000);
  test.skip(
    process.env.CASTLUMORA_TEST_SHELF !== '1' || !server || !username || password === undefined,
  );
  if (
    !['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server!).hostname) ||
    !existsSync('/tmp/castlumora-web-real-server/.castlumora-disposable')
  )
    throw new Error('A marked disposable loopback fixture is required.');
  const fixtureAuth = await request.post(server! + '/Users/AuthenticateByName', {
    headers: {
      Authorization:
        'MediaBrowser Client="Shelf guard", Device="Disposable", DeviceId="shelf-fixture-guard", Version="1"',
    },
    data: { Username: username, Pw: password },
  });
  expect(fixtureAuth.ok()).toBe(true);
  const fixtureSession = await fixtureAuth.json();
  const fixtureInfo = await request.get(server! + '/System/Info', {
    headers: { Authorization: 'MediaBrowser Token="' + fixtureSession.AccessToken + '"' },
  });
  expect((await fixtureInfo.json()).ProgramDataPath).toBe('/tmp/castlumora-web-real-server/data');
  await page.goto('/');
  await page.getByLabel('Server address').fill(server!);
  await page.getByRole('button', { name: 'Connect', exact: true }).click();
  await page.getByLabel('Username').fill(username!);
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Browse', exact: true })).toBeVisible();
});

test('native shelf: photo navigation and rotation', async ({ page }) => {
  await page.goto('/#/shelf/photos');
  await page.getByLabel('Photo view').selectOption('Photo');
  await page.getByRole('link', { name: 'Details for Blue Castle', exact: true }).click();
  await expect
    .poll(() =>
      page.locator('.photo-canvas img').evaluate((image: HTMLImageElement) => image.naturalWidth),
    )
    .toBeGreaterThan(0);
  await page.getByRole('button', { name: 'Rotate photo', exact: true }).click();
  await expect(page.locator('.photo-canvas img')).toHaveAttribute('style', /rotate\(90deg\)/);
  await page.getByRole('button', { name: 'Next photo', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Purple Castle', exact: true })).toBeVisible();
});

test('native shelf: PDF resume and EPUB chapters', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('/#/shelf/books');
  await page.getByRole('link', { name: 'Details for Castle PDF', exact: true }).click();
  await page.getByRole('link', { name: 'Open reader', exact: true }).click();
  await expect
    .poll(
      () =>
        page.locator('.pdf-canvas canvas').evaluate((canvas: HTMLCanvasElement) => canvas.width),
      { timeout: 20000 },
    )
    .toBeGreaterThan(300);
  await page.getByLabel('Book page', { exact: true }).fill('2');
  await expect(
    page.getByText('Reading position saved for your profile.', { exact: true }),
  ).toBeVisible();
  const saved = page.waitForResponse(
    (r) => r.url().includes('/Castlumora/Reader/') && r.request().method() === 'PUT' && r.ok(),
  );
  await page.getByLabel('Reader theme').selectOption('sepia');
  await saved;
  await page.reload();
  await expect(page.getByLabel('Book page', { exact: true })).toHaveValue('2');
  await expect(page.getByLabel('Reader theme')).toHaveValue('sepia');
  await page.goto('/#/shelf/books');
  await page.getByRole('link', { name: 'Details for Castle EPUB', exact: true }).click();
  await page.getByRole('link', { name: 'Open reader', exact: true }).click();
  await expect(page.getByLabel('Book chapter')).toContainText('Second castle');
  await page.getByLabel('Book chapter').selectOption('two.xhtml');
  await expect(
    page.frameLocator('.epub-canvas iframe').getByRole('heading', { name: 'Castle chapter two' }),
  ).toBeVisible();
  await expect(
    page.getByText('Reading position saved for your profile.', { exact: true }),
  ).toBeVisible();
  await page.reload();
  await expect(
    page.frameLocator('.epub-canvas iframe').getByRole('heading', { name: 'Castle chapter two' }),
  ).toBeVisible();
  expect(errors).toEqual([]);
});

test('native shelf: create, edit and delete a collection while retaining the movie', async ({
  page,
  request,
}) => {
  const header =
    'MediaBrowser Client="Shelf verification", Device="Disposable", DeviceId="shelf-collection-check", Version="1"';
  const authResponse = await request.post(server! + '/Users/AuthenticateByName', {
    headers: { Authorization: header },
    data: { Username: username, Pw: password },
  });
  expect(authResponse.ok()).toBe(true);
  const auth = await authResponse.json();
  const headers = { Authorization: header + ', Token="' + auth.AccessToken + '"' };
  const info = await request.get(server! + '/System/Info', { headers });
  expect((await info.json()).ProgramDataPath).toBe('/tmp/castlumora-web-real-server/data');
  const name = 'Synthetic Shelf Collection ' + Date.now();
  let collectionId: string | undefined;
  try {
    await page.goto('/#/shelf/collections');
    await page.getByLabel('New collection name', { exact: true }).fill(name);
    const created = page.waitForResponse(
      (r) => new URL(r.url()).pathname.endsWith('/Collections') && r.request().method() === 'POST',
    );
    await page.getByRole('button', { name: 'Create collection', exact: true }).click();
    const response = await created;
    expect(response.ok()).toBe(true);
    collectionId = (await response.json()).Id;
    await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
    await page.getByText('Add media to collection', { exact: true }).click();
    const picker = page.locator('.media-picker');
    await picker
      .getByLabel('Add media to collection search', { exact: true })
      .fill('Castlumora Test');
    await picker.getByRole('button', { name: 'Search', exact: true }).click();
    await picker.getByRole('button', { name: /Castlumora Test/ }).click();
    await expect(
      page.getByRole('link', { name: 'Details for Castlumora Test', exact: true }),
    ).toBeVisible();
    await page
      .getByRole('button', { name: 'Remove Castlumora Test from collection', exact: true })
      .click();
    await expect(
      page.getByRole('link', { name: 'Details for Castlumora Test', exact: true }),
    ).toHaveCount(0);
    page.once('dialog', (dialog) => dialog.accept());
    await page.getByRole('button', { name: 'Delete collection', exact: true }).click();
    await expect(
      page.getByRole('heading', { name: 'Your collections', exact: true }),
    ).toBeVisible();
    collectionId = undefined;
    const movies = await request.get(server! + '/Items', {
      headers,
      params: {
        UserId: auth.User.Id,
        Recursive: true,
        IncludeItemTypes: 'Movie',
        SearchTerm: 'Castlumora Test',
      },
    });
    expect(
      (await movies.json()).Items.some((item: { Name: string }) => item.Name === 'Castlumora Test'),
    ).toBe(true);
  } finally {
    if (collectionId) await request.delete(server! + '/Items/' + collectionId, { headers });
  }
});
