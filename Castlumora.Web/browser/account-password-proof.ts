import { randomUUID } from 'node:crypto';
import { expect, type APIRequestContext, type Page } from '@playwright/test';
import { readPreview } from './preview-read';

// Only owned disposable preview credentials; never retries a password mutation.
export async function verifyAccountPasswordChange(
  page: Page,
  request: APIRequestContext,
  preview: string,
  password: string,
) {
  const address = new URL(preview);
  if (address.hostname !== '127.0.0.1' || address.port !== '28096')
    throw new Error('Dedicated password fixture required');
  const endpoint = (path: string) => new URL('../' + path, preview).href;
  const owner = await page.evaluate(() =>
    JSON.parse(sessionStorage.getItem('castlumora-session')!),
  );
  const currentHeaders = { Authorization: `MediaBrowser Token="${owner.token}"` };
  const fixtureHeaders = {
    Authorization: `MediaBrowser Client="Password fixture", Device="CI", DeviceId="password-${randomUUID()}", Version="1.0"`,
  };
  const post = async (path: string, data: Record<string, unknown>, headers = fixtureHeaders) => {
    try {
      return await request.post(endpoint(path), { headers, data });
    } catch {
      throw new Error('Owned password fixture request failed; mutation was not retried.');
    }
  };
  const secondary = await post('Users/AuthenticateByName', {
    Username: owner.user.Name,
    Pw: password,
  });
  expect(secondary.ok()).toBe(true);
  const secondaryToken = (await secondary.json()).AccessToken;
  const secondaryHeaders = { Authorization: `MediaBrowser Token="${secondaryToken}"` };
  expect(
    (await readPreview(request, endpoint('Users/Me'), { headers: secondaryHeaders })).status(),
  ).toBe(200);
  await page.locator('.profile-menu > summary').click();
  await page.getByRole('link', { name: 'Viewing profiles', exact: true }).click();
  const next = 'PreviewUpdated-' + password;
  let attempted = false;
  try {
    await expect(
      page.getByRole('heading', { name: 'Account security', exact: true }),
    ).toBeVisible();
    await page
      .getByLabel('Current account password', { exact: true })
      .fill('Incorrect-' + password);
    await page.getByLabel('New account password', { exact: true }).fill(next);
    await page.getByLabel('Confirm new account password', { exact: true }).fill(next);
    attempted = true;
    const rejected = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname.endsWith('/Password') &&
        response.request().method() === 'POST',
    );
    await page.getByRole('button', { name: 'Update account password', exact: true }).click();
    expect((await rejected).status()).toBe(403);
    await expect(page.getByRole('status').filter({ hasText: '403' })).toBeVisible();
    await expect(page.getByLabel('Current account password', { exact: true })).toHaveValue('');
    expect(
      (await readPreview(request, endpoint('Users/Me'), { headers: secondaryHeaders })).status(),
    ).toBe(200);
    await page.getByLabel('Current account password', { exact: true }).fill(password);
    await page.getByLabel('New account password', { exact: true }).fill(next);
    await page.getByLabel('Confirm new account password', { exact: true }).fill(next);
    attempted = true;
    await page.getByRole('button', { name: 'Update account password', exact: true }).click();
    await expect(
      page.getByText(
        'Password updated. This session remains signed in; other sessions for this identity were revoked.',
        { exact: true },
      ),
    ).toBeVisible();
    for (const label of [
      'Current account password',
      'New account password',
      'Confirm new account password',
    ])
      await expect(page.getByLabel(label, { exact: true })).toHaveValue('');
    expect(
      (await readPreview(request, endpoint('Users/Me'), { headers: currentHeaders })).status(),
    ).toBe(200);
    expect(
      (await readPreview(request, endpoint('Users/Me'), { headers: secondaryHeaders })).status(),
    ).toBe(401);
    const oldLogin = await post('Users/AuthenticateByName', {
      Username: owner.user.Name,
      Pw: password,
    });
    expect(oldLogin.status()).toBe(401);
    const newLogin = await post('Users/AuthenticateByName', {
      Username: owner.user.Name,
      Pw: next,
    });
    expect(newLogin.ok()).toBe(true);
    const stored = await page.evaluate(() =>
      JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }),
    );
    expect(stored).not.toContain(password);
    expect(stored).not.toContain(next);
  } finally {
    if (attempted) {
      // Always restore only this owned fixture, including uncertain network outcomes.
      const restored = await post(
        'Users/' + owner.user.Id + '/Password',
        { CurrentPw: next, NewPw: password, ResetPassword: false },
        currentHeaders,
      );
      if (!restored.ok()) {
        const unchanged = await post('Users/AuthenticateByName', {
          Username: owner.user.Name,
          Pw: password,
        });
        expect(unchanged.ok()).toBe(true);
      }
    }
  }
}
