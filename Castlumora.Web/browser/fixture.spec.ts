// Isolated API fixture tests. These do not claim real-server or tuner verification.
import { test, expect } from '@playwright/test';
import { readFileSync, existsSync } from 'node:fs';
import { defaultLayout } from '../src/config/schema';
const id = '11111111111111111111111111111111';
const user = {
  Id: '22222222222222222222222222222222',
  Name: 'Fixture viewer',
  Policy: { IsAdministrator: true },
};
const title = {
  Id: id,
  Name: 'The Violet Horizon',
  Type: 'Movie',
  Overview:
    'A quiet journey into an extraordinary world. Browser test fixture, not a production catalog.',
  ProductionYear: 2026,
  Genres: ['Adventure'],
  RunTimeTicks: 240000000,
  UserData: { PlaybackPositionTicks: 0 },
  MediaSources: [],
};
test(
  'login, browse, details, direct playback, watchlist and server-backed editor',
  { tag: '@fixture' },
  async ({ page }) => {
    let draft = structuredClone(defaultLayout);
    let published = structuredClone(defaultLayout);
    const history: (typeof defaultLayout)[] = [];
    let prefs = { watchlist: [] as string[], hiddenResume: [] as string[] };
    const reports: string[] = [];
    const fixture = '/tmp/castlumora-web-fixtures/test.mp4';
    test.skip(!existsSync(fixture), 'Generate the documented local MP4 fixture first.');
    await page.route('http://127.0.0.1:8096/**', async (route) => {
      const url = new URL(route.request().url());
      const path = url.pathname;
      const method = route.request().method();
      const json = (body: unknown) =>
        route.fulfill({
          json: body,
          headers: {
            'Access-Control-Allow-Origin': '*',
            'Access-Control-Allow-Headers': '*',
            'Access-Control-Allow-Methods': '*',
          },
        });
      if (method === 'OPTIONS')
        return route.fulfill({
          status: 204,
          headers: {
            'Access-Control-Allow-Origin': '*',
            'Access-Control-Allow-Headers': '*',
            'Access-Control-Allow-Methods': '*',
          },
        });
      if (path === '/System/Info/Public')
        return json({ ServerName: 'Isolated browser fixture', StartupWizardCompleted: true });
      if (path === '/Users/Public') return json([user]);
      if (path === '/Users/AuthenticateByName')
        return json({ AccessToken: 'fixture-only-token', User: user });
      if (path === '/Users/Me') return json(user);
      if (path === '/Users') return json([user]);
      if (path === '/System/Info')
        return json({ ServerName: 'Fixture castle', Version: '1.0-dev' });
      if (path === '/Sessions') return json([]);
      if (path === '/Sessions/Logout') return route.fulfill({ status: 204 });
      if (path === '/Castlumora/Presentation') return json(published);
      if (path === '/Castlumora/Presentation/Draft') {
        if (method === 'PUT')
          draft = { ...route.request().postDataJSON(), revision: draft.revision + 1 };
        return json(draft);
      }
      if (path === '/Castlumora/Presentation/History')
        return json(
          history.map((config) => ({
            revision: config.revision,
            publishedAt: '2026-11-09T00:00:00Z',
          })),
        );
      if (path.includes('/Castlumora/Presentation/History/') && path.endsWith('/Restore')) {
        const revision = Number(path.split('/')[4]);
        const config = history.find((h) => h.revision === revision);
        if (!config) return route.fulfill({ status: 404 });
        draft = { ...structuredClone(config), revision: draft.revision + 1 };
        return json(draft);
      }
      if (path === '/Castlumora/Presentation/Publish') {
        published = { ...draft, revision: draft.revision + 1 };
        draft = structuredClone(published);
        history.push(structuredClone(published));
        return json(draft);
      }
      if (path === '/Castlumora/Presentation/Preferences') return json(prefs);
      if (path.includes('/Preferences/watchlist/')) {
        prefs = { ...prefs, watchlist: url.searchParams.get('enabled') === 'true' ? [id] : [] };
        return json(prefs);
      }
      if (path === '/Items') return json({ Items: [title], TotalRecordCount: 1 });
      if (path.includes('/Views')) return json({ Items: [], TotalRecordCount: 0 });
      if (path === '/UserItems/Resume' || path === '/Shows/NextUp')
        return json({ Items: [], TotalRecordCount: 0 });
      if (path === `/Users/${user.Id}/Items/${id}`) return json(title);
      if (path.endsWith('/PlaybackInfo'))
        return json({
          PlaySessionId: 'fixture-play-session',
          MediaSources: [
            {
              Id: id,
              Container: 'mp4',
              SupportsDirectPlay: true,
              MediaStreams: [{ Type: 'Audio', Index: 1, DisplayTitle: 'English' }],
            },
          ],
        });
      if (path === `/Videos/${id}/stream`)
        return route.fulfill({
          body: readFileSync(fixture),
          contentType: 'video/mp4',
          headers: { 'Access-Control-Allow-Origin': '*' },
        });
      if (path.startsWith('/Sessions/Playing')) {
        reports.push(path);
        return route.fulfill({ status: 204 });
      }
      if (path === '/Videos/ActiveEncodings') return route.fulfill({ status: 204 });
      return route.fulfill({ status: 404, json: { message: 'Fixture route not implemented' } });
    });
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Connect to your castle' })).toBeVisible();
    await page.getByLabel('Server address').fill('http://127.0.0.1:8096');
    await page.getByRole('button', { name: 'Connect', exact: true }).click();
    await page.getByLabel('Username').fill(user.Name);
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await expect(page.getByRole('heading', { name: title.Name, exact: true })).toBeVisible();
    await page.screenshot({ path: '../docs/screenshots/web-home-fixture.png', fullPage: true });
    await page.getByRole('link', { name: 'Browse', exact: true }).click();
    await page.getByRole('link', { name: 'Details for ' + title.Name }).click();
    await expect(page.getByRole('heading', { name: title.Name, exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Add to My List', exact: true }).click();
    await expect(page.getByRole('button', { name: 'In My List', exact: true })).toBeVisible();
    await page.getByRole('link', { name: 'Watch now', exact: true }).click();
    await expect
      .poll(() => page.locator('video').evaluate((v: HTMLVideoElement) => v.currentTime), {
        timeout: 15000,
      })
      .toBeGreaterThan(0);
    await page.locator('video').evaluate((v: HTMLVideoElement) => v.pause());
    await expect.poll(() => reports.includes('/Sessions/Playing/Progress')).toBe(true);
    await page.getByRole('link', { name: 'Back', exact: true }).click();
    await expect.poll(() => reports.includes('/Sessions/Playing/Stopped')).toBe(true);
    await page.getByRole('link', { name: 'Administration', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Your castle. Your rules.' })).toBeVisible();
    await page.getByLabel('home navigation label').fill('My home');
    await page.getByRole('button', { name: 'Save draft', exact: true }).click();
    await expect(page.getByText('Draft saved on the server.', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Publish', exact: true }).click();
    await expect(page.getByRole('link', { name: 'My home', exact: true })).toBeVisible();
    await page.getByLabel('home navigation label').fill('New home');
    await page.getByRole('button', { name: 'Save draft', exact: true }).click();
    await expect(page.getByText('Draft saved on the server.', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Publish', exact: true }).click();
    await expect(page.getByRole('link', { name: 'New home', exact: true })).toBeVisible();
    await page.getByRole('combobox', { name: 'Published revision', exact: true }).selectOption('2');
    await page.getByRole('button', { name: 'Restore to draft', exact: true }).click();
    await expect(
      page.getByText('Revision restored to draft. Review and publish when ready.', { exact: true }),
    ).toBeVisible();
    await expect(page.getByRole('link', { name: 'New home', exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Publish', exact: true }).click();
    await expect(page.getByRole('link', { name: 'My home', exact: true })).toBeVisible();

    await page.setViewportSize({ width: 390, height: 844 });
    await page.screenshot({
      path: '../docs/screenshots/web-editor-mobile-fixture.png',
      fullPage: true,
    });
  },
);
test('mobile login has no horizontal overflow', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Connect to your castle' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: '../docs/screenshots/web-login-mobile.png', fullPage: true });
});
