import { expect, type Route, test } from '@playwright/test';

const adminUserId = '50000000-0000-0000-0000-000000000001';

test('overall administrator enters automatically, switches in the header, and manages users only from Cheongju', async ({ page }, testInfo) => {
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
    if (path === '/api/admin/user-access/users') {
      return fulfillJson(route, membershipSnapshot([]));
    }
    if (path.endsWith('/access') && request.method() === 'PUT') {
      markMembershipMutationStarted();
      await membershipMutationReleased;
      return fulfillJson(route, {
        changed: true,
        accessVersion: 1,
        snapshot: membershipSnapshot(['OSAN'])
      });
    }
    return fulfillJson(route, { title: 'closed in synthetic Osan scope' }, 404);
  });

  await page.goto('/');
  const businessUnitSelector = page.locator('select[aria-label="사업부 선택"]:visible');
  await expect(page.getByRole('button', { name: /사업부로 이동/ })).toHaveCount(0);
  await expect(businessUnitSelector).toHaveValue('CHEONGJU');
  await businessUnitSelector.selectOption('OSAN');

  await expect(page.getByRole('heading', { name: '오산 사업부 홈' })).toBeVisible();
  const navigation = page.getByRole('navigation', { name: '공통 메뉴' });
  await expect(navigation.getByRole('button', { name: '프로젝트' })).toBeVisible();
  await expect(navigation.getByRole('button', { name: '진행 관리' })).toBeVisible();
  await expect(navigation.getByRole('button', { name: 'G2' })).toHaveCount(0);
  await expect(navigation.getByRole('button', { name: 'Pending' })).toHaveCount(0);
  await expect(navigation.getByRole('button', { name: '사용자 관리' })).toHaveCount(0);
  await expect(navigation.locator('.app-nav-group-label').filter({ hasText: /^관리$/ })).toHaveCount(0);

  await page.goto('/admin/users');
  await expect(businessUnitSelector).toHaveValue('CHEONGJU');
  await expect(page.getByRole('heading', { name: '사용자 관리' })).toBeVisible();
  await expect(navigation.getByRole('button', { name: '사업부 소속 관리' })).toHaveCount(0);
  await expect(page.getByRole('columnheader').allTextContents()).resolves.toEqual([
    '', '활성 상태', '사업부', '부서', '역할', '부서장', ''
  ]);
  const adminRow = page.getByRole('row').filter({ hasText: 'Synthetic Overall Admin' });
  await expect(adminRow.getByText('Synthetic Overall Admin')).toBeVisible();
  await expect(adminRow.getByText('총괄', { exact: true })).toBeVisible();

  const userRow = page.getByRole('row').filter({ hasText: 'Synthetic New User' });
  await userRow.getByRole('combobox', { name: 'Synthetic New User 사업부' }).selectOption('OSAN');
  await userRow.getByRole('checkbox', { name: 'Synthetic New User 활성 상태' }).check();
  await userRow.getByRole('combobox', { name: 'Synthetic New User 부서' })
    .selectOption('10000000-0000-0000-0000-000000000005');
  await expect(userRow.getByLabel('Synthetic New User 역할')).toHaveText('품질');
  const desktopRowBox = await userRow.boundingBox();
  expect(desktopRowBox?.height).toBeLessThanOrEqual(48);
  await userRow.getByRole('button', { name: '승인' }).click();
  await membershipMutationStarted;
  await expect(page.getByLabel('Synthetic New User 사업부')).toBeDisabled();
  releaseMembershipMutation();
  await expect(page.getByRole('status').filter({ hasText: '사용자 접근 정보를 저장했습니다.' })).toBeVisible();
  await expect(page.getByLabel('Synthetic New User 사업부')).toBeEnabled();

  await page.screenshot({ path: testInfo.outputPath('business-unit-access-desktop.png'), fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByLabel('Synthetic New User 사업부')).toHaveValue('OSAN');
  const mobileLayout = await page.locator('.business-unit-access-table-scroll').evaluate((element) => ({
    clientWidth: element.clientWidth,
    scrollWidth: element.scrollWidth
  }));
  expect(mobileLayout.scrollWidth).toBeGreaterThan(mobileLayout.clientWidth);
  const mobileRowBox = await userRow.boundingBox();
  expect(mobileRowBox?.height).toBeLessThanOrEqual(48);
  await page.screenshot({ path: testInfo.outputPath('business-unit-access-mobile.png'), fullPage: true });

  await businessUnitSelector.selectOption('OSAN');
  await expect(page).toHaveURL('/');
  await expect(page.getByRole('heading', { name: '오산 사업부 홈' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: '공통 메뉴' }).getByRole('button', { name: '사용자 관리' })).toHaveCount(0);
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
    if (path === '/api/admin/user-access/users') {
      return fulfillJson(route, membershipSnapshot());
    }
    return fulfillJson(route, { title: 'closed in synthetic business-unit scope' }, 404);
  });

  await installBackend(page);
  await page.goto('/');
  const firstTabSelector = page.locator('select[aria-label="사업부 선택"]:visible');
  await expect(firstTabSelector).toHaveValue('CHEONGJU');

  const secondTab = await page.context().newPage();
  await installBackend(secondTab);
  await secondTab.goto('/');
  const secondTabSelector = secondTab.locator('select[aria-label="사업부 선택"]:visible');
  await expect(secondTabSelector).toHaveValue('CHEONGJU');
  await secondTabSelector.selectOption('OSAN');
  await expect(secondTabSelector).toHaveValue('OSAN');
  await expect(firstTabSelector).toHaveValue('CHEONGJU');

  await Promise.all([page.reload(), secondTab.reload()]);
  await expect(firstTabSelector).toHaveValue('CHEONGJU');
  await expect(secondTabSelector).toHaveValue('OSAN');
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
    if (path === '/api/admin/user-access/users') {
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
  const navigation = page.getByRole('navigation', { name: '공통 메뉴' });
  await expect(navigation.getByRole('button', { name: '사용자 관리' })).toHaveCount(0);
  await page.goto('/admin/users');
  await expect(page).toHaveURL('/');
  await expect(page.getByRole('heading', { name: '오산 사업부 홈' })).toBeVisible();
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
  const unitProfile = (businessUnitCode: 'CHEONGJU' | 'OSAN', active: boolean) => ({
    businessUnitCode,
    membershipActive: active,
    localProfileExists: active,
    isActive: active,
    departmentId: active ? '10000000-0000-0000-0000-000000000005' : null,
    departmentCode: active ? 'quality' : null,
    departmentName: active ? '품질' : null,
    roles: active ? ['quality'] : [],
    isDepartmentHead: false,
    canManage: true
  });
  return {
    users: [
      {
        userId: adminUserId,
        authProvider: 'Dev',
        displayName: 'Synthetic Overall Admin',
        email: null,
        memberships: ['CHEONGJU', 'OSAN'],
        isOverallAdministrator: true,
        accessVersion: 1,
        pendingOperationId: null,
        pendingOperationStatus: null,
        pendingFailureCode: null,
        pendingProfiles: [],
        profiles: [unitProfile('CHEONGJU', true), unitProfile('OSAN', true)]
      },
      {
        userId: '50000000-0000-0000-0000-000000000002',
        authProvider: 'EntraId',
        displayName: 'Synthetic New User',
        email: 'new-user@example.invalid',
        memberships: userMemberships,
        isOverallAdministrator: false,
        accessVersion: 0,
        pendingOperationId: null,
        pendingOperationStatus: null,
        pendingFailureCode: null,
        pendingProfiles: [],
        profiles: [
          unitProfile('CHEONGJU', userMemberships.includes('CHEONGJU')),
          unitProfile('OSAN', userMemberships.includes('OSAN'))
        ]
      }
    ],
    availableBusinessUnits: ['CHEONGJU', 'OSAN'],
    businessUnits: ['CHEONGJU', 'OSAN'].map((code) => ({
      code,
      canManage: true,
      departments: [{
        departmentId: '10000000-0000-0000-0000-000000000005',
        code: 'quality',
        name: '품질',
        defaultRoleCode: 'quality'
      }],
      roles: [{
        roleId: '20000000-0000-0000-0000-000000000005',
        code: 'quality',
        name: '품질'
      }]
    }))
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
