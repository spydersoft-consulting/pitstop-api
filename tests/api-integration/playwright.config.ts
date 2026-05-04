import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: 'html',
  use: {
    baseURL: process.env.PITSTOP_BASE_URL ?? 'http://localhost:8080',
    extraHTTPHeaders: {
      Authorization: `Bearer ${process.env.PITSTOP_TEST_TOKEN ?? ''}`,
    },
  },
});
