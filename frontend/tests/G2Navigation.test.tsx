import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from '../src/App';

function json(body: unknown, status = 200) { return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }); }

const principal = { userId: '50000000-0000-0000-0000-000000000002', developmentUserKey: 'dev-sales', displayName: 'Dev Sales User', email: null, authProvider: 'Dev', isActive: true, approvalPending: false, department: 'sales', departmentName: '영업', profilePhotoVersion: null, roles: ['sales'] };

describe('G2 navigation', () => {
  beforeEach(() => {
    window.localStorage.clear(); window.history.pushState(null, '', '/g2');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input)).pathname;
      if (path === '/health/ready') return json({ name: 'ready', status: 'ok', database: { isReady: true, reason: 'reachable' }, checkedAtUtc: '2026-08-18T00:00:00Z' });
      if (path === '/api/runtime-mode') return json({ mode: 'Development', reviewSafe: false, mutationAllowed: true, backgroundWorkersEnabled: true, externalProvidersEnabled: true, databaseReadOnly: false, migrationExecutionEnabled: true, environment: 'Development', ready: true, reason: 'not_applicable', expectedMigration: null, actualMigration: null, migrationLedgerStatus: 'Exact', expectedMigrationCount: 0, actualMigrationCount: 0, missingMigrations: [], unexpectedMigrations: [], approvedLegacyMigrations: [], migrationSchemaCompatible: true, migrationLedgerReady: true });
      if (path === '/api/me') return json({ ...principal, permissions: ['projects.read', 'G2.Read', 'G2.Production.Update', 'G2.Delivery.Update', 'G2.Attendance.Update', 'G2.Inventory.Manage', 'G2.Target.Manage'], projectAccess: [], isTestUserSwitch: false, testUserKey: null, canUseAdminTestUserSwitch: false, actualUser: principal, effectiveUser: principal });
      if (path === '/api/g2/home') return json({ today: '2026-08-18', year: 2026, month: 8, hasInventoryBaseline: false, days: [] });
      if (path === '/api/g2/days') return json({ today: '2026-08-18', from: '2026-08-01', to: '2026-08-31', days: [] });
      return json({ title: 'not found' }, 404);
    }));
  });
  afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });

  it('opens the G2 home deep link and exposes only the three approved children', async () => {
    render(<App />);
    expect(await screen.findByRole('heading', { name: 'G2 홈' })).toBeInTheDocument();
    const navigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    const parent = within(navigation).getByRole('button', { name: 'G2' });
    fireEvent.click(parent);
    expect(within(navigation).getAllByRole('button', { name: '홈' })).toHaveLength(2);
    expect(within(navigation).getByRole('button', { name: '생산/출하 관리' })).toBeInTheDocument();
    expect(within(navigation).getByRole('button', { name: '제조 인원 출근 관리' })).toBeInTheDocument();
    expect(within(navigation).queryByText('손익관리')).toBeNull();
    fireEvent.click(within(navigation).getByRole('button', { name: '생산/출하 관리' }));
    await waitFor(() => expect(window.location.pathname).toBe('/g2/operations'));
    expect(await screen.findByRole('heading', { name: '생산/출하 관리' })).toBeInTheDocument();
  });
});
