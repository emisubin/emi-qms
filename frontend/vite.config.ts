import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import react from '@vitejs/plugin-react';
import type { Plugin } from 'vite';
import { defineConfig } from 'vitest/config';

const configDir = path.dirname(fileURLToPath(import.meta.url));
const hmrHost = process.env.VITE_HMR_HOST;
const hmrClientPort = process.env.VITE_HMR_CLIENT_PORT
  ? Number(process.env.VITE_HMR_CLIENT_PORT)
  : undefined;
const devServerPort = Number(process.env.VITE_DEV_SERVER_PORT ?? '5173');
const proxyTarget = process.env.VITE_DEV_PROXY_TARGET ?? 'http://localhost:5080';
const localEntraEnvPath = path.resolve(configDir, '../.env.entra-local');
const loopbackHost = '127.0.0.1';
const allowedDevelopmentFileRoots = [
  configDir,
  path.resolve(configDir, '../node_modules')
];

function isLoopbackHost(host: string | boolean | undefined) {
  return host === loopbackHost || host === 'localhost' || host === '::1';
}

function loopbackOnlyDevelopmentServer(): Plugin {
  return {
    name: 'emi-loopback-only-development-server',
    configResolved(config) {
      if (!isLoopbackHost(config.server.host)) {
        throw new Error(
          'EMI Development server must listen on a loopback host. Use 127.0.0.1, localhost, or ::1.'
        );
      }

      if (!isLoopbackHost(config.preview.host)) {
        throw new Error(
          'EMI preview server must listen on a loopback host. Use 127.0.0.1, localhost, or ::1.'
        );
      }
    }
  };
}

if (!Number.isInteger(devServerPort) || devServerPort < 1 || devServerPort > 65535) {
  throw new Error('VITE_DEV_SERVER_PORT must be a valid TCP port.');
}

function isEnabled(value: string | undefined) {
  return ['1', 'true', 'yes', 'on'].includes((value ?? '').trim().toLowerCase());
}

function resolveConfigPath(value: string | undefined, fallback: string) {
  const candidate = (value?.trim() || fallback).trim();
  return path.isAbsolute(candidate) ? candidate : path.resolve(configDir, candidate);
}

function loadLocalEntraFrontendConfig() {
  if (devServerPort !== 5174 || !fs.existsSync(localEntraEnvPath)) {
    return {};
  }

  const allowedKeys = new Set([
    'VITE_AUTH_MODE',
    'VITE_AZURE_TENANT_ID',
    'VITE_AZURE_CLIENT_ID',
    'VITE_AZURE_API_SCOPE',
    'VITE_AZURE_REDIRECT_URI'
  ]);
  const values: Record<string, string> = {};

  for (const rawLine of fs.readFileSync(localEntraEnvPath, 'utf8').split(/\r?\n/u)) {
    const line = rawLine.trim();
    if (!line || line.startsWith('#') || !line.includes('=')) {
      continue;
    }

    const separatorIndex = line.indexOf('=');
    const key = line.slice(0, separatorIndex).trim();
    if (!allowedKeys.has(key)) {
      continue;
    }

    let value = line.slice(separatorIndex + 1).trim();
    if (
      value.length >= 2
      && ((value.startsWith('"') && value.endsWith('"'))
        || (value.startsWith("'") && value.endsWith("'")))
    ) {
      value = value.slice(1, -1);
    }
    values[key] = value;
  }

  const requiredKeys = [
    'VITE_AZURE_TENANT_ID',
    'VITE_AZURE_CLIENT_ID',
    'VITE_AZURE_API_SCOPE',
    'VITE_AZURE_REDIRECT_URI'
  ];
  if (requiredKeys.some((key) => !values[key])) {
    throw new Error('The local Entra frontend environment is incomplete.');
  }

  const redirectUri = new URL(values.VITE_AZURE_REDIRECT_URI);
  const expectedRedirectOrigin = `https://localhost:${devServerPort}`;
  if (redirectUri.origin !== expectedRedirectOrigin || redirectUri.pathname !== '/') {
    throw new Error(
      `VITE_AZURE_REDIRECT_URI must exactly match the registered SPA redirect URI: ${expectedRedirectOrigin}`
    );
  }

  return values;
}

function loadHttpsOptions() {
  if (!isEnabled(process.env.VITE_DEV_HTTPS)) {
    return undefined;
  }

  const certPath = resolveConfigPath(process.env.VITE_DEV_HTTPS_CERT, '../.certs/localhost.pem');
  const keyPath = resolveConfigPath(process.env.VITE_DEV_HTTPS_KEY, '../.certs/localhost-key.pem');
  const missingPaths = [
    fs.existsSync(certPath) ? null : certPath,
    fs.existsSync(keyPath) ? null : keyPath
  ].filter(Boolean);

  if (missingPaths.length > 0) {
    throw new Error(
      [
        'VITE_DEV_HTTPS=true requires local HTTPS certificate files.',
        'Create them with:',
        '  brew install mkcert',
        '  mkcert -install',
        '  mkdir -p .certs',
        '  mkcert -key-file .certs/localhost-key.pem -cert-file .certs/localhost.pem localhost 127.0.0.1 ::1',
        `Missing file(s): ${missingPaths.join(', ')}`
      ].join('\n')
    );
  }

  return {
    cert: fs.readFileSync(certPath),
    key: fs.readFileSync(keyPath)
  };
}

const localEntraFrontendConfig = loadLocalEntraFrontendConfig();
const useLocalEntraFrontend = Object.keys(localEntraFrontendConfig).length > 0;
const runtimeDefine = useLocalEntraFrontend
  ? Object.fromEntries([
      ...Object.entries(localEntraFrontendConfig).map(([key, value]) => [
        `import.meta.env.${key}`,
        JSON.stringify(value)
      ]),
      ['import.meta.env.VITE_API_BASE_URL', JSON.stringify('')]
    ])
  : undefined;

export default defineConfig({
  define: runtimeDefine,
  plugins: [loopbackOnlyDevelopmentServer(), react()],
  server: {
    host: loopbackHost,
    port: devServerPort,
    strictPort: true,
    allowedHosts: ['localhost', loopbackHost],
    fs: {
      strict: true,
      allow: allowedDevelopmentFileRoots
    },
    https: loadHttpsOptions(),
    proxy: {
      '/api': {
        target: useLocalEntraFrontend ? 'http://127.0.0.1:5084' : proxyTarget,
        changeOrigin: true
      },
      '/health': {
        target: useLocalEntraFrontend ? 'http://127.0.0.1:5084' : proxyTarget,
        changeOrigin: true
      }
    },
    hmr: hmrHost
      ? {
          host: hmrHost,
          clientPort: hmrClientPort
        }
      : undefined
  },
  preview: {
    host: loopbackHost,
    allowedHosts: ['localhost', loopbackHost],
    strictPort: true
  },
  test: {
    environment: 'jsdom',
    globals: true,
    testTimeout: 10_000,
    include: ['tests/**/*.{test,spec}.{ts,tsx}'],
    setupFiles: './tests/setup.ts'
  }
});
