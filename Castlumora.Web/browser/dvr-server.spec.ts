// Opt-in owned synthetic DVR lifecycle only, with marker AND actual server-data-path guard.
import { test, expect } from '@playwright/test';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve, relative } from 'node:path';
const root = '/tmp/castlumora-web-real-server';
const server = process.env.CASTLUMORA_TEST_SERVER;
const username = process.env.CASTLUMORA_TEST_USER;
const password = process.env.CASTLUMORA_TEST_PASSWORD;
test('DVR lifecycle: schedule/cancel, record/complete/play, retain on removal and permission-checked file deletion', async ({
  page,
  request,
}) => {
  test.skip(
    process.env.CASTLUMORA_TEST_DVR !== '1' || !server || !username || password === undefined,
  );
  test.setTimeout(360000);
  page.setDefaultTimeout(20000);
  if (new URL(server!).hostname !== '127.0.0.1' || !existsSync(root + '/.castlumora-disposable'))
    throw new Error('Owned loopback DVR fixture required.');
  const state = JSON.parse(readFileSync(root + '/dvr-fixture-state.json', 'utf8'));
  const header =
    'MediaBrowser Client="DVR lifecycle", Device="Disposable", DeviceId="dvr-browser-admin", Version="1"';
  const authResponse = await request.post(server! + '/Users/AuthenticateByName', {
    headers: { Authorization: header },
    data: { Username: username, Pw: password },
  });
  expect(authResponse.ok()).toBe(true);
  const auth = await authResponse.json();
  const headers = { Authorization: header + ', Token="' + auth.AccessToken + '"' };
  const info = await request.get(server! + '/System/Info', { headers });
  expect((await info.json()).ProgramDataPath).toBe(root + '/data');
  await page.goto('/');
  await page.getByLabel('Server address').fill(server!);
  await page.getByRole('button', { name: 'Connect', exact: true }).click();
  await page.getByLabel('Username', { exact: true }).fill(username!);
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Browse', exact: true })).toBeVisible();
  await page.goto('/#/live');
  const complete = state.programs.find((p: { kind: string }) => p.kind === 'Complete');
  const cancelled = state.programs.find((p: { kind: string }) => p.kind === 'Cancel');
  async function schedule(name: string) {
    const title = page.getByText(name, { exact: true });
    await title.click();
    const detail = page.locator('details').filter({ has: page.getByText(name, { exact: true }) });
    await detail.getByRole('button', { name: 'Record options', exact: true }).click();
    const saved = page.waitForResponse(
      (r) =>
        new URL(r.url()).pathname.endsWith('/LiveTv/Timers') && r.request().method() === 'POST',
    );
    await page.getByRole('button', { name: 'Save single recording', exact: true }).click();
    expect((await saved).ok()).toBe(true);
  }
  await schedule(complete.name);
  await schedule(cancelled.name);
  await page.getByRole('button', { name: 'Scheduled', exact: true }).click();
  const card = page
    .locator('article')
    .filter({ has: page.getByRole('heading', { name: cancelled.name, exact: true }) });
  page.once('dialog', (dialog) => dialog.accept());
  await card.getByRole('button', { name: 'Cancel recording', exact: true }).click();
  await expect
    .poll(async () => {
      const r = await request.get(server! + '/LiveTv/Timers', { headers });
      return (
        (await r.json()).Items.find((t: { Name: string }) => t.Name === cancelled.name)?.Status ??
        'Cancelled'
      );
    })
    .toBe('Cancelled');
  await expect
    .poll(
      () => {
        const timers = JSON.parse(readFileSync(root + '/data/data/livetv/timers.json', 'utf8'));
        return timers.find((timer: { Name: string }) => timer.Name === complete.name)?.Status;
      },
      { timeout: 150000, intervals: [1000, 2000, 3000] },
    )
    .toBe('Completed');
  const recordingsUrl = server! + '/LiveTv/Recordings';
  let recording: { Id: string; Name: string; Path: string; CanDelete: boolean } | undefined;
  await expect
    .poll(
      async () => {
        const r = await request.get(recordingsUrl, {
          headers,
          params: { UserId: auth.User.Id, Fields: 'Path,CanDelete' },
        });
        recording = (await r.json()).Items.find((i: { Name: string }) =>
          i.Name.startsWith(complete.name),
        );
        return Boolean(recording);
      },
      { timeout: 90000 },
    )
    .toBe(true);
  const file = resolve(recording!.Path);
  expect(relative(root + '/dvr-lifecycle', file).startsWith('..')).toBe(false);
  expect(file.startsWith(root + '/dvr-lifecycle/')).toBe(true);
  expect(existsSync(file)).toBe(true);
  expect(recording!.CanDelete).toBe(true);
  state.recordingId = recording!.Id;
  state.recordingPath = file;
  writeFileSync(root + '/dvr-fixture-state.json', JSON.stringify(state));
  const newUser = await request.post(server! + '/Users/New', {
    headers,
    data: { Name: 'SyntheticDvrViewer' + Date.now() },
  });
  expect(newUser.ok()).toBe(true);
  const viewer = await newUser.json();
  state.viewerId = viewer.Id;
  writeFileSync(root + '/dvr-fixture-state.json', JSON.stringify(state));
  const policy = {
    ...viewer.Policy,
    IsAdministrator: false,
    EnableLiveTvAccess: true,
    EnableLiveTvManagement: false,
    EnableContentDeletion: false,
  };
  expect(
    (
      await request.post(server! + '/Users/' + viewer.Id + '/Policy', { headers, data: policy })
    ).ok(),
  ).toBe(true);
  const viewerAuth = await request.post(server! + '/Users/AuthenticateByName', {
    headers: { Authorization: header },
    data: { Username: viewer.Name, Pw: '' },
  });
  expect(viewerAuth.ok()).toBe(true);
  const vh = { Authorization: header + ', Token="' + (await viewerAuth.json()).AccessToken + '"' };
  expect([401, 403]).toContain(
    (
      await request.delete(server! + '/LiveTv/Recordings/' + recording!.Id, { headers: vh })
    ).status(),
  );
  expect([401, 403]).toContain(
    (await request.delete(server! + '/Items/' + recording!.Id, { headers: vh })).status(),
  );
  expect(existsSync(file)).toBe(true);
  await page.getByRole('button', { name: 'Recordings', exact: true }).click();
  const link = page.getByRole('link', { name: 'Details for ' + recording!.Name, exact: true });
  await link.click();
  await page.getByRole('link', { name: /^(Watch now|Resume)$/ }).click();
  await expect
    .poll(() => page.locator('video').evaluate((v: HTMLVideoElement) => v.currentTime), {
      timeout: 30000,
    })
    .toBeGreaterThan(1);
  await page.getByRole('link', { name: 'Back', exact: true }).click();
  await page.goto('/#/live');
  await page.getByRole('button', { name: 'Recordings', exact: true }).click();
  let item = page.locator('.poster-grid > div').filter({
    has: page.getByRole('link', { name: 'Details for ' + recording!.Name, exact: true }),
  });
  page.once('dialog', (dialog) => dialog.accept());
  await item.getByRole('button', { name: 'Remove from library', exact: true }).click();
  await expect(link).toHaveCount(0);
  expect(existsSync(file)).toBe(true);
  expect((await request.post(server! + '/Library/Refresh', { headers })).ok()).toBe(true);
  await expect
    .poll(
      async () => {
        const r = await request.get(recordingsUrl, {
          headers,
          params: { UserId: auth.User.Id, Fields: 'Path,CanDelete' },
        });
        recording = (await r.json()).Items.find((i: { Path: string }) => i.Path === file);
        return Boolean(recording);
      },
      { timeout: 90000 },
    )
    .toBe(true);
  await page.getByRole('button', { name: 'Refresh guide & recordings', exact: true }).click();
  item = page.locator('.poster-grid > div').filter({
    has: page.getByRole('link', { name: 'Details for ' + recording!.Name, exact: true }),
  });
  page.once('dialog', (dialog) => dialog.accept());
  await item.getByRole('button', { name: 'Delete recording file', exact: true }).click();
  await expect.poll(() => existsSync(file)).toBe(false);
  expect(existsSync(state.originalMovie)).toBe(true);
});
