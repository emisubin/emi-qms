import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e/mock-ui',
  testMatch: 'osan-project-registration.spec.ts',
  outputDir: 'test-results/osan-project-registration-mock',
  fullyParallel: false,
  workers: 1,
  reporter: [['list']],
  use: {
    baseURL: 'http://127.0.0.1:5173',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure'
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ],
  webServer: {
    command: 'VITE_AUTH_MODE=Dev corepack pnpm run build && corepack pnpm exec vite preview --host 127.0.0.1 --port 5173',
    url: 'http://127.0.0.1:5173',
    reuseExistingServer: false,
    timeout: 120_000
  }
});
