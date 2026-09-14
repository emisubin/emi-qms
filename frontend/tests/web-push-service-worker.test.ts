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

async function runtime() {
  const { default: vm } = await import('node:vm');
  const { vi } = await import('vitest');
  const handlers: Record<string, (event: unknown) => void> = {};
  const client = { id: 'app', url: 'https://pms.test/', navigate: vi.fn(), focus: vi.fn() };
  client.navigate.mockResolvedValue(client); client.focus.mockResolvedValue(client);
  const clients = { matchAll: vi.fn().mockResolvedValue([]), openWindow: vi.fn().mockResolvedValue(client) };
  vm.runInNewContext(workerSource, { URL, self: { location: { origin: 'https://pms.test' }, clients,
    addEventListener: (name: string, handler: (event: unknown) => void) => { handlers[name] = handler; } } });
  const click = (url: string) => {
    let pending!: Promise<unknown>;
    handlers.notificationclick({ notification: { close() {}, data: { url } }, waitUntil(p: Promise<unknown>) { pending = p; } });
    return pending;
  };
  return { client, clients, click, vi };
}
describe('notification window reuse', () => {
  it('requests existing-window navigation for app launches', () => {
    const manifest = JSON.parse(readFileSync(path.resolve(process.cwd(), 'public/manifest.webmanifest'), 'utf8'));
    expect(manifest.launch_handler.client_mode).toBe('navigate-existing');
    expect(manifest.id).toBe('/'); expect(manifest.scope).toBe('/');
  });
  it('navigates and focuses an existing window', async () => {
    const h = await runtime(); h.clients.matchAll.mockResolvedValue([h.client]);
    await h.click('/progress?projectId=demo');
    expect(h.client.navigate).toHaveBeenCalledWith('https://pms.test/progress?projectId=demo');
    expect(h.client.focus).toHaveBeenCalledTimes(1); expect(h.clients.openWindow).not.toHaveBeenCalled();
  });
  it('serializes overlapping clicks while a new window is not yet enumerated', async () => {
    const h = await runtime(); let resolve!: (client: typeof h.client) => void;
    h.clients.openWindow.mockImplementationOnce(() => new Promise(r => { resolve = r; }));
    const first = h.click('/projects/one'); const second = h.click('/projects/two');
    await h.vi.waitFor(() => expect(h.clients.openWindow).toHaveBeenCalledTimes(1));
    resolve(h.client); await Promise.all([first, second]);
    expect(h.clients.openWindow).toHaveBeenCalledTimes(1);
    expect(h.client.navigate).toHaveBeenCalledWith('https://pms.test/projects/two');
  });
  it('recovers from a failed launch and a closed client', async () => {
    const h = await runtime(); h.clients.openWindow.mockRejectedValueOnce(new Error('blocked'));
    await expect(h.click('/one')).rejects.toThrow('blocked');
    h.clients.matchAll.mockResolvedValue([h.client]); h.client.navigate.mockRejectedValue(new Error('closed'));
    await h.click('/two'); expect(h.clients.openWindow).toHaveBeenLastCalledWith('https://pms.test/two');
  });
  it('keeps invalid and external notification destinations on PMS', async () => {
    for (const url of ['https://other.test/path', 'javascript:alert(1)', 'http://[']) {
      const h = await runtime(); await h.click(url);
      expect(h.clients.openWindow).toHaveBeenCalledWith('https://pms.test/notifications');
    }
  });
});
