import { randomUUID } from 'node:crypto';
import { expect, type APIRequestContext, type Page } from '@playwright/test';
import { readPreview } from './preview-read';

export async function verifyOwnedProfileSessions(
  page: Page,
  request: APIRequestContext,
  preview: string,
  password: string,
  profileId: string,
) {
  const address = new URL(preview);
  if (address.hostname !== '127.0.0.1' || address.port !== '28096')
    throw new Error('Dedicated owned-profile preview required');
  const endpoint = (path: string) => new URL('../' + path, preview).href;
  const owner = await page.evaluate(() =>
    JSON.parse(sessionStorage.getItem('castlumora-session')!),
  );
  const ownerHeaders = { Authorization: `MediaBrowser Token="${owner.token}"` };
  const createChildSignIn = async (device: string, switchProfile = true) => {
    const header = `MediaBrowser Client="Owned profile fixture", Device="${device}", DeviceId="${randomUUID()}", Version="1"`;
    try {
      const login = await request.post(endpoint('Users/AuthenticateByName'), {
        headers: { Authorization: header },
        data: { Username: owner.user.Name, Pw: password },
      });
      expect(login.ok()).toBe(true);
      const token = (await login.json()).AccessToken;
      if (!switchProfile) return { Authorization: header + `, Token="${token}"` };
      const switched = await request.post(endpoint('Castlumora/Profiles/Switch'), {
        headers: { Authorization: header + `, Token="${token}"` },
        data: { profileId, accountPassword: password },
      });
      expect(switched.ok()).toBe(true);
      return { Authorization: header + `, Token="${(await switched.json()).AccessToken}"` };
    } catch {
      throw new Error('Owned profile sign-in fixture failed; mutation was not retried.');
    }
  };
  const first = await createChildSignIn('Owned profile selected');
  const second = await createChildSignIn('Owned profile retained');
  const route = 'Castlumora/Account/ProfileSessions/' + profileId;
  expect((await readPreview(request, endpoint(route), { headers: second })).status()).toBe(403);
  await page.locator('.profile-menu > summary').click();
  await page.getByRole('link', { name: 'Viewing profiles', exact: true }).click();
  await page
    .getByRole('combobox', { name: 'Manage profile sign-ins', exact: true })
    .selectOption({ label: 'Packaged Family Updated' });
  const panel = page.getByRole('region', {
    name: 'Packaged Family Updated signed-in devices',
    exact: true,
  });
  const row = panel.getByRole('article', {
    name: 'Owned profile selected · Owned profile fixture',
  });
  await expect(row).toBeVisible();
  page.once('dialog', (dialog) => dialog.accept());
  await row.getByRole('button', { name: 'Sign out this device', exact: true }).click();
  await expect(panel.getByText('Saved sign-in revoked.', { exact: true })).toBeVisible();
  await expect(row).toHaveCount(0);
  const status = async (headers: Record<string, string>) =>
    (await readPreview(request, endpoint('Users/Me'), { headers })).status();
  expect(await status(first)).toBe(401);
  expect(await status(second)).toBe(200);
  expect(await status(ownerHeaders)).toBe(200);
  page.once('dialog', (dialog) => dialog.accept());
  await panel.getByRole('button', { name: 'Sign out all profile devices', exact: true }).click();
  await expect(
    panel.getByText('Viewing profile signed out on all devices. Your account remains signed in.', {
      exact: true,
    }),
  ).toBeVisible();
  expect(await status(second)).toBe(401);
  expect(await status(ownerHeaders)).toBe(200);
  await expect(panel.getByRole('article')).toHaveCount(0);
  const freshChild = await createChildSignIn('Account-wide child fixture');
  const otherOwner = await createChildSignIn('Account-wide owner fixture', false);
  expect(await status(freshChild)).toBe(200);
  expect(await status(otherOwner)).toBe(200);
  page.once('dialog', (dialog) => dialog.accept());
  await page
    .getByRole('button', { name: 'Sign out other account and profile devices', exact: true })
    .click();
  await expect(
    page.getByText(
      'Other account and viewing-profile devices signed out. This device remains signed in.',
      { exact: true },
    ),
  ).toBeVisible();
  expect(await status(freshChild)).toBe(401);
  expect(await status(otherOwner)).toBe(401);
  expect(await status(ownerHeaders)).toBe(200);
  await expect(panel).toHaveCount(0);
  await page.getByRole('link', { name: 'Administration', exact: true }).click();
  await page
    .getByRole('navigation', { name: 'Administration sections' })
    .getByRole('link', { name: 'Users & devices', exact: true })
    .click();
}
