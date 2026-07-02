import { defineConfig, devices } from '@playwright/test';

const backendPort = process.env.E2E_BACKEND_PORT ?? '5082';
const frontendPort = process.env.E2E_FRONTEND_PORT ?? '5175';
const backendUrl = `http://127.0.0.1:${backendPort}`;
const frontendUrl = `http://127.0.0.1:${frontendPort}`;

export default defineConfig({
  testDir: './e2e/full-stack',
  fullyParallel: false,
  workers: 1,
  reporter: [['list'], ['html', { open: 'never' }]],
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
  webServer: [
    {
      command: 'bash scripts/e2e-backend-server.sh',
      cwd: '..',
      url: `${backendUrl}/health/ready`,
      reuseExistingServer: false,
      timeout: 180_000
    },
    {
      command: `VITE_AUTH_MODE=Dev VITE_API_BASE_URL=${backendUrl} VITE_DEV_USER_KEY=dev-sales corepack pnpm exec vite --host 127.0.0.1 --port ${frontendPort}`,
      url: frontendUrl,
      reuseExistingServer: false,
      timeout: 120_000
    }
  ]
});
