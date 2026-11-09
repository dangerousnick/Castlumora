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
  await expect(page.getByRole('heading', { name: /Administration/ })).toBeVisible();
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
