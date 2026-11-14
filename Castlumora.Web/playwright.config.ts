import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './browser',
  workers: 1,
  timeout: 45000,
  use: {
    baseURL: process.env.CASTLUMORA_BROWSER_URL ?? 'http://127.0.0.1:5173',
    headless: true,
    launchOptions: {
      executablePath: process.env.CHROMIUM_PATH ?? '/usr/bin/chromium',
      args: ['--no-sandbox'],
    },
    screenshot: 'only-on-failure',
  },
  reporter: 'list',
});
