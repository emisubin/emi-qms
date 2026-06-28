import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

const hmrHost = process.env.VITE_HMR_HOST;
const hmrClientPort = process.env.VITE_HMR_CLIENT_PORT
  ? Number(process.env.VITE_HMR_CLIENT_PORT)
  : undefined;

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
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
