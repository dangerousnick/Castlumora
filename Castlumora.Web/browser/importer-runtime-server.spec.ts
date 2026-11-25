// Opt-in managed-tool verification on our marked disposable server, never production.
import { test, expect } from '@playwright/test';
import { existsSync, mkdirSync, rmdirSync } from 'node:fs';
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
  expect(before.downloadJobsAvailable).toBe(before.ready);
  await page.goto('/');
  if (new URL(page.url()).origin !== new URL(server!).origin) {
    await page.getByLabel('Server address').fill(server!);
    await page.getByRole('button', { name: 'Connect', exact: true }).click();
  }
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
  await expect(page.getByText('Ready', { exact: true })).toBeVisible({ timeout: 20000 });
  await page.reload();
  await expect(page.getByText('Ready', { exact: true })).toBeVisible({ timeout: 20000 });
  await page.getByRole('button', { name: 'Check runtime', exact: true }).click();
  await expect(
    page.getByText('Runtime health checked on the server.', { exact: true }),
  ).toBeVisible();
  const ready = await get('/Castlumora/Imports/Runtime');
  expect(ready.ready).toBe(true);
  expect(ready.expectedSha256).toBe(before.expectedSha256);
  expect(ready.expectedVersion).toBe(before.expectedVersion);
  expect(ready.downloadJobsAvailable).toBe(true);
  await expect(page.getByRole('heading', { name: 'Import a video', exact: true })).toBeVisible();
  const libraryName = 'SyntheticImporterBrowser' + Date.now();
  const libraryPath = root + '/import-browser-library';
  mkdirSync(libraryPath, { recursive: true, mode: 0o700 });
  try {
    expect(
      (
        await request.post(
          server! +
            '/Library/VirtualFolders?name=' +
            libraryName +
            '&collectionType=homevideos&refreshLibrary=true&paths=' +
            encodeURIComponent(libraryPath),
          {
            headers,
            data: { LibraryOptions: { EnableRealtimeMonitor: true } },
          },
        )
      ).ok(),
    ).toBe(true);
    await expect
      .poll(
        async () =>
          (await get('/Castlumora/Imports/Queue')).destinations.map(
            (item: { name: string }) => item.name,
          ),
        { timeout: 20000 },
      )
      .toContain(libraryName);
    await page.reload();
    await page
      .getByRole('combobox', { name: 'Destination library', exact: true })
      .selectOption({ label: libraryName });
    await page.getByLabel('Source URL', { exact: true }).fill('https://127.0.0.1/private.mp4');
    await page.getByLabel('Imported title', { exact: true }).fill('Owned browser test');
    await page
      .getByLabel('Rights / permission record', { exact: true })
      .fill('Synthetic rejection test; no download authorized');
    await expect(
      page.getByRole('button', { name: 'Queue authorized video', exact: true }),
    ).toBeDisabled();
    await page
      .getByRole('checkbox', {
        name: 'I have the rights and permission to download and retain this source.',
      })
      .check();
    const beforeJobs = (await get('/Castlumora/Imports/Queue')).jobs.length;
    const rejected = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname.endsWith('/Castlumora/Imports/Jobs') &&
        response.request().method() === 'POST',
    );
    await page.getByRole('button', { name: 'Queue authorized video', exact: true }).click();
    expect((await rejected).status()).toBe(400);
    expect((await get('/Castlumora/Imports/Queue')).jobs).toHaveLength(beforeJobs);
  } finally {
    expect(
      (
        await request.delete(
          server! + '/Library/VirtualFolders?name=' + libraryName + '&refreshLibrary=false',
          { headers },
        )
      ).ok(),
    ).toBe(true);
    rmdirSync(libraryPath);
  }
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
