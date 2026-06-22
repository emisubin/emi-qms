import { render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from '../src/App';

describe('App', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows API and database health returned by the backend', async () => {
    vi.mocked(fetch)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          name: 'ready',
          status: 'ok',
          database: {
            isReady: true,
            reason: 'reachable'
          },
          checkedAtUtc: '2026-06-22T00:00:00Z'
        })
      } as Response)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          developmentUserKey: 'dev-admin',
          displayName: 'Dev System Administrator',
          department: 'administration',
          roles: ['system-administrator'],
          permissions: ['projects.read', 'users.manage'],
          projectAccess: []
        })
      } as Response);

    render(<App />);

    await waitFor(() => expect(screen.getByText('reachable')).toBeInTheDocument());
    expect(screen.getByText('ok')).toBeInTheDocument();
    expect(screen.getByText('dev-admin')).toBeInTheDocument();
    expect(screen.getByText('사용자 관리')).toBeInTheDocument();
  });
});
