import { randomUUID } from 'node:crypto';
import { mkdtempSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { expect, type APIRequestContext, type Page } from '@playwright/test';
import { readPreview } from './preview-read';

export async function verifyProfileAccess(
  page: Page,
  request: APIRequestContext,
  preview: string,
  password: string,
  profileId: string,
  childItemUrl: string,
) {
  const address = new URL(preview);
  if (
    address.hostname !== '127.0.0.1' ||
    address.port !== '28096' ||
    new URL(childItemUrl).origin !== address.origin
  )
    throw new Error('Dedicated profile-access fixture required');
  const endpoint = (path: string) => new URL('../' + path, preview).href;
  const owner = await page.evaluate(() =>
    JSON.parse(sessionStorage.getItem('castlumora-session')!),
  );
  const headers = { Authorization: `MediaBrowser Token="${owner.token}"` };
  const info = await (await readPreview(request, endpoint('System/Info'), { headers })).json();
  if (
    !['/tmp/castlumora-preview-fresh/data', '/tmp/castlumora-profile-access-local/data'].includes(
      info.ProgramDataPath,
    )
  )
    throw new Error('Refusing an unrecognized server data directory');
  const canonical = (id: string) => id.replaceAll('-', '').toLowerCase();
  const original = await (
    await readPreview(request, endpoint('Users/' + profileId), { headers })
  ).json();
  expect(canonical(original.Id)).toBe(canonical(profileId));
  const managed = await (
    await readPreview(request, endpoint('Castlumora/Profiles/Managed'), { headers })
  ).json();
  const preset = managed.profiles.find(
    (profile: { id: string }) => canonical(profile.id) === canonical(profileId),
  ).preset;
  const originalFolders = original.Policy.EnabledFolders as string[];
  expect(originalFolders.length).toBeGreaterThan(0);
  const signIn = async () => {
    const header = `MediaBrowser Client="Access browser fixture", Device="Profile access", DeviceId="${randomUUID()}", Version="1"`;
    try {
      const login = await request.post(endpoint('Users/AuthenticateByName'), {
        headers: { Authorization: header },
        data: { Username: owner.user.Name, Pw: password },
      });
      expect(login.ok()).toBe(true);
      const switched = await request.post(endpoint('Castlumora/Profiles/Switch'), {
        headers: { Authorization: header + `, Token="${(await login.json()).AccessToken}"` },
        data: { profileId, accountPassword: password },
      });
      expect(switched.ok()).toBe(true);
      return { Authorization: header + `, Token="${(await switched.json()).AccessToken}"` };
    } catch {
      throw new Error('Profile access sign-in fixture failed; mutation was not retried.');
    }
  };
  const oldChild = await signIn();
  expect((await readPreview(request, childItemUrl, { headers: oldChild })).ok()).toBe(true);
  const directory = mkdtempSync(join(tmpdir(), 'castlumora-profile-access-'));
  const name = 'Access fixture ' + randomUUID();
  let restored = false;
  try {
    const added = await request.post(
      endpoint(
        'Library/VirtualFolders?name=' +
          encodeURIComponent(name) +
          '&collectionType=homevideos&paths=' +
          encodeURIComponent(directory),
      ),
      { headers, data: {} },
    );
    expect(added.ok()).toBe(true);
    const folders = (await (
      await readPreview(request, endpoint('Library/VirtualFolders'), { headers })
    ).json()) as { Name: string; ItemId: string }[];
    const addedId = folders.find((folder) => folder.Name === name)!.ItemId;
    const edit = async (nextPreset: string, libraryIds: string[], cancelOnce = false) => {
      await page
        .getByRole('button', { name: 'Edit access for Packaged Family Updated', exact: true })
        .click();
      const form = page.getByRole('form', {
        name: 'Access settings for Packaged Family Updated',
        exact: true,
      });
      await expect(form.getByRole('checkbox', { name, exact: true })).toBeVisible();
      for (const checkbox of await form.getByRole('checkbox').all()) await checkbox.uncheck();
      await expect(
        form.getByRole('button', { name: 'Apply profile access', exact: true }),
      ).toBeDisabled();
      for (const id of libraryIds) {
        const folder = folders.find((folder) => canonical(folder.ItemId) === canonical(id));
        if (!folder) throw new Error('Owned library fixture missing');
        await form.getByRole('checkbox', { name: folder.Name, exact: true }).check();
      }
      await form
        .getByRole('combobox', { name: 'Updated profile preset', exact: true })
        .selectOption(nextPreset, { timeout: 10000 });
      if (cancelOnce) {
        page.once('dialog', (dialog) => dialog.dismiss());
        await form.getByRole('button', { name: 'Apply profile access', exact: true }).click();
        expect(
          (await readPreview(request, endpoint('Users/Me'), { headers: oldChild })).status(),
        ).toBe(200);
      }
      const applied = page.waitForResponse(
        (response) => {
          const path = new URL(response.url()).pathname;
          return (
            path.includes('/Castlumora/Profiles/Managed/') &&
            path.endsWith('/Access') &&
            canonical(path.split('/').at(-2) || '') === canonical(profileId) &&
            response.request().method() === 'PUT'
          );
        },
        { timeout: 15000 },
      );
      page.once('dialog', (dialog) => dialog.accept());
      await form.getByRole('button', { name: 'Apply profile access', exact: true }).click();
      expect((await applied).status()).toBe(204);
      await expect(
        page.getByText(
          'Profile access updated. Devices signed out; identity and viewing history retained.',
          { exact: true },
        ),
      ).toBeVisible();
      await expect(
        page.getByRole('button', { name: 'Edit access for Packaged Family Updated', exact: true }),
      ).toBeVisible();
    };
    await page.reload();
    await edit('kids', [addedId], true);
    expect((await readPreview(request, endpoint('Users/Me'), { headers: oldChild })).status()).toBe(
      401,
    );
    expect((await readPreview(request, endpoint('Users/Me'), { headers })).status()).toBe(200);
    const restricted = await (
      await readPreview(request, endpoint('Users/' + profileId), { headers })
    ).json();
    expect(restricted.Policy.IsAdministrator).toBe(false);
    expect(restricted.Policy.MaxParentalRating).toBe(6);
    expect((restricted.Policy.EnabledFolders as string[]).map(canonical)).toEqual([
      canonical(addedId),
    ]);
    const newChild = await signIn();
    expect((await readPreview(request, endpoint('Users/Me'), { headers: newChild })).status()).toBe(
      200,
    );
    expect((await readPreview(request, childItemUrl, { headers: newChild })).status()).toBe(404);
    await edit(preset, originalFolders);
    restored = true;
    expect((await readPreview(request, endpoint('Users/Me'), { headers: newChild })).status()).toBe(
      401,
    );
    const after = await (
      await readPreview(request, endpoint('Users/' + profileId), { headers })
    ).json();
    expect(canonical(after.Id)).toBe(canonical(profileId));
    expect(after.Configuration.AudioLanguagePreference).toBe(
      original.Configuration.AudioLanguagePreference,
    );
    expect(after.Configuration.SubtitleLanguagePreference).toBe(
      original.Configuration.SubtitleLanguagePreference,
    );
    const finalChild = await signIn();
    expect((await readPreview(request, childItemUrl, { headers: finalChild })).status()).toBe(200);
  } finally {
    // Failed updates keep their owned fixture/plan available for diagnosis, rather than deleting recovery inputs.
    if (restored) {
      expect(
        (
          await request.delete(
            endpoint(
              'Library/VirtualFolders?name=' + encodeURIComponent(name) + '&refreshLibrary=false',
            ),
            { headers },
          )
        ).ok(),
      ).toBe(true);
      rmSync(directory, { recursive: true });
    }
  }
}
