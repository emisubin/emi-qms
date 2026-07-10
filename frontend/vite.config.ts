import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

const configDir = path.dirname(fileURLToPath(import.meta.url));
const hmrHost = process.env.VITE_HMR_HOST;
const hmrClientPort = process.env.VITE_HMR_CLIENT_PORT
  ? Number(process.env.VITE_HMR_CLIENT_PORT)
  : undefined;
const devServerPort = Number(process.env.VITE_DEV_SERVER_PORT ?? '5173');
const proxyTarget = process.env.VITE_DEV_PROXY_TARGET ?? 'http://localhost:5080';

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

export default defineConfig({
  plugins: [react()],
  server: {
    port: devServerPort,
    strictPort: true,
    https: loadHttpsOptions(),
    proxy: {
      '/api': {
        target: proxyTarget,
        changeOrigin: true
      },
      '/health': {
        target: proxyTarget,
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
  test: {
    environment: 'jsdom',
    globals: true,
    include: ['tests/**/*.{test,spec}.{ts,tsx}'],
    setupFiles: './tests/setup.ts'
  }
});
