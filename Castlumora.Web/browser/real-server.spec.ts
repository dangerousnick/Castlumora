// Opt-in verification against a disposable LOOPBACK server. Never point at production.
import { test, expect } from '@playwright/test';
import { existsSync } from 'node:fs';
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
  await page.getByRole('link', { name: 'Details for Castlumora Test', exact: true }).click();
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
  await page.getByRole('link', { name: 'Details for Castlumora Test', exact: true }).click();
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

test('real disposable server: administrator settings persist and advanced sections load', async ({
  page,
}) => {
  test.skip(
    !server || !username || password === undefined,
    'Requires disposable-server environment.',
  );
  if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server!).hostname))
    throw new Error('Disposable loopback server required.');
  await page.goto('/');
  await page.getByLabel('Server address').fill(server!);
  await page.getByRole('button', { name: 'Connect', exact: true }).click();
  await page.getByLabel('Username').fill(username!);
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('link', { name: 'Administration', exact: true }).click();
  const nav = page.getByRole('navigation', { name: 'Administration sections' });
  await nav.getByRole('link', { name: 'Playback & encoding', exact: true }).click();
  const crf = page.getByLabel('H264 quality (CRF)', { exact: true });
  await expect(crf).toBeVisible();
  const original = await crf.inputValue();
  const changed = original === '23' ? '24' : '23';
  await crf.fill(changed);
  await page.getByRole('button', { name: 'Save encoding', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Saved on the server.');
  await page.reload();
  await expect(crf).toHaveValue(changed);
  await crf.fill(original);
  await page.getByRole('button', { name: 'Save encoding', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Saved on the server.');
  await nav.getByRole('link', { name: 'Libraries & metadata', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Libraries & metadata', exact: true }),
  ).toBeVisible();
  await expect(page.getByRole('button', { name: /Fixture Movies/ })).toBeVisible();
  await nav.getByRole('link', { name: 'Users & devices', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Users, access & devices', exact: true }),
  ).toBeVisible();
  await nav.getByRole('link', { name: 'System & backups', exact: true }).click();
  await page.getByRole('button', { name: 'logs', exact: true }).click();
  const logs = page.getByRole('combobox', { name: 'Server log', exact: true });
  await expect.poll(() => logs.locator('option').count()).toBeGreaterThan(1);
  await logs.selectOption({ index: 1 });
  await expect.poll(() => page.locator('.server-log').textContent()).not.toBe('');
});

test('real disposable server: synthetic guide, DVR schedules and series rules', async ({
  page,
}) => {
  test.skip(
    !server || !username || password === undefined || process.env.CASTLUMORA_TEST_LIVE !== '1',
    'Requires the disposable M3U/XMLTV fixture.',
  );
  if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server!).hostname))
    throw new Error('Disposable loopback server required.');
  await page.goto('/');
  await page.getByLabel('Server address').fill(server!);
  await page.getByRole('button', { name: 'Connect', exact: true }).click();
  await page.getByLabel('Username').fill(username!);
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  const guide = page.waitForResponse(
    (r) => new URL(r.url()).pathname.endsWith('/LiveTv/Programs') && r.request().method() === 'GET',
  );
  await page.getByRole('link', { name: 'Live TV & guide', exact: true }).click();
  const programs = (await (await guide).json()).Items as { Name: string; StartDate: string }[];
  const upcoming = programs.find(
    (p) =>
      p.Name.startsWith('Synthetic Castle Show') && +new Date(p.StartDate) > Date.now() + 60000,
  );
  if (!upcoming)
    throw new Error(
      'Regenerate the disposable XMLTV fixture: no future synthetic program remains.',
    );
  const name = upcoming.Name;
  await page
    .getByRole('combobox', { name: 'Channel', exact: true })
    .selectOption({ label: 'Castle One' });
  const program = page.locator('.programs details').filter({ hasText: name });
  await program.locator('summary').click();
  await program.getByRole('button', { name: 'Record options', exact: true }).click();
  await page.getByLabel('Padding before (seconds)').fill('15');
  const created = page.waitForResponse(
    (r) => new URL(r.url()).pathname.endsWith('/LiveTv/Timers') && r.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Save single recording', exact: true }).click();
  expect((await created).status()).toBe(204);
  await page.getByRole('button', { name: 'Scheduled', exact: true }).click();
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Edit recording', exact: true }).click();
  await expect(page.getByLabel('Padding before (seconds)')).toHaveValue('15');
  await page.getByLabel('Padding before (seconds)').fill('20');
  const updated = page.waitForResponse(
    (r) =>
      /\/LiveTv\/Timers\/[^/]+$/.test(new URL(r.url()).pathname) && r.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Save single recording', exact: true }).click();
  expect((await updated).status()).toBe(204);
  await page.getByRole('button', { name: 'Edit recording', exact: true }).click();
  await expect(page.getByLabel('Padding before (seconds)')).toHaveValue('20');
  await page.getByRole('button', { name: 'Close options', exact: true }).click();
  page.on('dialog', (dialog) => void dialog.accept());
  await page.getByRole('button', { name: 'Cancel recording', exact: true }).click();
  await expect(page.getByText('No scheduled available.', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Guide', exact: true }).click();
  await program.locator('summary').click();
  await program.getByRole('button', { name: 'Record options', exact: true }).click();
  await page.getByLabel('Record the series', { exact: true }).check();
  const series = page.waitForResponse(
    (r) =>
      new URL(r.url()).pathname.endsWith('/LiveTv/SeriesTimers') && r.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Save series recording', exact: true }).click();
  expect((await series).status()).toBe(204);
  await page.getByRole('button', { name: 'Series', exact: true }).click();
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Cancel recording', exact: true }).click();
  await expect(page.getByText('No series available.', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Recordings', exact: true }).click();
  await expect(page.getByText('No recordings available.', { exact: true })).toBeVisible();
});

test('real disposable server: synthetic live playback, channel switching and session cleanup', async ({
  page,
}) => {
  test.setTimeout(90000);
  test.skip(
    !server || !username || password === undefined || process.env.CASTLUMORA_TEST_LIVE !== '1',
    'Requires disposable M3U/XMLTV fixture.',
  );
  if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server!).hostname))
    throw new Error('Disposable loopback server required.');
  await page.goto('/');
  await page.getByLabel('Server address').fill(server!);
  await page.getByRole('button', { name: 'Connect', exact: true }).click();
  await page.getByLabel('Username').fill(username!);
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('link', { name: 'Live TV & guide', exact: true }).click();
  await page.getByRole('link', { name: /Castle One.*Watch live/ }).click();
  await expect
    .poll(() => page.locator('video').evaluate((v: HTMLVideoElement) => v.currentTime), {
      timeout: 30000,
    })
    .toBeGreaterThan(1);
  const closed = page.waitForResponse(
    (r) => r.url().includes('/LiveStreams/Close') && r.request().method() === 'POST',
  );
  await page
    .getByRole('combobox', { name: 'Switch channel', exact: true })
    .selectOption({ label: 'Castle Two' });
  expect((await closed).ok()).toBe(true);
  await expect(page.getByRole('heading', { name: 'Castle Two', exact: true })).toBeVisible();
  await expect
    .poll(() => page.locator('video').evaluate((v: HTMLVideoElement) => v.currentTime), {
      timeout: 30000,
    })
    .toBeGreaterThan(1);
  const stopped = page.waitForResponse(
    (r) => r.url().includes('/LiveStreams/Close') && r.request().method() === 'POST',
  );
  await page.getByRole('link', { name: 'Back', exact: true }).click();
  expect((await stopped).ok()).toBe(true);
});

test('real disposable server: approved intro manual/automatic/disabled playback modes', async ({
  page,
}) => {
  const episode = process.env.CASTLUMORA_TEST_EPISODE;
  test.skip(
    !server || !username || password === undefined || !episode,
    'Requires reviewed synthetic skipper episodes.',
  );
  if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server!).hostname))
    throw new Error('Disposable loopback server required.');
  await page.goto('/');
  await page.getByLabel('Server address').fill(server!);
  await page.getByRole('button', { name: 'Connect', exact: true }).click();
  await page.getByLabel('Username').fill(username!);
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Browse', exact: true })).toBeVisible();
  await page.goto('/#/play/' + episode);
  const mode = page.getByRole('combobox', { name: 'Episode skipping', exact: true });
  await mode.selectOption('manual');
  await expect
    .poll(() => page.locator('video').evaluate((v: HTMLVideoElement) => v.currentTime), {
      timeout: 20000,
    })
    .toBeGreaterThan(0);
  const timeline = page.getByRole('slider', { name: /Playback position/ });
  await timeline.press('Home');
  for (let i = 0; i < 15; i++) await timeline.press('ArrowRight');
  await page.getByRole('button', { name: 'Skip Intro', exact: true }).click();
  await expect
    .poll(() => page.locator('video').evaluate((v: HTMLVideoElement) => v.currentTime))
    .toBeGreaterThanOrEqual(40);
  await mode.selectOption('automatic');
  await timeline.press('Home');
  for (let i = 0; i < 15; i++) await timeline.press('ArrowRight');
  await expect
    .poll(() => page.locator('video').evaluate((v: HTMLVideoElement) => v.currentTime))
    .toBeGreaterThanOrEqual(40);
  await mode.selectOption('disabled');
  await timeline.press('Home');
  for (let i = 0; i < 15; i++) await timeline.press('ArrowRight');
  await expect(page.getByRole('button', { name: 'Skip Intro', exact: true })).toHaveCount(0);
  const time = await page.locator('video').evaluate((v: HTMLVideoElement) => v.currentTime);
  expect(time).toBeGreaterThanOrEqual(10);
  expect(time).toBeLessThan(30);
  await mode.selectOption('default');
  const stopped = page.waitForResponse(
    (r) => r.url().includes('/Sessions/Playing/Stopped') && r.request().method() === 'POST',
  );
  await page.getByRole('link', { name: 'Back', exact: true }).click();
  expect((await stopped).ok()).toBe(true);
});

test('real disposable server: native skipper administrator review persists', async ({ page }) => {
  test.skip(
    !server || !username || password === undefined || !process.env.CASTLUMORA_TEST_EPISODE,
    'Requires reviewed synthetic episodes.',
  );
  if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server!).hostname))
    throw new Error('Disposable loopback server required.');
  await page.goto('/');
  await page.getByLabel('Server address').fill(server!);
  await page.getByRole('button', { name: 'Connect', exact: true }).click();
  await page.getByLabel('Username').fill(username!);
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('link', { name: 'Administration', exact: true }).click();
  await page
    .getByRole('navigation', { name: 'Administration sections' })
    .getByRole('link', { name: 'Intro & outro skipper', exact: true })
    .click();
  await page.getByText('Choose episode to review', { exact: true }).click();
  await page
    .getByLabel('Choose episode to review search', { exact: true })
    .fill('Synthetic Skipper Episode 1');
  await page.getByRole('button', { name: 'Search', exact: true }).click();
  await page.getByRole('button', { name: /Synthetic Skipper Episode 1/ }).click();
  const protectedFrom = page.getByLabel('Protect content from this second (optional)', {
    exact: true,
  });
  await expect(protectedFrom).toHaveValue('170');
  const intro = page.getByRole('group', { name: 'Intro interval 1', exact: true });
  await expect(intro.getByLabel('Reviewed and approved for skipping')).toBeChecked();
  const original = await intro.getByLabel('End (seconds)').inputValue();
  await intro.getByLabel('End (seconds)').fill('41');
  const saved = page.waitForResponse(
    (r) => r.url().includes('/Castlumora/Skipper/Items/') && r.request().method() === 'PUT',
  );
  await page.getByRole('button', { name: 'Save reviewed intervals', exact: true }).click();
  expect((await saved).ok()).toBe(true);
  await expect(intro.getByLabel('End (seconds)')).toHaveValue('41');
  await intro.getByLabel('End (seconds)').fill(original);
  const restored = page.waitForResponse(
    (r) => r.url().includes('/Castlumora/Skipper/Items/') && r.request().method() === 'PUT',
  );
  await page.getByRole('button', { name: 'Save reviewed intervals', exact: true }).click();
  expect((await restored).ok()).toBe(true);
});

test('real disposable server: administrator invitation, restricted signup and persistent pairing', async ({
  page,
  browser,
}) => {
  test.setTimeout(90000);
  test.skip(
    !server ||
      !username ||
      password === undefined ||
      process.env.CASTLUMORA_TEST_REGISTRATION !== '1',
    'Requires the marked disposable registration fixture.',
  );
  if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server!).hostname))
    throw new Error('Disposable loopback server required.');
  const fixtureRoot = (
    process.env.CASTLUMORA_TEST_FIXTURE_ROOT ?? '/tmp/castlumora-web-real-server'
  ).replace(/\/$/, '');
  if (!existsSync(fixtureRoot + '/.castlumora-disposable'))
    throw new Error('Registration tests require the explicit disposable fixture marker.');
  await page.goto('/');
  await page.getByLabel('Server address').fill(server!);
  await page.getByRole('button', { name: 'Connect', exact: true }).click();
  await page.getByLabel('Username').fill(username!);
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('link', { name: 'Administration', exact: true }).click();
  const session = (await page.evaluate(() =>
    JSON.parse(sessionStorage.getItem('castlumora-session')!),
  )) as { token: string; deviceId: string };
  const headers = {
    Authorization: `MediaBrowser Client="Castlumora Web", Device="Browser", DeviceId="${session.deviceId}", Version="1.0", Token="${session.token}"`,
  };
  const info = await (
    await page.request.get(server!.replace(/\/$/, '') + '/System/Info', { headers })
  ).json();
  if (info.ProgramDataPath !== fixtureRoot + '/data')
    throw new Error('Server data path does not match the disposable registration fixture.');
  const stateUrl = server!.replace(/\/$/, '') + '/Castlumora/Registration/Admin';
  const original = (await (await page.request.get(stateUrl, { headers })).json()).settings;
  const signupName = 'SyntheticSignup' + Date.now();
  let invitationId: string | undefined;
  page.setDefaultTimeout(15000);
  const guest = await browser.newContext();
  guest.setDefaultTimeout(15000);
  try {
    await page
      .getByRole('navigation', { name: 'Administration sections' })
      .getByRole('link', { name: 'Registration & invitations', exact: true })
      .click();
    await page.getByLabel('Enable self-registration', { exact: true }).check();
    await page.getByLabel('Require an invitation', { exact: true }).check();
    const saved = page.waitForResponse(
      (r) =>
        r.url().includes('/Castlumora/Registration/Settings') && r.request().method() === 'PUT',
    );
    await page.getByRole('button', { name: 'Save registration settings', exact: true }).click();
    expect((await saved).ok()).toBe(true);
    await page.getByRole('checkbox', { name: 'All libraries', exact: true }).uncheck();
    await page.getByRole('checkbox', { name: 'Fixture Movies', exact: true }).check();
    const policySaved = page.waitForResponse(
      (r) =>
        r.url().includes('/Castlumora/Registration/Settings') && r.request().method() === 'PUT',
    );
    await page
      .getByRole('button', { name: 'Save default signup permissions', exact: true })
      .click();
    expect((await policySaved).ok()).toBe(true);
    await page.getByLabel('Invitation label', { exact: true }).fill('Browser ' + signupName);
    const invitation = page.waitForResponse(
      (r) =>
        r.url().includes('/Castlumora/Registration/Invitations') && r.request().method() === 'POST',
    );
    await page.getByRole('button', { name: 'Create invitation', exact: true }).click();
    const inviteResponse = await invitation;
    expect(inviteResponse.ok()).toBe(true);
    invitationId = (await inviteResponse.json()).id;
    const invitationLink = await page
      .getByRole('textbox', { name: 'One-time invitation link', exact: true })
      .inputValue();
    const visitor = await guest.newPage();
    await visitor.goto(invitationLink);
    await expect.poll(() => visitor.url()).not.toContain('invite=');
    await visitor.getByRole('button', { name: 'Check registration', exact: true }).click();
    await visitor.getByLabel('New username', { exact: true }).fill(signupName);
    await visitor.getByLabel('New password', { exact: true }).fill('Synthetic-signup-password-1');
    await visitor
      .getByLabel('Confirm new password', { exact: true })
      .fill('Synthetic-signup-password-1');
    await visitor.getByRole('button', { name: 'Create account', exact: true }).click();
    await expect(
      visitor.getByRole('heading', { name: 'Your account is ready', exact: true }),
    ).toBeVisible();
    await visitor.getByRole('link', { name: 'Go to sign in', exact: true }).click();
    await visitor.getByLabel('Username', { exact: true }).fill(signupName);
    await expect(visitor.getByLabel('Server address', { exact: true })).toHaveCount(0);
    await visitor.getByLabel('Password', { exact: true }).fill('Synthetic-signup-password-1');
    await visitor.getByRole('button', { name: 'Sign in', exact: true }).click();
    await visitor.getByRole('link', { name: 'Browse', exact: true }).click();
    await expect(
      visitor.getByRole('link', { name: 'Details for Castlumora Test', exact: true }),
    ).toBeVisible();
    await expect(
      visitor.getByRole('link', { name: 'Details for Castlumora Skipper', exact: true }),
    ).toHaveCount(0);
    await expect(visitor.getByRole('link', { name: 'Administration', exact: true })).toHaveCount(0);
    await visitor.evaluate(() => sessionStorage.removeItem('castlumora-session'));
    await visitor.reload();
    await expect(visitor.getByLabel('Username', { exact: true })).toBeVisible();
    await expect(visitor.getByLabel('Server address', { exact: true })).toHaveCount(0);
    await expect(visitor.getByLabel('Password', { exact: true })).toHaveValue('');
  } finally {
    await guest.close();
    const base = server!.replace(/\/$/, '');
    const users = (await (await page.request.get(base + '/Users', { headers })).json()) as {
      Id: string;
      Name: string;
    }[];
    const created = users.find((user) => user.Name === signupName);
    if (created)
      expect((await page.request.delete(base + '/Users/' + created.Id, { headers })).ok()).toBe(
        true,
      );
    if (invitationId)
      expect(
        (
          await page.request.delete(base + '/Castlumora/Registration/Invitations/' + invitationId, {
            headers,
          })
        ).ok(),
      ).toBe(true);
    const current = (await (await page.request.get(stateUrl, { headers })).json()).settings;
    expect(
      (
        await page.request.put(base + '/Castlumora/Registration/Settings', {
          headers,
          data: { ...original, revision: current.revision },
        })
      ).ok(),
    ).toBe(true);
  }
});
