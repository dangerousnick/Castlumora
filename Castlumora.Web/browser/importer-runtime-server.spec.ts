// Opt-in managed-tool verification on our marked disposable server, never production.
import { test, expect } from '@playwright/test';
import { existsSync } from 'node:fs';
const root = '/tmp/castlumora-web-real-server';
test('managed importer runtime: native install, integrity health, reload and viewer denials', async ({
  page,
  request,
}) => {
  const server = process.env.CASTLUMORA_TEST_SERVER;
  const username = process.env.CASTLUMORA_TEST_USER;
  const password = process.env.CASTLUMORA_TEST_PASSWORD;
  test.skip(
    process.env.CASTLUMORA_TEST_IMPORTER_RUNTIME !== '1' ||
      !server ||
      !username ||
      password === undefined,
  );
  test.setTimeout(90000);
  page.setDefaultTimeout(20000);
  if (new URL(server!).hostname !== '127.0.0.1' || !existsSync(root + '/.castlumora-disposable'))
    throw new Error('Owned loopback fixture required.');
  const header =
    'MediaBrowser Client="Importer runtime verification", Device="Disposable", DeviceId="importer-runtime-admin", Version="1"';
  const login = await request.post(server! + '/Users/AuthenticateByName', {
    headers: { Authorization: header },
    data: { Username: username, Pw: password },
  });
  expect(login.ok()).toBe(true);
  const auth = await login.json();
  const headers = { Authorization: header + ', Token="' + auth.AccessToken + '"' };
  const get = async (path: string) => {
    const response = await request.get(server! + path, { headers });
    expect(response.ok()).toBe(true);
    return response.json();
  };
  expect((await get('/System/Info')).ProgramDataPath).toBe(root + '/data');
  const before = await get('/Castlumora/Imports/Runtime');
  expect(before.supported).toBe(true);
  expect(before.bundleAvailable).toBe(true);
  expect(before.downloadJobsAvailable).toBe(false);
  await page.goto('/');
  await page.getByLabel('Server address').fill(server!);
  await page.getByRole('button', { name: 'Connect', exact: true }).click();
  await page.getByLabel('Username', { exact: true }).fill(username!);
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Browse', exact: true })).toBeVisible();
  await page.goto('/#/admin/imports');
  await expect(page.getByRole('heading', { name: 'Imports & Sources', exact: true })).toBeVisible();
  page.once('dialog', (dialog) => dialog.accept());
  const installed = page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname.endsWith('/Castlumora/Imports/Runtime/Install') &&
      response.request().method() === 'POST',
  );
  await page
    .getByRole('button', { name: /^(Install bundled runtime|Reinstall pinned runtime)$/ })
    .click();
  expect((await installed).ok()).toBe(true);
  await expect(
    page.getByText('Managed runtime installed and checked.', { exact: true }),
  ).toBeVisible();
  await expect(page.getByText('Ready', { exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByText('Ready', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Check runtime', exact: true }).click();
  await expect(
    page.getByText('Runtime health checked on the server.', { exact: true }),
  ).toBeVisible();
  const ready = await get('/Castlumora/Imports/Runtime');
  expect(ready.ready).toBe(true);
  expect(ready.expectedSha256).toBe(before.expectedSha256);
  expect(ready.expectedVersion).toBe(before.expectedVersion);
  expect(ready.downloadJobsAvailable).toBe(false);
  let viewerId: string | undefined;
  try {
    const created = await request.post(server! + '/Users/New', {
      headers,
      data: { Name: 'SyntheticImporterRuntimeViewer' + Date.now() },
    });
    expect(created.ok()).toBe(true);
    const viewer = await created.json();
    viewerId = viewer.Id;
    const viewerHeader = header.replace('importer-runtime-admin', 'importer-runtime-viewer');
    const vl = await request.post(server! + '/Users/AuthenticateByName', {
      headers: { Authorization: viewerHeader },
      data: { Username: viewer.Name, Pw: '' },
    });
    expect(vl.ok()).toBe(true);
    const vh = { Authorization: viewerHeader + ', Token="' + (await vl.json()).AccessToken + '"' };
    expect([401, 403]).toContain(
      (await request.get(server! + '/Castlumora/Imports/Runtime', { headers: vh })).status(),
    );
    expect([401, 403]).toContain(
      (
        await request.post(server! + '/Castlumora/Imports/Runtime/Install', { headers: vh })
      ).status(),
    );
  } finally {
    if (viewerId)
      expect((await request.delete(server! + '/Users/' + viewerId, { headers })).ok()).toBe(true);
  }
});
