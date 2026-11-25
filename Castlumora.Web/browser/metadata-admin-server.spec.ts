// Opt-in administrator mutations only on our owned synthetic creator library.
import { test, expect } from '@playwright/test';
import { existsSync, readFileSync, writeFileSync, unlinkSync } from 'node:fs';
const root = '/tmp/castlumora-web-real-server';
test('native metadata management persists readers and corrections, repairs and denies viewers', async ({
  page,
  request,
}) => {
  const server = process.env.CASTLUMORA_TEST_SERVER;
  const username = process.env.CASTLUMORA_TEST_USER;
  const password = process.env.CASTLUMORA_TEST_PASSWORD;
  test.skip(
    process.env.CASTLUMORA_TEST_CREATOR !== '1' || !server || !username || password === undefined,
  );
  test.setTimeout(120000);
  page.setDefaultTimeout(15000);
  if (new URL(server!).hostname !== '127.0.0.1' || !existsSync(root + '/.castlumora-disposable'))
    throw new Error('Owned loopback fixture required.');
  const fixture = JSON.parse(readFileSync(root + '/creator-sidecar-result.json', 'utf8'));
  const header =
    'MediaBrowser Client="Metadata administration", Device="Disposable", DeviceId="metadata-admin-verify", Version="1"';
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
  const library = (await get('/Library/VirtualFolders')).find(
    (v: { Name: string }) => v.Name === 'Fixture Creator Sidecar',
  );
  expect(library.Locations).toEqual([root + '/creator-sidecar']);
  const original = await get('/Items/' + fixture.itemId);
  const source = JSON.parse(readFileSync(fixture.path.replace(/\.mp4$/, '.info.json'), 'utf8'));
  const recoveryPath = root + '/metadata-admin-state.json';
  if (existsSync(recoveryPath))
    throw new Error('Restore the previous owned metadata fixture before another run.');
  const recovery = { library, original, viewerId: '' };
  writeFileSync(recoveryPath, JSON.stringify(recovery), { mode: 0o600 });
  let viewerId: string | undefined;
  try {
    await page.goto('/');
    await page.getByLabel('Server address').fill(server!);
    await page.getByRole('button', { name: 'Connect', exact: true }).click();
    await page.getByLabel('Username', { exact: true }).fill(username!);
    await page.getByLabel('Password', { exact: true }).fill(password!);
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await expect(page.getByRole('link', { name: 'Browse', exact: true })).toBeVisible();
    await page.goto('/#/admin/metadata');
    await expect(
      page.getByRole('heading', { name: 'Metadata Management', exact: true }),
    ).toBeVisible();
    await page.getByLabel('Metadata library', { exact: true }).selectOption(library.ItemId);
    const enabled = page.getByLabel('Enable YouTube sidecar reader', { exact: true });
    await enabled.uncheck();
    await page.getByRole('button', { name: 'Save reader settings', exact: true }).click();
    await expect
      .poll(
        async () =>
          (await get('/Library/VirtualFolders')).find(
            (v: { ItemId: string }) => v.ItemId === library.ItemId,
          ).LibraryOptions.DisabledLocalMetadataReaders,
      )
      .toContain('Castlumora YouTube sidecar');
    await page.reload();
    await page.getByLabel('Metadata library', { exact: true }).selectOption(library.ItemId);
    await expect(enabled).not.toBeChecked();
    await enabled.check();
    await page
      .getByRole('button', { name: 'Move Castlumora YouTube sidecar up', exact: true })
      .click();
    await page.getByRole('button', { name: 'Save reader settings', exact: true }).click();
    await expect
      .poll(
        async () =>
          (await get('/Library/VirtualFolders')).find(
            (v: { ItemId: string }) => v.ItemId === library.ItemId,
          ).LibraryOptions.LocalMetadataReaderOrder[0],
      )
      .toBe('Castlumora YouTube sidecar');
    await page.getByLabel('Find media to correct', { exact: true }).fill(original.Name);
    await page.getByRole('button', { name: 'Search metadata', exact: true }).click();
    await page.getByRole('button', { name: original.Name + ' · Video', exact: true }).click();
    await page
      .getByLabel('Metadata title', { exact: true })
      .fill('Native creator metadata correction');
    await page.getByLabel('Description', { exact: true }).fill('Native administrator correction');
    await page.getByLabel('Lock manual metadata corrections', { exact: true }).check();
    await page.getByRole('button', { name: 'Save metadata corrections', exact: true }).click();
    await expect
      .poll(async () => (await get('/Items/' + fixture.itemId)).Name)
      .toBe('Native creator metadata correction');
    expect((await get('/Items/' + fixture.itemId)).LockData).toBe(true);
    await expect(
      page.getByRole('button', { name: 'Repair from source', exact: true }),
    ).toBeDisabled();
    await page.getByLabel('Lock manual metadata corrections', { exact: true }).uncheck();
    await page.getByRole('button', { name: 'Save metadata corrections', exact: true }).click();
    await expect.poll(async () => (await get('/Items/' + fixture.itemId)).LockData).toBe(false);
    page.once('dialog', (d) => d.accept());
    await page.getByRole('button', { name: 'Repair from source', exact: true }).click();
    await expect
      .poll(async () => (await get('/Items/' + fixture.itemId)).Name, { timeout: 30000 })
      .toBe(source.title);
    await page.getByRole('button', { name: 'Reload metadata', exact: true }).click();
    await expect(page.getByLabel('Metadata title', { exact: true })).toHaveValue(source.title);
    expect((await get('/Items/' + fixture.itemId)).ProviderIds).toEqual(original.ProviderIds);
    const created = await request.post(server! + '/Users/New', {
      headers,
      data: { Name: 'SyntheticMetadataViewer' + Date.now() },
    });
    expect(created.ok()).toBe(true);
    const viewer = await created.json();
    viewerId = viewer.Id;
    recovery.viewerId = viewer.Id;
    writeFileSync(recoveryPath, JSON.stringify(recovery));
    const viewerHeader = header.replace('metadata-admin-verify', 'metadata-viewer-verify');
    const vl = await request.post(server! + '/Users/AuthenticateByName', {
      headers: { Authorization: viewerHeader },
      data: { Username: viewer.Name, Pw: '' },
    });
    expect(vl.ok()).toBe(true);
    const vh = { Authorization: viewerHeader + ', Token="' + (await vl.json()).AccessToken + '"' };
    for (const [path, data] of [
      ['/Items/' + fixture.itemId, original],
      ['/Items/' + fixture.itemId + '/Refresh?metadataRefreshMode=FullRefresh', {}],
      [
        '/Library/VirtualFolders/LibraryOptions',
        { Id: library.ItemId, LibraryOptions: library.LibraryOptions },
      ],
    ] as const)
      expect([401, 403]).toContain(
        (await request.post(server! + path, { headers: vh, data })).status(),
      );
  } finally {
    expect(
      (
        await request.post(server! + '/Library/VirtualFolders/LibraryOptions', {
          headers,
          data: { Id: library.ItemId, LibraryOptions: library.LibraryOptions },
        })
      ).ok(),
    ).toBe(true);
    expect(
      (await request.post(server! + '/Items/' + fixture.itemId, { headers, data: original })).ok(),
    ).toBe(true);
    if (viewerId)
      expect((await request.delete(server! + '/Users/' + viewerId, { headers })).ok()).toBe(true);
    unlinkSync(recoveryPath);
  }
});
