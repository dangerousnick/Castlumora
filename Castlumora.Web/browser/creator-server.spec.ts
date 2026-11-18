// Opt-in verification of locally retained synthetic creator media; never contacts YouTube.
import { test, expect } from '@playwright/test';
import { existsSync, readFileSync } from 'node:fs';
const root = '/tmp/castlumora-web-real-server';
test('existing creator video: browse as video, escaped metadata and real playback', async ({
  page,
  request,
}) => {
  const server = process.env.CASTLUMORA_TEST_SERVER;
  const username = process.env.CASTLUMORA_TEST_USER;
  const password = process.env.CASTLUMORA_TEST_PASSWORD;
  test.skip(
    process.env.CASTLUMORA_TEST_CREATOR !== '1' || !server || !username || password === undefined,
  );
  if (new URL(server!).hostname !== '127.0.0.1' || !existsSync(root + '/.castlumora-disposable'))
    throw new Error('Owned loopback fixture required.');
  const result = JSON.parse(readFileSync(root + '/creator-sidecar-result.json', 'utf8'));
  const header =
    'MediaBrowser Client="Creator browser", Device="Disposable", DeviceId="creator-browser", Version="1"';
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
  const itemResponse = await request.get(
    server! + '/Users/' + auth.User.Id + '/Items/' + result.itemId,
    { headers },
  );
  expect(itemResponse.ok()).toBe(true);
  const item = await itemResponse.json();
  expect(item.Type).toBe('Video');
  await page.goto('/');
  await page.getByLabel('Server address').fill(server!);
  await page.getByRole('button', { name: 'Connect', exact: true }).click();
  await page.getByLabel('Username', { exact: true }).fill(username!);
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('link', { name: 'Browse', exact: true }).click();
  await page.getByLabel('Media type', { exact: true }).selectOption('Video');
  await page.getByRole('link', { name: 'Details for ' + item.Name, exact: true }).click();
  await expect(page.locator('.detail-copy h1')).toHaveText(item.Name);
  await expect(
    page.getByText('Owned fixture <script>must not execute</script>', { exact: true }),
  ).toBeVisible();
  await expect(page.locator('.detail-copy script')).toHaveCount(0);
  await page.getByRole('link', { name: /^(Watch now|Resume)$/ }).click();
  await expect
    .poll(() => page.locator('video').evaluate((video: HTMLVideoElement) => video.currentTime), {
      timeout: 30000,
    })
    .toBeGreaterThan(1);
  expect(existsSync(result.path)).toBe(true);
});
