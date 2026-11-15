// Opt-in only: actual extracted archive running on the dedicated fresh-install loopback port.
import { test, expect } from '@playwright/test';
import type { MediaSource } from '../src/api/types';
import { verifyPreferredFixtureTracks } from './media-preference-proof';
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
  await page.getByRole('button', { name: 'Sign out', exact: true }).click();
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
  await test.step('YouTube form rejects unsafe source without creating a persistent job', async () => {
    const youtube = page.getByRole('region', { name: 'Authorized YouTube import', exact: true });
    await expect(
      youtube.getByRole('heading', { name: 'Import a YouTube video', exact: true }),
    ).toBeVisible();
    await youtube
      .getByRole('combobox', { name: 'YouTube destination library', exact: true })
      .selectOption({ label: name });
    await youtube
      .getByLabel('YouTube video URL', { exact: true })
      .fill('https://127.0.0.1/private');
    const previous = (await get('Castlumora/Imports/Queue')).jobs.length;
    const rejected = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname.endsWith('/Castlumora/Imports/YouTube/Preview') &&
        response.request().method() === 'POST',
    );
    await youtube.getByRole('button', { name: 'Preview YouTube video', exact: true }).click();
    const rejectedResponse = await rejected;
    expect(rejectedResponse.status()).toBe(400);
    // The client intentionally discards error bodies; inspect the same policy response
    // through the authenticated API context rather than a paused browser body stream.
    const policyResponse = await request.post(rejectedResponse.url(), {
      headers,
      data: rejectedResponse.request().postDataJSON(),
    });
    expect(policyResponse.status()).toBe(400);
    expect(await policyResponse.text()).toContain('publicly routable');
    await expect(youtube.getByRole('status')).toContainText('Preview rejected.');
    expect((await get('Castlumora/Imports/Queue')).jobs).toHaveLength(previous);
    await expect(
      youtube.getByRole('button', { name: 'Queue confirmed YouTube video', exact: true }),
    ).toHaveCount(0);
  });
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

test('packaged preview: restart managed profiles isolate preferences and revoke prior sessions', async ({
  page,
  request,
}) => {
  test.skip(!preview || !password || process.env.CASTLUMORA_PREVIEW_RESTART !== '1');
  const target = new URL(preview!);
  if (target.hostname !== '127.0.0.1' || target.port !== '28096')
    throw new Error('Dedicated isolated packaged preview required.');
  test.setTimeout(90000);
  page.setDefaultTimeout(20000);
  await page.goto(preview!);
  await page.getByLabel('Username', { exact: true }).fill('PreviewOwner');
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Administration', exact: true })).toBeVisible();
  const session = () =>
    page.evaluate(() => JSON.parse(sessionStorage.getItem('castlumora-session')!));
  const headers = (token: string) => ({ Authorization: `MediaBrowser Token="${token}"` });
  const endpoint = (path: string) => new URL('../' + path, preview!).href;
  const owner = await session();
  await page.getByRole('link', { name: 'Administration', exact: true }).click();
  await page
    .getByRole('navigation', { name: 'Administration sections' })
    .getByRole('link', { name: 'Users & devices', exact: true })
    .click();
  await page
    .getByRole('combobox', { name: 'Owning account', exact: true })
    .selectOption(owner.user.Id);
  await page.getByLabel('Profile name', { exact: true }).fill('Packaged Kids');
  await page.getByRole('checkbox', { name: 'Packaged Fixture Movies', exact: true }).check();
  await page.getByRole('button', { name: 'Create viewing profile', exact: true }).click();
  await expect(
    page.getByText('Profile created. Review its policy below or switch from the profile menu.', {
      exact: true,
    }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Rename Packaged Kids', exact: true }).click();
  await page.getByLabel('Profile display name', { exact: true }).fill('Packaged Family');
  await page.getByRole('button', { name: 'Save profile name', exact: true }).click();
  await expect(
    page.getByRole('button', { name: 'Rename Packaged Family', exact: true }),
  ).toBeVisible();
  const switchTo = async (label: string) => {
    await page.locator('.profile-menu > summary').click();
    await page.getByRole('link', { name: 'Viewing profiles', exact: true }).click();
    await page
      .getByRole('combobox', { name: 'Viewing profile', exact: true })
      .selectOption({ label });
    await page.getByLabel('Account password', { exact: true }).fill(password!);
    await page.getByRole('button', { name: 'Switch viewing profile', exact: true }).click();
  };
  await switchTo('Packaged Family (kids)');
  await expect(page.locator('.profile-menu > summary')).toContainText('Packaged Family');
  await expect(page.getByRole('link', { name: 'Administration', exact: true })).toHaveCount(0);
  // A cached friendly label must not override a later administrator correction.
  await page.evaluate(() => {
    const value = JSON.parse(sessionStorage.getItem('castlumora-session')!);
    value.profileName = 'Outdated profile label';
    sessionStorage.setItem('castlumora-session', JSON.stringify(value));
  });
  await page.reload();
  await expect(page.locator('.profile-menu > summary')).toContainText('Packaged Family');
  const child = await session();
  expect(child.user.Id).not.toBe(owner.user.Id);
  expect(child.token).not.toBe(owner.token);
  expect(
    (await request.get(endpoint('Users/Me'), { headers: headers(owner.token) })).status(),
  ).toBe(401);
  expect(
    (
      await request.post(endpoint('Users/New'), {
        headers: headers(child.token),
        data: { Name: 'Forbidden profile administrator' },
      })
    ).status(),
  ).toBe(403);
  await page.locator('.profile-menu > summary').click();
  await page.getByRole('link', { name: 'Viewing profiles', exact: true }).click();
  await page.getByLabel('Preferred audio language', { exact: true }).fill('spa');
  await page.getByLabel('Preferred subtitle language', { exact: true }).fill('eng');
  await page
    .getByRole('checkbox', { name: 'Use source default audio track', exact: true })
    .uncheck();
  await page
    .getByRole('combobox', { name: 'Subtitle behavior', exact: true })
    .selectOption('Always');
  await page.getByRole('button', { name: 'Save media preferences', exact: true }).click();
  await expect(
    page.getByText('Media preferences saved for this profile.', { exact: true }),
  ).toBeVisible();
  await page.reload();
  await expect(page.getByLabel('Preferred audio language', { exact: true })).toHaveValue('spa');
  let fixture: { Id: string; OfficialRating?: string; MediaSources: MediaSource[] } | undefined;
  await expect
    .poll(
      async () => {
        const response = await request.get(endpoint('Items'), {
          headers: headers(child.token),
          params: {
            UserId: child.user.Id,
            Recursive: true,
            IncludeItemTypes: 'Movie',
            Fields: 'MediaSources',
            SearchTerm: 'Castlumora Test',
          },
        });
        expect(response.ok()).toBe(true);
        fixture = (await response.json()).Items[0];
        return (
          fixture?.OfficialRating === 'G' &&
          (fixture.MediaSources[0]?.MediaStreams.length ?? 0) >= 4
        );
      },
      { timeout: 20000 },
    )
    .toBe(true);
  const audioIndex = fixture!.MediaSources[0].MediaStreams.find(
    (stream) => stream.Type === 'Audio' && stream.Language === 'spa',
  )!.Index;
  const subtitleIndex = fixture!.MediaSources[0].MediaStreams.find(
    (stream) => stream.Type === 'Subtitle' && stream.Language === 'eng',
  )!.Index;
  const playbackRequests: Record<string, unknown>[] = [];
  const capturePlayback = (response: import('@playwright/test').Request) => {
    if (new URL(response.url()).pathname.endsWith('/PlaybackInfo') && response.method() === 'POST')
      playbackRequests.push(response.postDataJSON());
  };
  page.on('request', capturePlayback);
  await page.goto(preview! + '#/item/' + fixture!.Id);
  await page.getByRole('link', { name: 'Watch now', exact: true }).click();
  await verifyPreferredFixtureTracks(page);
  expect(
    playbackRequests.some(
      (body) =>
        body.EnableDirectPlay === false &&
        body.AudioStreamIndex === audioIndex &&
        body.SubtitleStreamIndex === subtitleIndex,
    ),
  ).toBe(true);
  page.off('request', capturePlayback);
  await page.getByRole('link', { name: 'Back', exact: true }).click();
  const marker = '11111111222233334444555555555555';
  expect(
    (
      await request.put(
        endpoint('Castlumora/Presentation/Preferences/hiddenResume/' + marker + '?enabled=true'),
        { headers: headers(child.token) },
      )
    ).ok(),
  ).toBe(true);
  await switchTo('PreviewOwner (account)');
  await expect(page.getByRole('link', { name: 'Administration', exact: true })).toBeVisible();
  const returned = await session();
  expect(returned.user.Id).toBe(owner.user.Id);
  const preferences = await request.get(endpoint('Castlumora/Presentation/Preferences'), {
    headers: headers(returned.token),
  });
  expect(preferences.ok()).toBe(true);
  expect((await preferences.json()).hiddenResume).not.toContain(marker);
  expect(
    (await request.get(endpoint('Users/Me'), { headers: headers(child.token) })).status(),
  ).toBe(401);
  await page.getByRole('link', { name: 'Administration', exact: true }).click();
  await page
    .getByRole('navigation', { name: 'Administration sections' })
    .getByRole('link', { name: 'Users & devices', exact: true })
    .click();
  const childItemUrl = endpoint('Items/' + fixture!.Id + '?userId=' + child.user.Id);
  const beforeRename = await request.get(childItemUrl, { headers: headers(returned.token) });
  expect(beforeRename.ok()).toBe(true);
  const watched = (await beforeRename.json()).UserData;
  expect(watched.LastPlayedDate).toBeTruthy();
  await page.getByRole('button', { name: 'Rename Packaged Family', exact: true }).click();
  await page.getByLabel('Profile display name', { exact: true }).fill('Packaged Family Updated');
  await page.getByRole('button', { name: 'Save profile name', exact: true }).click();
  await expect(
    page.getByRole('button', { name: 'Rename Packaged Family Updated', exact: true }),
  ).toBeVisible();
  const afterRename = await request.get(childItemUrl, { headers: headers(returned.token) });
  expect(afterRename.ok()).toBe(true);
  expect((await afterRename.json()).UserData).toEqual(watched);
  const retainedUser = await request.get(endpoint('Users/' + child.user.Id), {
    headers: headers(returned.token),
  });
  expect(retainedUser.ok()).toBe(true);
  const retainedConfiguration = (await retainedUser.json()).Configuration;
  expect(retainedConfiguration.AudioLanguagePreference).toBe('spa');
  expect(retainedConfiguration.SubtitleLanguagePreference).toBe('eng');
  page.once('dialog', (dialog) => dialog.accept());
  await page.getByRole('button', { name: 'Delete Packaged Family Updated', exact: true }).click();
  await expect(page.getByText('No managed profiles.', { exact: true })).toBeVisible();
  expect(
    (
      await request.get(endpoint('Users/' + child.user.Id), { headers: headers(returned.token) })
    ).status(),
  ).toBe(404);
  await page.getByRole('link', { name: 'Browse', exact: true }).click();
  await page.getByRole('link', { name: 'Details for Castlumora Test', exact: true }).click();
  await page.getByRole('link', { name: 'Watch now', exact: true }).click();
  await expect
    .poll(() => page.locator('video').evaluate((video: HTMLVideoElement) => video.readyState), {
      timeout: 20000,
    })
    .toBeGreaterThanOrEqual(2);
  const position = await page
    .locator('video')
    .evaluate((video: HTMLVideoElement) => video.currentTime);
  await expect
    .poll(() => page.locator('video').evaluate((video: HTMLVideoElement) => video.currentTime), {
      timeout: 20000,
    })
    .toBeGreaterThan(position + 0.2);
});
