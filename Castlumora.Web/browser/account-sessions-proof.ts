import { randomUUID } from 'node:crypto';
import { expect, type APIRequestContext, type Page } from '@playwright/test';
import { readPreview } from './preview-read';

// Mutate only the dedicated, disposable packaged preview; never retry mutations.
export async function verifyAccountSessions(
  page: Page,
  request: APIRequestContext,
  preview: string,
  password: string,
) {
  const address = new URL(preview);
  if (address.hostname !== '127.0.0.1' || address.port !== '28096')
    throw new Error('Dedicated device-management fixture required');
  const endpoint = (path: string) => new URL('../' + path, preview).href;
  const owner = await page.evaluate(() =>
    JSON.parse(sessionStorage.getItem('castlumora-session')!),
  );
  const currentHeaders = { Authorization: `MediaBrowser Token="${owner.token}"` };
  const login = async (device: string) => {
    let result;
    try {
      result = await request.post(endpoint('Users/AuthenticateByName'), {
        headers: {
          Authorization: `MediaBrowser Client="Device fixture", Device="${device}", DeviceId="devices-${randomUUID()}", Version="1.0"`,
        },
        data: { Username: owner.user.Name, Pw: password },
      });
    } catch {
      throw new Error('Owned device fixture login failed; mutation was not retried.');
    }
    expect(result.ok()).toBe(true);
    return { Authorization: `MediaBrowser Token="${(await result.json()).AccessToken}"` };
  };
  const selectedName = 'Owned selected sign-in';
  const retainedName = 'Owned retained sign-in';
  const selected = await login(selectedName);
  const retained = await login(retainedName);
  // Refresh the same integrated page so persisted inventory includes both new authorizations.
  await page.reload();
  const panel = page.getByRole('region', { name: 'My signed-in devices', exact: true });
  await expect(panel.getByText('Current sign-in', { exact: true })).toBeVisible();
  const selectedRow = panel.getByRole('article', { name: selectedName + ' · Device fixture' });
  await expect(selectedRow).toBeVisible();
  page.once('dialog', (dialog) => dialog.accept());
  await selectedRow.getByRole('button', { name: 'Sign out this device', exact: true }).click();
  await expect(panel.getByText('Saved sign-in revoked.', { exact: true })).toBeVisible();
  await expect(selectedRow).toHaveCount(0);
  expect((await readPreview(request, endpoint('Users/Me'), { headers: selected })).status()).toBe(
    401,
  );
  expect((await readPreview(request, endpoint('Users/Me'), { headers: retained })).status()).toBe(
    200,
  );
  expect(
    (await readPreview(request, endpoint('Users/Me'), { headers: currentHeaders })).status(),
  ).toBe(200);
  page.once('dialog', (dialog) => dialog.accept());
  await panel.getByRole('button', { name: 'Sign out other devices', exact: true }).click();
  await expect(
    panel.getByText(
      'Other sign-ins for this identity were revoked. This device remains signed in.',
      { exact: true },
    ),
  ).toBeVisible();
  expect((await readPreview(request, endpoint('Users/Me'), { headers: retained })).status()).toBe(
    401,
  );
  expect(
    (await readPreview(request, endpoint('Users/Me'), { headers: currentHeaders })).status(),
  ).toBe(200);
  await expect(panel.getByRole('article')).toHaveCount(1);
  await expect(
    panel.getByRole('button', { name: 'Sign out other devices', exact: true }),
  ).toBeDisabled();
  await expect(
    panel.getByRole('button', { name: 'Sign out this device', exact: true }),
  ).toHaveCount(0);
}
