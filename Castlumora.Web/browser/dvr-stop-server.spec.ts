// Opt-in active-stop verification, restricted to our marked disposable server.
import { test, expect } from '@playwright/test';
import { existsSync, readFileSync, statSync, writeFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';
const root = '/tmp/castlumora-web-real-server';
test('active DVR stop retains playable partial media and rejects viewer cancellation', async ({
  page,
  request,
}) => {
  const server = process.env.CASTLUMORA_TEST_SERVER;
  const username = process.env.CASTLUMORA_TEST_USER;
  const password = process.env.CASTLUMORA_TEST_PASSWORD;
  test.skip(
    process.env.CASTLUMORA_TEST_DVR !== '1' || !server || !username || password === undefined,
  );
  test.setTimeout(180000);
  if (new URL(server!).hostname !== '127.0.0.1' || !existsSync(root + '/.castlumora-disposable'))
    throw new Error('Marked loopback fixture required.');
  const state = JSON.parse(readFileSync(root + '/dvr-fixture-state.json', 'utf8'));
  const program = state.programs.find((p: { kind: string }) => p.kind === 'Stop');
  expect(program).toBeTruthy();
  const header =
    'MediaBrowser Client="DVR stop", Device="Disposable", DeviceId="dvr-stop", Version="1"';
  const login = await request.post(server! + '/Users/AuthenticateByName', {
    headers: { Authorization: header },
    data: { Username: username, Pw: password },
  });
  expect(login.ok()).toBe(true);
  const auth = await login.json();
  const headers = { Authorization: header + ', Token="' + auth.AccessToken + '"' };
  expect(
    (await (await request.get(server! + '/System/Info', { headers })).json()).ProgramDataPath,
  ).toBe(root + '/data');
  await page.goto('/');
  await page.getByLabel('Server address').fill(server!);
  await page.getByRole('button', { name: 'Connect', exact: true }).click();
  await page.getByLabel('Username', { exact: true }).fill(username!);
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Browse', exact: true })).toBeVisible();
  await page.goto('/#/live');
  await page.getByText(program.name, { exact: true }).click();
  await page
    .locator('details')
    .filter({ has: page.getByText(program.name, { exact: true }) })
    .getByRole('button', { name: 'Record options', exact: true })
    .click();
  const saved = page.waitForResponse(
    (r) => new URL(r.url()).pathname.endsWith('/LiveTv/Timers') && r.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Save single recording', exact: true }).click();
  expect((await saved).ok()).toBe(true);
  let timer: { Id: string; Status: string } | undefined;
  await expect
    .poll(
      async () => {
        timer = (
          await (await request.get(server! + '/LiveTv/Timers', { headers })).json()
        ).Items.find((t: { Name: string }) => t.Name === program.name);
        return timer?.Status;
      },
      { timeout: 90000, intervals: [1000, 2000] },
    )
    .toBe('InProgress');
  // The recorder has genuinely written data before we request a stop.
  const timerFile = root + '/data/data/livetv/timers.json';
  const recorded = () =>
    JSON.parse(readFileSync(timerFile, 'utf8')).find(
      (t: { Name: string }) => t.Name === program.name,
    );
  await expect
    .poll(
      () => {
        const folder = root + '/dvr-lifecycle/' + program.name;
        return (
          existsSync(folder) &&
          readdirSync(folder).some(
            (name) => name.endsWith('.ts') && statSync(folder + '/' + name).size > 18800,
          )
        );
      },
      { timeout: 45000 },
    )
    .toBe(true);
  const userResponse = await request.post(server! + '/Users/New', {
    headers,
    data: { Name: 'SyntheticDvrStopViewer' + Date.now() },
  });
  expect(userResponse.ok()).toBe(true);
  const viewer = await userResponse.json();
  state.viewerId = viewer.Id;
  writeFileSync(root + '/dvr-fixture-state.json', JSON.stringify(state));
  expect(
    (
      await request.post(server! + '/Users/' + viewer.Id + '/Policy', {
        headers,
        data: {
          ...viewer.Policy,
          IsAdministrator: false,
          EnableLiveTvAccess: true,
          EnableLiveTvManagement: false,
        },
      })
    ).ok(),
  ).toBe(true);
  const vl = await request.post(server! + '/Users/AuthenticateByName', {
    headers: { Authorization: header },
    data: { Username: viewer.Name, Pw: '' },
  });
  expect(vl.ok()).toBe(true);
  const vh = { Authorization: header + ', Token="' + (await vl.json()).AccessToken + '"' };
  expect([401, 403]).toContain(
    (await request.delete(server! + '/LiveTv/Timers/' + timer!.Id, { headers: vh })).status(),
  );
  await page.getByRole('button', { name: 'Scheduled', exact: true }).click();
  await page.getByRole('button', { name: 'Refresh guide & recordings', exact: true }).click();
  const card = page
    .locator('article')
    .filter({ has: page.getByRole('heading', { name: program.name, exact: true }) });
  let confirmation = '';
  page.once('dialog', async (d) => {
    confirmation = d.message();
    await d.accept();
  });
  await card.getByRole('button', { name: 'Stop recording', exact: true }).click();
  expect(confirmation).toContain('recorded media is retained');
  await expect.poll(() => recorded()?.Status, { timeout: 20000 }).toBe('Completed');
  const file = resolve(recorded().RecordingPath);
  expect(file.startsWith(root + '/dvr-lifecycle/')).toBe(true);
  expect(statSync(file).size).toBeGreaterThan(0);
  expect(Date.now()).toBeLessThan(Date.parse(program.end));
  state.recordingPath = file;
  writeFileSync(root + '/dvr-fixture-state.json', JSON.stringify(state));
  let item: { Id: string; Name: string } | undefined;
  await expect
    .poll(
      async () => {
        item = (
          await (
            await request.get(server! + '/LiveTv/Recordings', {
              headers,
              params: { UserId: auth.User.Id, Fields: 'Path' },
            })
          ).json()
        ).Items.find((i: { Path: string }) => i.Path === file);
        return Boolean(item);
      },
      { timeout: 60000 },
    )
    .toBe(true);
  await page.getByRole('button', { name: 'Recordings', exact: true }).click();
  await page.getByRole('link', { name: 'Details for ' + item!.Name, exact: true }).click();
  await page.getByRole('link', { name: /^(Watch now|Resume)$/ }).click();
  await expect
    .poll(() => page.locator('video').evaluate((v: HTMLVideoElement) => v.currentTime), {
      timeout: 30000,
    })
    .toBeGreaterThan(1);
  expect(existsSync(file)).toBe(true);
  expect(existsSync(state.originalMovie)).toBe(true);
});
