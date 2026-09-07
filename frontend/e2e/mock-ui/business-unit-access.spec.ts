import { expect, type Route, test } from '@playwright/test';

const adminUserId = '50000000-0000-0000-0000-000000000001';

test('overall administrator selects Osan, sees the restricted shell, and manages memberships', async ({ page }, testInfo) => {
  let markMembershipMutationStarted!: () => void;
  let releaseMembershipMutation!: () => void;
  const membershipMutationStarted = new Promise<void>((resolve) => {
    markMembershipMutationStarted = resolve;
  });
  const membershipMutationReleased = new Promise<void>((resolve) => {
    releaseMembershipMutation = resolve;
  });

  await page.route('http://localhost:5080/**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const selectedBusinessUnit = request.headers()['x-qms-business-unit'];

    if (path === '/health/ready') {
      return fulfillJson(route, { status: 'ready', database: { reason: 'reachable' } });
    }
    if (path === '/api/runtime-mode') {
      return fulfillJson(route, {
        mode: 'Development',
        reviewSafe: false,
        mutationAllowed: true,
        databaseReadOnly: false,
        ready: true,
        reason: 'development'
      });
    }
    if (path === '/api/me') {
      return fulfillJson(route, selectedBusinessUnit
        ? currentUser(selectedBusinessUnit as 'CHEONGJU' | 'OSAN')
        : {
            userId: adminUserId,
            developmentUserKey: 'dev-admin',
            displayName: 'Synthetic Overall Admin',
            email: null,
            businessUnitAccessStatus: 'selection_required',
            allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
            isOverallAdministrator: true,
            errorCode: 'business_unit_selection_required',
            businessUnitAccess: {
              status: 'selection_required',
              selectedBusinessUnit: null,
              allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
              isOverallAdministrator: true,
              errorCode: 'business_unit_selection_required'
            }
          });
    }
    if (path === '/api/admin/business-unit-access/users') {
      return fulfillJson(route, membershipSnapshot());
    }
    if (path.endsWith('/memberships') && request.method() === 'PUT') {
      markMembershipMutationStarted();
      await membershipMutationReleased;
      return fulfillJson(route, {
        changed: true,
        snapshot: membershipSnapshot(['CHEONGJU', 'OSAN'])
      });
    }
    return fulfillJson(route, { title: 'closed in synthetic Osan scope' }, 404);
  });

  await page.goto('/');
  await expect(page.getByRole('heading', { name: '이 탭에서 사용할 사업부를 선택해 주세요.' })).toBeVisible();
  await page.getByRole('button', { name: '오산 사업부로 이동' }).click();

  await expect(page.getByRole('heading', { name: '오산 사업부 홈' })).toBeVisible();
  const navigation = page.getByRole('navigation', { name: '공통 메뉴' });
  await expect(navigation.getByRole('button', { name: '프로젝트' })).toBeVisible();
  await expect(navigation.getByRole('button', { name: '진행 관리' })).toBeVisible();
  await expect(navigation.getByRole('button', { name: 'G2' })).toHaveCount(0);
  await expect(navigation.getByRole('button', { name: 'Pending' })).toHaveCount(0);

  await navigation.getByRole('button', { name: '사업부 소속 관리' }).click();
  await expect(page.getByRole('heading', { name: '사업부 소속 관리' })).toBeVisible();
  const adminCard = page.locator('article').filter({ hasText: 'Synthetic Overall Admin' });
  await expect(adminCard.getByText('Synthetic Overall Admin')).toBeVisible();
  await expect(page.getByText('총괄 관리자', { exact: true })).toBeVisible();

  const userCard = page.locator('article').filter({ hasText: 'Synthetic New User' });
  await userCard.getByRole('checkbox', { name: '오산' }).check();
  await userCard.getByRole('button', { name: '소속 저장' }).click();
  await membershipMutationStarted;
  await expect(page.getByLabel('사업부 선택').first()).toBeDisabled();
  releaseMembershipMutation();
  await expect(page.getByRole('status').filter({ hasText: '사업부 소속을 저장했습니다.' })).toBeVisible();
  await expect(page.getByLabel('사업부 선택').first()).toBeEnabled();

  await page.screenshot({ path: testInfo.outputPath('business-unit-access-desktop.png'), fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByLabel('사업부 선택').first()).toHaveValue('OSAN');
  await page.screenshot({ path: testInfo.outputPath('business-unit-access-mobile.png'), fullPage: true });

  await page.goto('/pending');
  await expect(page).toHaveURL('/');
  await expect(page.getByRole('heading', { name: '오산 사업부 홈' })).toBeVisible();
});

test('each tab keeps and restores its own business-unit selection', async ({ page }) => {
  const installBackend = (target: typeof page) => target.route('http://localhost:5080/**', async (route) => {
    const path = new URL(route.request().url()).pathname;
    const selectedBusinessUnit = route.request().headers()['x-qms-business-unit'] as 'CHEONGJU' | 'OSAN' | undefined;
    if (path === '/health/ready') {
      return fulfillJson(route, { status: 'ready', database: { reason: 'reachable' } });
    }
    if (path === '/api/runtime-mode') {
      return fulfillJson(route, { mode: 'Development', reviewSafe: false, mutationAllowed: true });
    }
    if (path === '/api/me') {
      return fulfillJson(route, selectedBusinessUnit
        ? currentUser(selectedBusinessUnit)
        : selectionRequiredUser());
    }
    if (path === '/api/admin/business-unit-access/users') {
      return fulfillJson(route, membershipSnapshot());
    }
    return fulfillJson(route, { title: 'closed in synthetic business-unit scope' }, 404);
  });

  await installBackend(page);
  await page.goto('/');
  await page.getByRole('button', { name: '청주 사업부로 이동' }).click();
  await expect(page.getByLabel('사업부 선택').first()).toHaveValue('CHEONGJU');

  const secondTab = await page.context().newPage();
  await installBackend(secondTab);
  await secondTab.goto('/');
  await secondTab.getByRole('button', { name: '오산 사업부로 이동' }).click();
  await expect(secondTab.getByLabel('사업부 선택').first()).toHaveValue('OSAN');
  await expect(page.getByLabel('사업부 선택').first()).toHaveValue('CHEONGJU');

  await Promise.all([page.reload(), secondTab.reload()]);
  await expect(page.getByLabel('사업부 선택').first()).toHaveValue('CHEONGJU');
  await expect(secondTab.getByLabel('사업부 선택').first()).toHaveValue('OSAN');
});

test('single-business overall administrator sees no selector on desktop or mobile', async ({ page }, testInfo) => {
  let localProfileReady = false;

  await page.route('http://localhost:5080/**', async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/health/ready') {
      return fulfillJson(route, { status: 'ready', database: { reason: 'reachable' } });
    }
    if (path === '/api/runtime-mode') {
      return fulfillJson(route, {
        mode: 'Development',
        reviewSafe: false,
        mutationAllowed: true,
        databaseReadOnly: false,
        ready: true,
        reason: 'development'
      });
    }
    if (path === '/api/me') {
      return fulfillJson(route, localProfileReady
        ? currentUser('OSAN', ['OSAN'])
        : {
            userId: adminUserId,
            developmentUserKey: 'dev-admin',
            displayName: 'Synthetic Osan-only Overall Admin',
            email: null,
            businessUnitAccessStatus: 'local_profile_pending',
            allowedBusinessUnits: ['OSAN'],
            isOverallAdministrator: true,
            errorCode: 'business_unit_local_profile_pending',
            businessUnitAccess: {
              status: 'local_profile_pending',
              selectedBusinessUnit: 'OSAN',
              allowedBusinessUnits: ['OSAN'],
              isOverallAdministrator: true,
              errorCode: 'business_unit_local_profile_pending'
            }
          });
    }
    if (path === '/api/admin/business-unit-access/users') {
      return fulfillJson(route, membershipSnapshot());
    }
    return fulfillJson(route, { title: 'closed in synthetic business-unit scope' }, 404);
  });

  await page.goto('/');
  await expect(page.getByRole('heading', { name: '오산 사용자 등록이 필요합니다.' })).toBeVisible();
  await expect(page.getByRole('button', { name: /사업부로 이동/ })).toHaveCount(0);
  await expect(page.getByLabel('사업부 선택')).toHaveCount(0);
  await page.screenshot({ path: testInfo.outputPath('single-membership-gate-desktop.png'), fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('button', { name: /사업부로 이동/ })).toHaveCount(0);
  await expect(page.getByLabel('사업부 선택')).toHaveCount(0);
  await expect(page.locator('html')).toHaveJSProperty('scrollWidth', 390);
  await page.screenshot({ path: testInfo.outputPath('single-membership-gate-mobile.png'), fullPage: true });

  localProfileReady = true;
  await page.reload();
  await expect(page.getByRole('heading', { name: '오산 사업부 홈' })).toBeVisible();
  await expect(page.getByLabel('사업부 선택')).toHaveCount(0);
  await expect(page.locator('html')).toHaveJSProperty('scrollWidth', 390);
  await page.screenshot({ path: testInfo.outputPath('single-membership-shell-mobile.png'), fullPage: true });

  await page.setViewportSize({ width: 1440, height: 900 });
  await expect(page.getByLabel('사업부 선택')).toHaveCount(0);
  await page.screenshot({ path: testInfo.outputPath('single-membership-shell-desktop.png'), fullPage: true });
});

function currentUser(
  selectedBusinessUnit: 'CHEONGJU' | 'OSAN',
  allowedBusinessUnits: Array<'CHEONGJU' | 'OSAN'> = ['CHEONGJU', 'OSAN']
) {
  const principal = {
    userId: adminUserId,
    developmentUserKey: 'dev-admin',
    displayName: 'Synthetic Overall Admin',
    email: null,
    authProvider: 'Dev',
    isActive: true,
    approvalPending: false,
    department: 'management-support',
    departmentName: '경영지원',
    profilePhotoVersion: null,
    roles: ['system-administrator']
  };
  return {
    ...principal,
    permissions: ['users.manage'],
    projectAccess: [],
    isTestUserSwitch: false,
    testUserKey: null,
    canUseAdminTestUserSwitch: false,
    actualUser: principal,
    effectiveUser: principal,
    businessUnitAccess: {
      status: 'selected',
      selectedBusinessUnit,
      allowedBusinessUnits,
      isOverallAdministrator: true,
      errorCode: null
    }
  };
}

function selectionRequiredUser() {
  return {
    userId: adminUserId,
    developmentUserKey: 'dev-admin',
    displayName: 'Synthetic Overall Admin',
    email: null,
    businessUnitAccessStatus: 'selection_required',
    allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
    isOverallAdministrator: true,
    errorCode: 'business_unit_selection_required',
    businessUnitAccess: {
      status: 'selection_required',
      selectedBusinessUnit: null,
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: 'business_unit_selection_required'
    }
  };
}

function membershipSnapshot(userMemberships = ['CHEONGJU']) {
  return {
    users: [
      {
        userId: adminUserId,
        authProvider: 'Dev',
        displayName: 'Synthetic Overall Admin',
        email: null,
        memberships: ['CHEONGJU', 'OSAN'],
        isOverallAdministrator: true
      },
      {
        userId: '50000000-0000-0000-0000-000000000002',
        authProvider: 'EntraId',
        displayName: 'Synthetic New User',
        email: 'new-user@example.invalid',
        memberships: userMemberships,
        isOverallAdministrator: false
      }
    ],
    availableBusinessUnits: ['CHEONGJU', 'OSAN']
  };
}

function fulfillJson(route: Route, body: unknown, status = 200) {
  return route.fulfill({
    status,
    contentType: 'application/json',
    headers: {
      'Access-Control-Allow-Origin': 'http://127.0.0.1:5173',
      'Access-Control-Allow-Headers': 'Content-Type, X-Dev-User, X-Qms-Business-Unit'
    },
    body: JSON.stringify(body)
  });
}
