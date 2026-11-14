// Opt-in only: actual extracted archive running on the dedicated fresh-install loopback port.
import { test, expect } from '@playwright/test';
const preview = process.env.CASTLUMORA_PREVIEW_URL;
const password = process.env.CASTLUMORA_PREVIEW_PASSWORD;
test('packaged preview: fresh setup, login, administration and logout', async ({
  page,
  request,
}) => {
  test.skip(!preview || !password, 'Requires a separately launched fresh packaged preview.');
  const target = new URL(preview!);
  if (target.hostname !== '127.0.0.1' || target.port !== '28096')
    throw new Error('Fresh package tests require the dedicated loopback preview port.');
  test.setTimeout(90000);
  page.setDefaultTimeout(20000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  const manifest = await request.get(new URL('BUILD_INFO.json', preview!).href);
  expect(manifest.ok()).toBe(true);
  expect((await manifest.json()).selfContainedDotnet).toBe(true);
  await page.goto(preview!);
  await expect(page.getByRole('heading', { name: 'Set up Castlumora' })).toBeVisible();
  await page.getByLabel('Server name', { exact: true }).fill('Castlumora Packaged Preview');
  await page.getByLabel('Administrator username', { exact: true }).fill('PreviewOwner');
  await page.getByLabel('Administrator password', { exact: true }).fill(password!);
  await page.getByLabel('Confirm administrator password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Create administrator and finish setup' }).click();
  await expect(page.getByRole('heading', { name: 'Welcome home' })).toBeVisible();
  await page.getByLabel('Username', { exact: true }).fill('PreviewOwner');
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Browse', exact: true })).toBeVisible();
  await expect(page.locator('header').getByRole('link', { name: 'Castlumora home' })).toBeVisible();
  await page.getByRole('link', { name: 'Administration', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Your castle. Your rules.' })).toBeVisible();
  const legacy = await request.get(new URL('legacy/', preview!).href);
  expect(await legacy.text()).toContain('does not silently install an older incompatible client');
  const session = await page.evaluate(() =>
    JSON.parse(sessionStorage.getItem('castlumora-session')!),
  );
  const auth = { Authorization: `MediaBrowser Token="${session.token}"` };
  const info = await request.get(new URL('../System/Info', preview!).href, { headers: auth });
  expect((await info.json()).ProgramDataPath).toContain('/tmp/castlumora-preview-fresh/data');
  const setupAgain = await request.post(new URL('../Startup/User', preview!).href, {
    data: { Name: 'Intruder', Password: 'not-used-password' },
  });
  expect([401, 403]).toContain(setupAgain.status());
  await page.locator('.profile-menu summary').click();
  await page.getByRole('button', { name: 'Sign out / switch profile', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Welcome home' })).toBeVisible();
  expect(errors).toEqual([]);
});

test('packaged preview: restart retains owner and creates/scans a library through native administration', async ({
  page,
  request,
}) => {
  test.skip(!preview || !password || process.env.CASTLUMORA_PREVIEW_RESTART !== '1');
  const target = new URL(preview!);
  if (target.hostname !== '127.0.0.1' || target.port !== '28096')
    throw new Error('Dedicated loopback preview required.');
  test.setTimeout(90000);
  page.setDefaultTimeout(20000);
  await page.goto(preview!);
  await expect(page.getByRole('heading', { name: 'Welcome home' })).toBeVisible();
  await page.getByLabel('Username', { exact: true }).fill('PreviewOwner');
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Browse', exact: true })).toBeVisible();
  const session = await page.evaluate(() =>
    JSON.parse(sessionStorage.getItem('castlumora-session')!),
  );
  const auth = { Authorization: `MediaBrowser Token="${session.token}"` };
  const info = await request.get(new URL('../System/Info', preview!).href, { headers: auth });
  expect((await info.json()).ProgramDataPath).toBe('/tmp/castlumora-preview-fresh/data');
  await page.getByRole('link', { name: 'Administration', exact: true }).click();
  await page
    .getByRole('navigation', { name: 'Administration sections' })
    .getByRole('link', { name: 'Libraries & metadata', exact: true })
    .click();
  await page.getByLabel('Name', { exact: true }).fill('Packaged Fixture Movies');
  await page.getByRole('combobox', { name: /^Media type/ }).selectOption('movies');
  await page
    .getByLabel('Server folder path', { exact: true })
    .fill('/tmp/castlumora-preview-fresh-media');
  await page.getByRole('button', { name: 'Create library', exact: true }).click();
  await expect(page.getByRole('button', { name: /Packaged Fixture Movies/ })).toBeVisible();
  await page.getByRole('link', { name: 'Browse', exact: true }).click();
  await expect
    .poll(
      async () => {
        const response = await request.get(new URL('../Items', preview!).href, {
          headers: auth,
          params: {
            UserId: session.user.Id,
            Recursive: true,
            IncludeItemTypes: 'Movie',
            SearchTerm: 'Castlumora Test',
          },
        });
        return (await response.json()).Items.length;
      },
      { timeout: 60000 },
    )
    .toBeGreaterThan(0);
  await page.reload();
  await page.getByRole('link', { name: 'Details for Castlumora Test', exact: true }).click();
  await page.getByRole('link', { name: 'Watch now', exact: true }).click();
  await expect
    .poll(() => page.locator('video').evaluate((video: HTMLVideoElement) => video.currentTime), {
      timeout: 20000,
    })
    .toBeGreaterThan(0);
  await page.getByRole('link', { name: 'Back', exact: true }).click();
  await page.getByRole('link', { name: 'Administration', exact: true }).click();
  await page
    .getByRole('navigation', { name: 'Administration sections' })
    .getByRole('link', { name: 'Imports & Sources', exact: true })
    .click();
  page.once('dialog', (dialog) => dialog.accept());
  await page.getByRole('button', { name: 'Install bundled runtime', exact: true }).click();
  await expect(page.getByText('Ready', { exact: true })).toBeVisible();
  const runtime = await request.get(new URL('../Castlumora/Imports/Runtime', preview!).href, {
    headers: auth,
  });
  expect(runtime.ok()).toBe(true);
  const tool = await runtime.json();
  expect(tool.ready).toBe(true);
  expect(tool.bundleAvailable).toBe(true);
  expect(tool.downloadJobsAvailable).toBe(true);
  await expect(page.getByRole('heading', { name: 'Import a video', exact: true })).toBeVisible();
  const queue = await request.get(new URL('../Castlumora/Imports/Queue', preview!).href, {
    headers: auth,
  });
  expect(queue.ok()).toBe(true);
  expect((await queue.json()).available).toBe(true);
});

test('packaged preview: authorized public import indexes, deduplicates and plays owned media', async ({
  page,
  request,
}) => {
  test.skip(!preview || !password || process.env.CASTLUMORA_PREVIEW_RESTART !== '1');
  const target = new URL(preview!);
  if (target.hostname !== '127.0.0.1' || target.port !== '28096')
    throw new Error('Dedicated isolated packaged preview required.');
  test.setTimeout(180000);
  page.setDefaultTimeout(20000);
  await page.goto(preview!);
  await page.getByLabel('Username', { exact: true }).fill('PreviewOwner');
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Browse', exact: true })).toBeVisible();
  const session = await page.evaluate(() =>
    JSON.parse(sessionStorage.getItem('castlumora-session')!),
  );
  const headers = { Authorization: `MediaBrowser Token="${session.token}"` };
  const endpoint = (path: string) => new URL('../' + path, preview!).href;
  const get = async (path: string) => {
    const response = await request.get(endpoint(path), { headers });
    expect(response.ok()).toBe(true);
    return response.json();
  };
  expect((await get('System/Info')).ProgramDataPath).toContain(
    '/tmp/castlumora-preview-fresh/data',
  );
  const name = 'Owned Imported Videos';
  const title = 'Owned Imported Preview Clip';
  const library = await request.post(
    endpoint(
      'Library/VirtualFolders?name=' +
        encodeURIComponent(name) +
        '&collectionType=homevideos&refreshLibrary=true&paths=' +
        encodeURIComponent('/tmp/castlumora-preview-authorized-import'),
    ),
    { headers, data: { LibraryOptions: { EnableRealtimeMonitor: true } } },
  );
  expect(library.ok()).toBe(true);
  await expect
    .poll(
      async () =>
        (await get('Castlumora/Imports/Queue')).destinations.map(
          (item: { name: string }) => item.name,
        ),
      { timeout: 30000 },
    )
    .toContain(name);
  await page.goto(new URL('#/admin/imports', preview!).href);
  await expect(page.getByRole('heading', { name: 'Import a video', exact: true })).toBeVisible({
    timeout: 20000,
  });
  await page
    .getByRole('combobox', { name: 'Destination library', exact: true })
    .selectOption({ label: name });
  const source =
    'https://raw.githubusercontent.com/dangerousnick/Castlumora/36cf2b44a01c0f88c6f5c9ec979161790f03d8a1/tests/Jellyfin.Api.Tests/Fixtures/import-owned.mp4';
  await page.getByLabel('Source URL', { exact: true }).fill(source);
  await page.getByLabel('Imported title', { exact: true }).fill(title);
  await page
    .getByLabel('Rights / permission record', { exact: true })
    .fill(
      'Repository owner-authorized generated synthetic clip, GPL; fixture provenance at commit36cf2b44a0.',
    );
  await page
    .getByRole('checkbox', {
      name: 'I have the rights and permission to download and retain this source.',
    })
    .check();
  const submitted = page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname.endsWith('/Castlumora/Imports/Jobs') &&
      response.request().method() === 'POST',
  );
  await page.getByRole('checkbox', { name: /Automatically retry temporary/ }).check();
  await page.getByRole('button', { name: 'Queue authorized video', exact: true }).click();
  const response = await submitted;
  expect(response.ok()).toBe(true);
  const job = await response.json();
  expect(job.automaticRetries).toBe(true);
  await expect
    .poll(
      async () => {
        const state = await get('Castlumora/Imports/Queue');
        expect(JSON.stringify(state)).not.toContain(source);
        const current = state.jobs.find((item: { id: string }) => item.id === job.id);
        if (current?.state === 'failed')
          throw new Error('Owned import failed: ' + current.error + '; job ' + job.id);
        return current?.state;
      },
      { timeout: 75000, intervals: [500, 1000] },
    )
    .toBe('completed');
  const completed = (await get('Castlumora/Imports/Queue')).jobs.find(
    (item: { id: string }) => item.id === job.id,
  );
  expect(completed.automaticRetries).toBe(true);
  expect(completed.nextAttemptAt).toBeNull();
  expect(completed.sourceHost).toBe('raw.githubusercontent.com');
  expect(completed.bytes).toBe(11734);
  expect(completed.sha256).toBe('8f56e298a9ceaacc3f34126546eeb3cd9d8368c85f3e2c719c63aaa98a55e554');
  const repeat = await request.post(endpoint('Castlumora/Imports/Jobs'), {
    headers,
    data: {
      url: source,
      destinationId: completed.destinationId,
      title,
      rights: 'Same authorized synthetic clip',
      rightsConfirmed: true,
    },
  });
  expect(repeat.ok()).toBe(true);
  expect((await repeat.json()).id).toBe(job.id);
  // Acceptance is atomic; downloads subsequently complete independently.
  const previousCount = (await get('Castlumora/Imports/Queue')).jobs.length;
  await page.getByRole('checkbox', { name: /Import multiple URLs/ }).check();
  await page
    .getByRole('textbox', { name: 'Source URLs (one per line)', exact: true })
    .fill(`${source}?bulk=one\nhttps://127.0.0.1/private.mp4`);
  await page.getByLabel('Title prefix', { exact: true }).fill('Owned batch');
  await page
    .getByLabel('Rights / permission record', { exact: true })
    .fill('Owner-authorized repository GPL synthetic clip');
  await page.getByRole('checkbox', { name: /I have the rights/ }).check();
  const invalid = page.waitForResponse(
    (r) =>
      new URL(r.url()).pathname.endsWith('/Castlumora/Imports/Jobs/Batch') &&
      r.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Queue authorized batch', exact: true }).click();
  expect((await invalid).status()).toBe(400);
  expect((await get('Castlumora/Imports/Queue')).jobs).toHaveLength(previousCount);
  await page
    .getByRole('textbox', { name: 'Source URLs (one per line)', exact: true })
    .fill(`${source}?bulk=one\n${source}?bulk=two`);
  const savedBatch = page.waitForResponse(
    (r) =>
      new URL(r.url()).pathname.endsWith('/Castlumora/Imports/Jobs/Batch') &&
      r.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Queue authorized batch', exact: true }).click();
  const batchResponse = await savedBatch;
  expect(batchResponse.ok()).toBe(true);
  const batchJobs = (await batchResponse.json()).jobs;
  expect(batchJobs).toHaveLength(2);
  expect(new Set(batchJobs.map((entry: { id: string }) => entry.id)).size).toBe(2);
  await expect
    .poll(
      async () => {
        const history = (await get('Castlumora/Imports/Queue')).jobs;
        return batchJobs.map(
          (entry: { id: string }) =>
            history.find((current: { id: string }) => current.id === entry.id)?.state,
        );
      },
      { timeout: 75000 },
    )
    .toEqual(['completed', 'completed']);
  const bulkHistory = (await get('Castlumora/Imports/Queue')).jobs;
  for (const entry of batchJobs) {
    const current = bulkHistory.find((candidate: { id: string }) => candidate.id === entry.id);
    expect(current.bytes).toBe(completed.bytes);
    expect(current.sha256).toBe(completed.sha256);
    expect(JSON.stringify(current)).not.toContain(source);
  }

  let item:
    | { Id: string; Type: string; Name: string; MediaStreams: { Type: string; Codec: string }[] }
    | undefined;
  await expect
    .poll(
      async () => {
        const result = await get(
          'Items?Recursive=true&IncludeItemTypes=Video&SearchTerm=' +
            encodeURIComponent(title) +
            '&Fields=MediaStreams',
        );
        item = result.Items.find((entry: { Name: string }) => entry.Name === title);
        return item?.Id;
      },
      { timeout: 60000 },
    )
    .toBeTruthy();
  expect(item!.Type).toBe('Video');
  expect(item!.MediaStreams).toEqual(
    expect.arrayContaining([
      expect.objectContaining({ Type: 'Video', Codec: 'h264' }),
      expect.objectContaining({ Type: 'Audio', Codec: 'aac' }),
    ]),
  );
  await page.goto(new URL('#/item/' + item!.Id, preview!).href);
  await page.getByRole('link', { name: 'Watch now', exact: true }).click();
  await expect
    .poll(() => page.locator('video').evaluate((video: HTMLVideoElement) => video.currentTime), {
      timeout: 20000,
    })
    .toBeGreaterThan(0);
  await page.getByRole('link', { name: 'Back', exact: true }).click();
});
