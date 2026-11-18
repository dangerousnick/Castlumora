// Real, extracted-package restart/recovery workflow; no production fault endpoint.
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { test, expect } from '@playwright/test';
import { readPreview } from './preview-read';

const stage = process.env.CASTLUMORA_PREVIEW_RECOVERY_STAGE;
const preview = process.env.CASTLUMORA_PREVIEW_URL;
const password = process.env.CASTLUMORA_PREVIEW_PASSWORD;
test('packaged profile recovery retains identity and viewing data across restart', async ({
  page,
  request,
}) => {
  test.skip(!preview || !password || !stage || !process.env.RUNNER_TEMP);
  const target = new URL(preview!);
  if (
    target.hostname !== '127.0.0.1' ||
    target.port !== '28096' ||
    !['interrupted', 'persisted'].includes(stage!)
  )
    throw new Error('Dedicated recovery fixture required');
  const fixture = JSON.parse(
    readFileSync(process.env.RUNNER_TEMP + '/castlumora-profile-recovery.json', 'utf8'),
  );
  page.setDefaultTimeout(20000);
  test.setTimeout(90000);
  await page.goto(preview!);
  await page.getByLabel('Username', { exact: true }).fill('PreviewOwner');
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Administration', exact: true })).toBeVisible();
  const session = () =>
    page.evaluate(() => JSON.parse(sessionStorage.getItem('castlumora-session')!));
  const owner = await session();
  expect(owner.user.Id.replaceAll('-', '')).toBe(fixture.ownerId.replaceAll('-', ''));
  const headers = { Authorization: `MediaBrowser Token="${owner.token}"` };
  const endpoint = (path: string) => new URL('../' + path, preview!).href;
  await page.getByRole('link', { name: 'Administration', exact: true }).click();
  await page
    .getByRole('navigation', { name: 'Administration sections' })
    .getByRole('link', { name: 'Users & devices', exact: true })
    .click();
  if (stage === 'interrupted') {
    await expect(
      page.getByText('Provisioning interrupted — unavailable; preserve for recovery or remove', {
        exact: true,
      }),
    ).toBeVisible();
    const inventory = await readPreview(request, endpoint('Castlumora/Profiles/Managed'), {
      headers,
    });
    const before = (await inventory.json()).profiles.find(
      (profile: { id: string }) => profile.id === fixture.id,
    );
    expect(before.ready).toBe(false);
    page.once('dialog', (dialog) => dialog.accept());
    await page.getByRole('button', { name: 'Recover Recovery Fixture', exact: true }).click();
    await expect(
      page.getByText(
        'Profile recovered. Original identity retained; review its current policy before switching.',
        { exact: true },
      ),
    ).toBeVisible();
  }
  await expect(
    page.getByRole('button', { name: 'Recover Recovery Fixture', exact: true }),
  ).toHaveCount(0);
  const inventory = await readPreview(request, endpoint('Castlumora/Profiles/Managed'), {
    headers,
  });
  const recovered = (await inventory.json()).profiles.find(
    (profile: { id: string }) => profile.id === fixture.id,
  );
  expect(recovered.ready).toBe(true);
  expect(recovered.lifecycle).toBe('active');
  const info = await readPreview(request, endpoint('Users/' + fixture.id), { headers });
  const user = await info.json();
  expect(
    createHash('sha256')
      .update(
        readFileSync('/tmp/castlumora-preview-fresh/config/castlumora/profiles/credentials.key'),
      )
      .digest('hex'),
  ).toBe(fixture.keySha256);
  expect(user.Name).toBe(fixture.coreName);
  expect(user.Configuration).toEqual(fixture.configuration);
  expect(user.Policy.IsAdministrator).toBe(false);
  const media = await readPreview(
    request,
    endpoint('Items/' + fixture.itemId + '?userId=' + fixture.id),
    { headers },
  );
  expect((await media.json()).UserData).toEqual(fixture.userData);
  const blocked = await request.post(
    endpoint('Castlumora/Profiles/Managed/' + fixture.id + '/Recover'),
    { headers, data: {} },
  );
  expect(blocked.status()).toBe(409);
  await page.locator('.profile-menu > summary').click();
  await page.getByRole('link', { name: 'Viewing profiles', exact: true }).click();
  await page
    .getByRole('combobox', { name: 'Viewing profile', exact: true })
    .selectOption({ label: 'Recovery Fixture (kids)' });
  await page.getByLabel('Account password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Switch viewing profile', exact: true }).click();
  await expect(page.locator('.profile-menu > summary')).toContainText('Recovery Fixture');
  const child = await session();
  expect(child.user.Id.replaceAll('-', '')).toBe(fixture.id.replaceAll('-', ''));
  expect((await readPreview(request, endpoint('Users/Me'), { headers })).status()).toBe(401);
  expect(
    (
      await request.post(endpoint('Castlumora/Profiles/Managed/' + fixture.id + '/Recover'), {
        headers: { Authorization: `MediaBrowser Token="${child.token}"` },
        data: {},
      })
    ).status(),
  ).toBe(403);
  expect((await readPreview(request, endpoint('health'))).status()).toBe(200);
});
