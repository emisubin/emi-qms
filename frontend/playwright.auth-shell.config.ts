import { defineConfig, devices } from '@playwright/test';

const frontendPort = Number(process.env.AUTH_SHELL_PORT ?? 5187);
const reuseExistingServer = process.env.AUTH_SHELL_REUSE_EXISTING === 'true';
const frontendUrl = `http://127.0.0.1:${frontendPort}`;

if (!Number.isInteger(frontendPort) || frontendPort < 1024 || frontendPort > 65535) {
  throw new Error('AUTH_SHELL_PORT must be a valid non-privileged TCP port.');
}

export default defineConfig({
  testDir: './e2e/auth-shell',
  fullyParallel: false,
  reporter: 'list',
  use: {
    baseURL: frontendUrl,
    trace: 'off'
  },
  projects: [
    {
      name: 'desktop-1920',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1920, height: 1080 }
      }
    },
    {
      name: 'desktop-1440',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1440, height: 810 }
      }
    },
    {
      name: 'desktop-1280',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1280, height: 720 }
      }
    },
    {
      name: 'desktop-1024',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1024, height: 768 }
      }
    },
    {
      name: 'desktop-short-window',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1440, height: 600 }
      }
    },
    {
      name: 'desktop-narrow-window',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 651, height: 708 }
      }
    }
  ],
  webServer: {
    command: [
      `VITE_DEV_SERVER_PORT=${frontendPort}`,
      'VITE_AUTH_MODE=EntraId',
      'VITE_AZURE_TENANT_ID=synthetic-tenant',
      'VITE_AZURE_CLIENT_ID=synthetic-client',
      'VITE_AZURE_API_SCOPE=api://synthetic/access',
      `corepack pnpm exec vite --host 127.0.0.1 --port ${frontendPort} --strictPort`
    ].join(' '),
    url: frontendUrl,
    reuseExistingServer,
    timeout: 120_000
  }
});
