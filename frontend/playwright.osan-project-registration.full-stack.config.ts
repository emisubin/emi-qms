import { defineConfig, devices } from '@playwright/test';

const backendPort = process.env.E2E_BACKEND_PORT ?? '5082';
const frontendPort = process.env.E2E_FRONTEND_PORT ?? '5175';
const backendUrl = `http://127.0.0.1:${backendPort}`;
const frontendUrl = `http://127.0.0.1:${frontendPort}`;

export default defineConfig({
  timeout: 120_000,
  testDir: './e2e/full-stack',
  testMatch: 'osan-project-registration.full-stack.spec.ts',
  outputDir: 'test-results/osan-project-registration-full-stack',
  fullyParallel: false,
  workers: 1,
  reporter: [['list']],
  use: {
    baseURL: frontendUrl,
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
    command: `VITE_AUTH_MODE=Dev VITE_API_BASE_URL=${backendUrl} VITE_DEV_USER_KEY=dev-admin corepack pnpm run build && corepack pnpm exec vite preview --host 127.0.0.1 --port ${frontendPort}`,
    url: frontendUrl,
    reuseExistingServer: false,
    timeout: 120_000
  }
});
