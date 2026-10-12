import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from '../src/App';

function json(body: unknown, status = 200) { return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }); }

let administrator = false;
let g2OnlyForNonAdministrators = true;
const principal = { userId: '50000000-0000-0000-0000-000000000002', developmentUserKey: 'dev-sales', displayName: 'Dev Sales User', email: null, authProvider: 'Dev', isActive: true, approvalPending: false, department: 'sales', departmentName: '영업', profilePhotoVersion: null, roles: ['sales'] };

describe('G2 navigation', () => {
  beforeEach(() => {
    administrator = false;
    g2OnlyForNonAdministrators = true;
    window.localStorage.clear(); window.history.pushState(null, '', '/g2');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input)).pathname;
      if (path === '/health/ready') return json({ name: 'ready', status: 'ok', database: { isReady: true, reason: 'reachable' }, checkedAtUtc: '2026-08-18T00:00:00Z' });
      if (path === '/access/api/runtime-mode' || path === '/cheongju/api/runtime-mode') return json({ mode: 'Development', reviewSafe: false, mutationAllowed: true, backgroundWorkersEnabled: true, externalProvidersEnabled: true, databaseReadOnly: false, migrationExecutionEnabled: true, environment: 'Development', ready: true, reason: 'not_applicable', expectedMigration: null, actualMigration: null, migrationLedgerStatus: 'Exact', expectedMigrationCount: 0, actualMigrationCount: 0, missingMigrations: [], unexpectedMigrations: [], approvedLegacyMigrations: [], migrationSchemaCompatible: true, migrationLedgerReady: true });
      if (path === '/access/api/me' || path === '/cheongju/api/me') { const current = administrator ? {...principal,roles:['system-administrator']} : principal; return json({ ...current, permissions: ['projects.read', 'G2.Read', 'G2.Production.Update', 'G2.Delivery.Update', 'G2.Attendance.Update', 'G2.Inventory.Manage', 'G2.Target.Manage'], projectAccess: [], isTestUserSwitch: false, testUserKey: null, canUseAdminTestUserSwitch: false, actualUser: current, effectiveUser: current, businessUnitAccess: {status:'selected',selectedBusinessUnit:'CHEONGJU',allowedBusinessUnits:['CHEONGJU'],isOverallAdministrator:false,errorCode:null,g2OnlyForNonAdministrators} }); }
      if (path === '/cheongju/api/system/holidays') return json([]);
      if (path === '/cheongju/api/g2/materials/latest') return json(null);
      if (path === '/cheongju/api/g2/materials') return json({date:'2026-08-17',canEdit:false,items:[]});
      if (path === '/cheongju/api/g2/home') return json({ today: '2026-08-18', year: 2026, month: 8, hasInventoryBaseline: false, days: [] });
      if (path === '/cheongju/api/g2/days') return json({ today: '2026-08-18', from: '2026-08-01', to: '2026-08-31', days: [] });
      return json({ title: 'not found' }, 404);
    }));
  });
  afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });

  it('opens the G2 home deep link and exposes the approved children including materials', async () => {
    render(<App />);
    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'G2 홈' })).toBeInTheDocument();
    }, { timeout: 5_000 });
    const navigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    const parent = within(navigation).getByRole('button', { name: 'G2' });
    fireEvent.click(parent);
    expect(within(navigation).getAllByRole('button', { name: '홈' })).toHaveLength(2);
    expect(within(navigation).getByRole('button', { name: '생산/출하 관리' })).toBeInTheDocument();
    expect(within(navigation).getByRole('button', { name: '제조 인원 출근 관리' })).toBeInTheDocument();
    expect(within(navigation).getAllByRole('button', { name: '자재' })).toHaveLength(1);
    expect(within(navigation).queryByText('손익관리')).toBeNull();
    fireEvent.click(within(navigation).getByRole('button', { name: '생산/출하 관리' }));
    await waitFor(() => expect(window.location.pathname).toBe('/g2/operations'));
    expect(await screen.findByRole('heading', { name: '생산/출하 관리' })).toBeInTheDocument();
  });
  it.each([false, true])('uses G2 as Cheongju root home (administrator=%s)', async admin => {
    administrator = admin;
    window.history.replaceState(null, '', '/');
    render(<App />);
    expect(await screen.findByRole('heading', {name:'G2 홈'})).toBeInTheDocument();
    await waitFor(() => expect(window.location.pathname).toBe('/g2'));
    const nav = screen.getAllByRole('navigation', {name:'공통 메뉴'})[0];
    fireEvent.click(within(nav).getByRole('button', {name:'EMI PMS 로고로 홈 이동'}));
    await waitFor(() => expect(window.location.pathname).toBe('/g2'));
    expect(vi.mocked(fetch).mock.calls.some(([input]) => new URL(String(input)).pathname === '/cheongju/api/home')).toBe(false);
  });
  it('disables non-G2 desktop and mobile menus', async () => {
    vi.stubGlobal('matchMedia', vi.fn((query: string) => ({matches:query === '(max-width: 860px)',media:query,onchange:null,addEventListener:vi.fn(),removeEventListener:vi.fn(),addListener:vi.fn(),removeListener:vi.fn(),dispatchEvent:vi.fn()})));
    render(<App />);
    await screen.findByRole('heading', {name:'G2 홈'});
    const nav = screen.getAllByRole('navigation', {name:'공통 메뉴'})[0];
    const projects = within(nav).getByRole('button', {name:'프로젝트 준비중'});
    expect(projects).toBeDisabled();
    expect(within(nav).getByRole('button', {name:'생산관리 준비중'})).toBeDisabled();
    fireEvent.click(projects);
    expect(window.location.pathname).toBe('/g2');
    fireEvent.click(screen.getByRole('button', {name:'메뉴 열기'}));
    const mobile = screen.getByRole('navigation', {name:'빠른 이동'});
    expect(within(mobile).getByRole('button', {name:'프로젝트 준비중'})).toBeDisabled();
    fireEvent.click(within(mobile).getByRole('button', {name:'G2'}));
    expect(within(mobile).getByRole('button', {name:'생산/출하 관리'})).toBeEnabled();
  });
  it.each(['/projects','/admin','/my-work','/sales','/notifications'])('blocks ordinary-user deep link and history: %s', async path => {
    window.history.replaceState(null, '', path);
    render(<App />);
    expect(await screen.findByRole('heading', {name:'준비중'})).toBeInTheDocument();
    expect(vi.mocked(fetch).mock.calls.some(([input]) => new URL(String(input)).pathname === '/cheongju/api/projects')).toBe(false);
    fireEvent.click(screen.getByRole('button', {name:'G2 홈으로 이동'}));
    expect(await screen.findByRole('heading', {name:'G2 홈'})).toBeInTheDocument();
    window.history.pushState(null, '', '/projects'); fireEvent.popState(window);
    expect(await screen.findByRole('heading', {name:'준비중'})).toBeInTheDocument();
  });
  it('keeps existing non-G2 navigation available for local administrator', async () => {
    administrator = true; render(<App />);
    await screen.findByRole('heading', {name:'G2 홈'});
    const nav = screen.getAllByRole('navigation', {name:'공통 메뉴'})[0];
    expect(within(nav).getByRole('button', {name:'프로젝트'})).toBeEnabled();
    fireEvent.click(within(nav).getByRole('button', {name:'프로젝트'}));
    await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([input]) => new URL(String(input)).pathname === '/cheongju/api/projects')).toBe(true));
    expect(screen.queryByRole('heading', {name:'준비중'})).not.toBeInTheDocument();
  });

  it('preserves the legacy home and department navigation when the server policy is false', async () => {
    g2OnlyForNonAdministrators = false;
    window.history.replaceState(null, '', '/'); render(<App />);
    expect(await screen.findByRole('heading',{name:'업무 홈'})).toBeInTheDocument();
    expect(window.location.pathname).toBe('/');
    const nav = screen.getAllByRole('navigation',{name:'공통 메뉴'})[0];
    expect(within(nav).getByRole('button',{name:'프로젝트'})).toBeEnabled();
    fireEvent.click(within(nav).getByRole('button',{name:'프로젝트'}));
    await waitFor(()=>expect(window.location.pathname).toBe('/projects'));
    expect(screen.queryByRole('heading',{name:'준비중'})).not.toBeInTheDocument();
  });

});
