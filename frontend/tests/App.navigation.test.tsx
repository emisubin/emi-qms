import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from '../src/App';

const principal = {
  userId: '50000000-0000-0000-0000-000000000002',
  developmentUserKey: 'dev-production',
  displayName: 'dev-production',
  email: null,
  authProvider: 'Dev',
  isActive: true,
  approvalPending: false,
  department: 'production-planning',
  departmentName: '생산관리',
  profilePhotoVersion: null,
  roles: ['production']
};

const me = {
  ...principal,
  permissions: ['projects.read', 'Project.Read.All', 'ProductionPlan.Update'],
  projectAccess: [],
  isTestUserSwitch: false,
  testUserKey: null,
  canUseAdminTestUserSwitch: false,
  actualUser: principal,
  effectiveUser: principal
};

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}

async function navMockFetch(input: RequestInfo | URL): Promise<Response> {
  const path = new URL(String(input)).pathname;

  if (path === '/health/ready') {
    return json({ name: 'ready', status: 'ok', database: { isReady: true, reason: 'reachable' }, checkedAtUtc: '2026-08-01T00:00:00Z' });
  }

  if (path === '/api/runtime-mode') {
    return json({
      mode: 'Development',
      reviewSafe: false,
      mutationAllowed: true,
      backgroundWorkersEnabled: true,
      externalProvidersEnabled: true,
      databaseReadOnly: false,
      migrationExecutionEnabled: true,
      environment: 'Development',
      ready: true,
      reason: 'not_applicable',
      expectedMigration: null,
      actualMigration: null,
      migrationLedgerStatus: 'Exact',
      expectedMigrationCount: 0,
      actualMigrationCount: 0,
      missingMigrations: [],
      unexpectedMigrations: [],
      approvedLegacyMigrations: [],
      migrationSchemaCompatible: true,
      migrationLedgerReady: true
    });
  }

  if (path === '/api/me') {
    return json(me);
  }

  if (path === '/api/audit/site-access/signals') {
    return json({
      sessionId: '61000000-0000-4000-8000-000000000001',
      idempotencyReceipt: '61000000-0000-4000-8000-000000000002',
      startedAtUtc: '2026-09-01T00:00:00Z',
      lastActivityAtUtc: '2026-09-01T00:00:00Z',
      created: true
    });
  }

  // Nav tests only assert shell/navigation behavior; unrelated data endpoints
  // resolve to 404 so pages render their error states instead of crashing on
  // partially-shaped payloads.
  return json({ title: 'not found' }, 404);
}

async function renderShell() {
  render(<App />);
  const navigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
  return navigation;
}

describe('FABLE department navigation: whole-parent disclosure accordion', () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.history.pushState(null, '', '/');
    vi.stubGlobal('fetch', vi.fn(navMockFetch));
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('starts with every department collapsed and no child focusable', async () => {
    const navigation = await renderShell();

    for (const parent of ['생산관리', '자재', '품질', '물류']) {
      expect(within(navigation).getByRole('button', { name: parent })).toHaveAttribute('aria-expanded', 'false');
    }
    for (const child of ['생산계획', '제조 투입', '입고 관리', '패널 키팅', '수입검사(IQC)', '포장']) {
      expect(within(navigation).queryByRole('button', { name: child })).toBeNull();
    }
  });

  it('records the initial page and the next view using fixed menu codes only', async () => {
    await renderShell();
    const fetchMock = vi.mocked(fetch);
    await waitFor(() => expect(fetchMock.mock.calls.some(([input]) => (
      new URL(String(input)).pathname === '/api/audit/site-access/signals'
    ))).toBe(true));

    window.history.pushState(null, '', '/projects');
    window.dispatchEvent(new PopStateEvent('popstate'));

    await waitFor(() => {
      const bodies = fetchMock.mock.calls
        .filter(([input]) => new URL(String(input)).pathname === '/api/audit/site-access/signals')
        .map(([, init]) => JSON.parse(String(init?.body)));
      expect(bodies.map((body) => body.menuCode)).toEqual(['Home', 'Projects']);
      expect(bodies.every((body) => Object.keys(body).sort().join(',') === 'browserClientId,menuCode')).toBe(true);
    });
  });

  it('toggles children with the whole parent row without navigating', async () => {
    const navigation = await renderShell();
    const parent = within(navigation).getByRole('button', { name: '생산관리' });

    fireEvent.click(parent);
    expect(parent).toHaveAttribute('aria-expanded', 'true');
    const childListId = parent.getAttribute('aria-controls');
    expect(childListId).toBeTruthy();
    const childList = document.getElementById(childListId!);
    expect(childList).not.toBeNull();
    expect(within(childList as HTMLElement).getByRole('button', { name: '생산계획' })).toBeInTheDocument();
    expect(within(childList as HTMLElement).getByRole('button', { name: '제조 투입' })).toBeInTheDocument();

    // Toggling is not navigation: no workspace heading, no work-selection hub.
    expect(screen.queryByRole('heading', { name: '생산관리' })).toBeNull();
    expect(document.querySelector('[data-testid^="department-work-hub-"]')).toBeNull();

    fireEvent.click(parent);
    expect(parent).toHaveAttribute('aria-expanded', 'false');
    expect(within(navigation).queryByRole('button', { name: '생산계획' })).toBeNull();
  });

  it('keeps at most one department expanded', async () => {
    const navigation = await renderShell();

    fireEvent.click(within(navigation).getByRole('button', { name: '생산관리' }));
    expect(within(navigation).getByRole('button', { name: '생산계획' })).toBeInTheDocument();

    fireEvent.click(within(navigation).getByRole('button', { name: '물류' }));
    expect(within(navigation).getByRole('button', { name: '생산관리' })).toHaveAttribute('aria-expanded', 'false');
    expect(within(navigation).queryByRole('button', { name: '생산계획' })).toBeNull();
    expect(within(navigation).getByRole('button', { name: '포장' })).toBeInTheDocument();
    expect(within(navigation).getByRole('button', { name: '납품' })).toBeInTheDocument();
  });

  it('navigates through children only, retaining the parent expansion for orientation', async () => {
    const navigation = await renderShell();

    fireEvent.click(within(navigation).getByRole('button', { name: '생산관리' }));
    fireEvent.click(within(navigation).getByRole('button', { name: '생산계획' }));

    expect(await screen.findByRole('heading', { name: '생산관리' })).toBeInTheDocument();
    expect(document.querySelector('[data-testid^="department-work-hub-"]')).toBeNull();
    expect(within(navigation).getByRole('button', { name: '생산관리' })).toHaveAttribute('aria-expanded', 'true');
    expect(within(navigation).getByRole('button', { name: '생산계획' })).toHaveAttribute('aria-current', 'page');

    fireEvent.click(within(navigation).getByRole('button', { name: '제조 투입' }));
    await waitFor(() => expect(within(navigation).getByRole('button', { name: '제조 투입' })).toHaveAttribute('aria-current', 'page'));
    expect(within(navigation).getByRole('button', { name: '생산계획' })).not.toHaveAttribute('aria-current');
  });

  it('redirects legacy department-root deep links to the first workspace and keeps departments collapsed', async () => {
    window.history.pushState(null, '', '/materials');
    const navigation = await renderShell();

    await waitFor(() => expect(within(navigation).getByRole('button', { name: '자재' })).toHaveAttribute('aria-current', 'page'));
    expect(document.querySelector('[data-testid^="department-work-hub-"]')).toBeNull();
    expect(screen.queryByRole('heading', { name: '처리할 업무를 선택하세요' })).toBeNull();
    expect(within(navigation).getByRole('button', { name: '자재' })).toHaveAttribute('aria-expanded', 'false');
    expect(within(navigation).queryByRole('button', { name: '입고 관리' })).toBeNull();
  });
});

describe('FABLE mobile drawer: whole-parent disclosure (390px layout mode)', () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.history.pushState(null, '', '/materials/kitting');
    vi.stubGlobal('matchMedia', vi.fn((query: string) => ({
      matches: query === '(max-width: 860px)',
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn()
    })));
    vi.stubGlobal('fetch', vi.fn(navMockFetch));
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  async function openDrawer() {
    fireEvent.click(await screen.findByRole('button', { name: '메뉴 열기' }));
    return await screen.findByRole('dialog', { name: '전체 업무 메뉴' });
  }

  it('opens collapsed with the active department current but not expanded', async () => {
    render(<App />);
    const menu = await openDrawer();

    const materials = within(menu).getByRole('button', { name: '자재' });
    expect(materials).toHaveAttribute('aria-current', 'page');
    expect(materials).toHaveAttribute('aria-expanded', 'false');
    expect(within(menu).queryAllByRole('button', { name: /키팅/ })).toHaveLength(0);
  });

  it('expands one department at a time from the parent row and navigates via a child, closing the drawer', async () => {
    render(<App />);
    let menu = await openDrawer();

    const materials = within(menu).getByRole('button', { name: '자재' });
    fireEvent.click(materials);
    expect(materials).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('dialog', { name: '전체 업무 메뉴' })).toBeInTheDocument();
    expect(within(menu).getByRole('button', { name: '패널 키팅' })).toHaveAttribute('aria-current', 'page');

    fireEvent.click(within(menu).getByRole('button', { name: '품질' }));
    expect(within(menu).queryByRole('button', { name: '패널 키팅' })).toBeNull();
    expect(within(menu).getByRole('button', { name: '수입검사(IQC)' })).toBeInTheDocument();

    fireEvent.click(within(menu).getByRole('button', { name: '자재' }));
    fireEvent.click(within(menu).getByRole('button', { name: '입고 관리' }));
    expect(screen.queryByRole('dialog', { name: '전체 업무 메뉴' })).toBeNull();

    menu = await openDrawer();
    expect(within(menu).getByRole('button', { name: '자재' })).toHaveAttribute('aria-expanded', 'false');
    expect(within(menu).queryByRole('button', { name: '입고 관리' })).toBeNull();
    fireEvent.click(within(menu).getByRole('button', { name: '자재' }));
    expect(within(menu).getByRole('button', { name: '입고 관리' })).toHaveAttribute('aria-current', 'page');
    expect(within(menu).getByRole('button', { name: '패널 키팅' })).not.toHaveAttribute('aria-current');
  });
});
