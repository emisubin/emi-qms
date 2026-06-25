import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e/full-stack',
  fullyParallel: false,
  workers: 1,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: 'http://127.0.0.1:5174',
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
      url: 'http://127.0.0.1:5081/health/ready',
      reuseExistingServer: false,
      timeout: 180_000
    },
    {
      command: 'VITE_API_BASE_URL=http://127.0.0.1:5081 VITE_DEV_USER_KEY=dev-sales corepack pnpm exec vite --host 127.0.0.1 --port 5174',
      url: 'http://127.0.0.1:5174',
      reuseExistingServer: false,
      timeout: 120_000
    }
  ]
});
