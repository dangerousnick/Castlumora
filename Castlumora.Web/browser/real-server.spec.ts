// Opt-in verification against a disposable LOOPBACK server. Never point at production.
import { test, expect } from '@playwright/test';
const server = process.env.CASTLUMORA_TEST_SERVER;
const username = process.env.CASTLUMORA_TEST_USER;
const password = process.env.CASTLUMORA_TEST_PASSWORD;
test('real disposable server: authenticate, browse, details, stream and report progress', async ({
  page,
}) => {
  test.skip(
    !server || !username || password === undefined,
    'Set the documented disposable-server test environment.',
  );
  if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server!).hostname))
    throw new Error('Real-server tests require a disposable loopback server.');
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('/');
  await page.getByLabel('Server address').fill(server!);
  await page.getByRole('button', { name: 'Connect', exact: true }).click();
  await page.getByLabel('Username').fill(username!);
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Browse', exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Browse', exact: true }).click();
  await page
    .getByRole('link', { name: /Details for / })
    .first()
    .click();
  await expect(page.locator('.detail-copy h1')).toBeVisible();
  const started = page.waitForResponse(
    (r) =>
      r.url().includes('/Sessions/Playing') &&
      !r.url().includes('/Progress') &&
      r.request().method() === 'POST',
  );
  await page.getByRole('link', { name: /^(Watch now|Resume)$/ }).click();
  await expect
    .poll(() => page.locator('video').evaluate((v: HTMLVideoElement) => v.currentTime), {
      timeout: 20000,
    })
    .toBeGreaterThan(0);
  expect((await started).ok()).toBe(true);
  const progress = page.waitForResponse(
    (r) => r.url().includes('/Sessions/Playing/Progress') && r.request().method() === 'POST',
  );
  await page.locator('video').evaluate((v: HTMLVideoElement) => v.pause());
  expect((await progress).ok()).toBe(true);
  const stopped = page.waitForResponse(
    (r) => r.url().includes('/Sessions/Playing/Stopped') && r.request().method() === 'POST',
  );
  await page.getByRole('link', { name: 'Back', exact: true }).click();
  expect((await stopped).ok()).toBe(true);
  expect(errors).toEqual([]);
});

test('real disposable server: compatible HLS playback and cleanup', async ({ page }) => {
  test.skip(
    !server || !username || password === undefined,
    'Requires disposable-server environment.',
  );
  if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server!).hostname))
    throw new Error('Real-server tests require a disposable loopback server.');
  const responses: { path: string; status: number }[] = [];
  page.on('response', (r) => {
    const path = new URL(r.url()).pathname;
    if (
      path.includes('.m3u8') ||
      path.includes('ActiveEncodings') ||
      path.includes('/Sessions/Playing')
    )
      responses.push({ path, status: r.status() });
  });
  await page.goto('/');
  await page.getByLabel('Server address').fill(server!);
  await page.getByRole('button', { name: 'Connect', exact: true }).click();
  await page.getByLabel('Username').fill(username!);
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('link', { name: 'Browse', exact: true }).click();
  await page
    .getByRole('link', { name: /Details for / })
    .first()
    .click();
  await page.getByRole('link', { name: /^(Watch now|Resume)$/ }).click();
  await expect
    .poll(() => page.locator('video').evaluate((v: HTMLVideoElement) => v.currentTime))
    .toBeGreaterThan(0);
  await page.getByLabel('Playback position', { exact: true }).press('Home');
  for (let n = 0; n < 9; n++)
    await page.getByLabel('Playback position', { exact: true }).press('ArrowRight');
  await expect
    .poll(() => page.locator('video').evaluate((v: HTMLVideoElement) => v.currentTime))
    .toBeGreaterThan(7);
  await page.getByRole('button', { name: 'Try compatible stream' }).click();
  await expect
    .poll(() => responses.some((r) => r.path.includes('.m3u8') && r.status === 200), {
      timeout: 20000,
    })
    .toBe(true);
  await expect
    .poll(
      () => page.locator('video').evaluate((v: HTMLVideoElement) => !v.paused && v.currentTime > 0),
      { timeout: 20000 },
    )
    .toBe(true);
  const qualityResponse = page.waitForResponse(
    (r) =>
      r.url().includes('/PlaybackInfo') &&
      r.request().method() === 'POST' &&
      r.request().postDataJSON()?.MaxStreamingBitrate === 2000000,
  );
  await page.getByRole('combobox', { name: 'Quality', exact: true }).selectOption('2000000');
  expect((await qualityResponse).ok()).toBe(true);
  await expect
    .poll(
      () => page.locator('video').evaluate((v: HTMLVideoElement) => !v.paused && v.currentTime > 0),
      { timeout: 20000 },
    )
    .toBe(true);
  const audio = page.getByRole('combobox', { name: 'Audio', exact: true });
  await expect(audio.locator('option')).toHaveCount(2);
  const audioResponse = page.waitForResponse(
    (r) => r.url().includes('/PlaybackInfo') && r.request().method() === 'POST',
  );
  await audio.selectOption({ index: 1 });
  expect((await audioResponse).ok()).toBe(true);
  await expect
    .poll(
      () => page.locator('video').evaluate((v: HTMLVideoElement) => !v.paused && v.currentTime > 0),
      { timeout: 20000 },
    )
    .toBe(true);
  const subtitle = page.getByRole('combobox', { name: 'Subtitles', exact: true });
  await expect(subtitle.locator('option')).toHaveCount(2);
  const subtitleResponse = page.waitForResponse(
    (r) => r.url().includes('/PlaybackInfo') && r.request().method() === 'POST',
  );
  await subtitle.selectOption({ index: 1 });
  expect((await subtitleResponse).ok()).toBe(true);
  await expect
    .poll(
      () => page.locator('video').evaluate((v: HTMLVideoElement) => !v.paused && v.currentTime > 0),
      { timeout: 20000 },
    )
    .toBe(true);
  await expect
    .poll(() => page.locator('track').evaluate((t: HTMLTrackElement) => t.readyState), {
      timeout: 10000,
    })
    .toBe(2);
  await page.getByRole('link', { name: 'Back', exact: true }).click();
  await expect
    .poll(() => responses.some((r) => r.path.includes('ActiveEncodings') && r.status === 204))
    .toBe(true);
});
