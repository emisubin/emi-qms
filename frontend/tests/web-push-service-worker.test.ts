/// <reference types="node" />

import { readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

const workerSource = readFileSync(path.resolve(process.cwd(), 'public/web-push-service-worker.js'), 'utf8');

describe('PWA push service worker', () => {
  it('handles only visible push and notification clicks', () => {
    expect(workerSource).toContain("addEventListener('push'");
    expect(workerSource).toContain("addEventListener('notificationclick'");
    expect(workerSource).not.toContain("addEventListener('fetch'");
    expect(workerSource).not.toContain("addEventListener('sync'");
  });

  it('opens the privacy-safe notification destination without embedding a subscription secret', () => {
    expect(workerSource).toContain("event.notification.data?.url");
    expect(workerSource).toContain("'/notifications'");
    expect(workerSource).not.toContain('p256dh');
    expect(workerSource).not.toContain('subscription-secret');
  });
});
