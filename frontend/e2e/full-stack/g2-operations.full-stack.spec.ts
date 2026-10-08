import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type APIRequestContext, type APIResponse, type Locator, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = path.resolve(process.cwd(), 'test-results/g2-operations');

type MetricPayload = { quantity: number | null; version: number } | null;
type G2DayPayload = {
  isForecast: boolean;
  productionPlan: MetricPayload;
  deliveryPlan: MetricPayload;
  planNote: { text: string | null } | null;
  morningProduction: MetricPayload;
  afternoonProduction: MetricPayload;
  delivery: MetricPayload;
  defect: MetricPayload;
  morningRepair: MetricPayload;
  afternoonRepair: MetricPayload;
  repairTotal: number | null;
  defectInventory: number | null;
  defectInventoryCount: MetricPayload;
  productionTotal: number | null;
  morningAttendanceTotal: number | null;
  afternoonAttendanceTotal: number | null;
  attendanceTotal: number | null;
  inventory: number | null;
  dailyProductionTarget: MetricPayload;
  deliveryTarget: MetricPayload;
  inventoryTarget: MetricPayload;
};

test('G2 permissions, concurrent inputs, inventory calculation, and responsive UI use the isolated stack', async ({ page, request }) => {
  test.setTimeout(180_000);
  page.setDefaultTimeout(10_000);
  const consoleErrors: string[] = [];
  page.on('console', message => { if (message.type() === 'error') consoleErrors.push(message.text()); });
  await fs.mkdir(screenshotDirectory, { recursive: true });

  const initialHome = await getJson<{ today: string }>(request, '/api/g2/home', 'dev-sales');
  const today = initialHome.today;
  const tomorrow = addDays(today, 1);
  const dayAfterTomorrow = addDays(today, 2);
  const futureDatesShareMonth = tomorrow.slice(0, 7) === dayAfterTomorrow.slice(0, 7);
  const futureRangeEnd = futureDatesShareMonth ? dayAfterTomorrow : tomorrow;

  await expectStatus(request.put(`${apiBaseUrl}/api/g2/operations/${today}`, {
    headers: devHeaders('dev-manufacturing'),
    data: {
      morningProduction: { quantity: 7, expectedVersion: null },
      delivery: { quantity: 2, expectedVersion: null }
    }
  }), 403);
  let todayData = await getDay(request, today, 'dev-quality');
  expect(todayData.morningProduction).toBeNull();

  await expectOk(request.put(`${apiBaseUrl}/api/g2/operations/${today}`, {
    headers: devHeaders('dev-admin'),
    data: {
      morningProduction: { quantity: 10, expectedVersion: null },
      afternoonProduction: { quantity: 4, expectedVersion: null },
      delivery: { quantity: 3, expectedVersion: null },
      defect: { quantity: 1, expectedVersion: null }
    }
  }));

  const competing = await Promise.all([
    request.put(`${apiBaseUrl}/api/g2/operations/${today}`, {
      headers: devHeaders('dev-manufacturing'),
      data: { morningProduction: { quantity: 11, expectedVersion: 1 } }
    }),
    request.put(`${apiBaseUrl}/api/g2/operations/${today}`, {
      headers: devHeaders('dev-manufacturing'),
      data: { morningProduction: { quantity: 12, expectedVersion: 1 } }
    })
  ]);
  expect(competing.map(response => response.status()).sort()).toEqual([200, 409]);

  todayData = await getDay(request, today, 'dev-sales');
  expect(todayData.morningProduction).not.toBeNull();
  const winningMorningVersion = todayData.morningProduction!.version;
  await Promise.all([
    expectOk(request.put(`${apiBaseUrl}/api/g2/operations/${today}`, {
      headers: devHeaders('dev-manufacturing'),
      data: { morningProduction: { quantity: 13, expectedVersion: winningMorningVersion } }
    })),
    expectOk(request.put(`${apiBaseUrl}/api/g2/operations/${today}`, {
      headers: devHeaders('dev-manufacturing'),
      data: { afternoonProduction: { quantity: 5, expectedVersion: 1 } }
    }))
  ]);

  await expectStatus(request.put(`${apiBaseUrl}/api/g2/operations/${today}`, {
    headers: devHeaders('dev-logistics'),
    data: { morningProduction: { quantity: 99, expectedVersion: winningMorningVersion + 1 } }
  }), 403);
  await expectStatus(request.put(`${apiBaseUrl}/api/g2/operations/${today}`, {
    headers: devHeaders('dev-logistics'),
    data: { defect: { quantity: 99, expectedVersion: 1 } }
  }), 403);
  await expectStatus(request.put(`${apiBaseUrl}/api/g2/operations/${today}`, {
    headers: devHeaders('dev-logistics'),
    data: { delivery: { quantity: 4, expectedVersion: 1 } }
  }), 403);
  await expectOk(request.put(`${apiBaseUrl}/api/g2/operations/${today}`, {
    headers: devHeaders('dev-sales'),
    data: { delivery: { quantity: 4, expectedVersion: 1 } }
  }));

  await expectOk(request.put(`${apiBaseUrl}/api/g2/attendance/${today}`, {
    headers: devHeaders('dev-manufacturing'),
    data: {
      morningEmiAttendance: { quantity: 6, expectedVersion: null },
      morningContractorAttendance: { quantity: 2, expectedVersion: null },
      afternoonEmiAttendance: { quantity: 5, expectedVersion: null },
      afternoonContractorAttendance: { quantity: 1, expectedVersion: null }
    }
  }));
  await expectOk(request.put(`${apiBaseUrl}/api/g2/inventory-counts/${today}`, {
    headers: devHeaders('dev-sales'),
    data: { quantity: 20, expectedVersion: null }
  }));
  await expectOk(request.put(`${apiBaseUrl}/api/g2/targets/DailyProduction/${today}`, {
    headers: devHeaders('dev-sales'),
    data: { quantity: 24, expectedVersion: null }
  }));
  await expectOk(request.put(`${apiBaseUrl}/api/g2/targets/Delivery/${today}`, {
    headers: devHeaders('dev-sales'),
    data: { quantity: 15, expectedVersion: null }
  }));
  await expectOk(request.put(`${apiBaseUrl}/api/g2/targets/Inventory/${today}`, {
    headers: devHeaders('dev-sales'),
    data: { quantity: 18, expectedVersion: null }
  }));

  await expectStatus(request.put(`${apiBaseUrl}/api/g2/operations/${tomorrow}`, {
    headers: devHeaders('dev-manufacturing'),
    data: { morningProduction: { quantity: 2, expectedVersion: null } }
  }), 400);
  await expectStatus(request.put(`${apiBaseUrl}/api/g2/attendance/${tomorrow}`, {
    headers: devHeaders('dev-sales'),
    data: { morningEmiAttendance: { quantity: 7, expectedVersion: null } }
  }), 400);
  await expectStatus(request.put(`${apiBaseUrl}/api/g2/inventory-counts/${tomorrow}`, {
    headers: devHeaders('dev-sales'),
    data: { quantity: 99, expectedVersion: null }
  }), 400);
  await expectStatus(request.put(`${apiBaseUrl}/api/g2/operations/2200-01-02`, {
    headers: devHeaders('dev-sales'),
    data: { delivery: { quantity: 1, expectedVersion: null } }
  }), 400);
  await expectOk(request.put(`${apiBaseUrl}/api/g2/workspace/${tomorrow}`, {
    headers: devHeaders('dev-sales'),
    data: { deliveryPlan: { quantity: 8, expectedVersion: null }, planNote: { text: '다음 출하 계획', expectedVersion: null } }
  }));
  await expectOk(request.put(`${apiBaseUrl}/api/g2/workspace/${tomorrow}`, {
    headers: devHeaders('dev-manufacturing'),
    data: { productionPlan: { quantity: 5, expectedVersion: null } }
  }));

  todayData = await getDay(request, today, 'dev-viewer');
  expect(todayData.productionTotal).toBe(18);
  expect(todayData.delivery!.quantity).toBe(4);
  expect(todayData.defect!.quantity).toBe(1);
  expect(todayData.inventory).toBe(20);
  expect(todayData.morningAttendanceTotal).toBe(8);
  expect(todayData.afternoonAttendanceTotal).toBe(6);
  expect(todayData.attendanceTotal).toBe(14);

  const tomorrowData = await getDay(request, tomorrow, 'dev-viewer');
  expect(tomorrowData.isForecast).toBe(true);
  expect(tomorrowData.productionTotal).toBeNull();
  expect(tomorrowData.defect).toBeNull();
  expect(tomorrowData.inventory).toBeNull();
  expect(tomorrowData.morningAttendanceTotal).toBeNull();
  expect(tomorrowData.productionPlan?.quantity).toBe(5);
  expect(tomorrowData.deliveryPlan?.quantity).toBe(8);
  expect(tomorrowData.planNote?.text).toBe('다음 출하 계획');
  expect(tomorrowData.dailyProductionTarget!.quantity).toBe(24);
  expect(tomorrowData.deliveryTarget!.quantity).toBe(15);
  expect(tomorrowData.inventoryTarget!.quantity).toBe(18);
  expect((await getDay(request, dayAfterTomorrow, 'dev-viewer')).inventory).toBeNull();

  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto('/g2');
  await expect(page.getByRole('heading', { name: 'G2 홈' })).toBeVisible();
  await expect(page.getByRole('img', { name: '일별 생산, 납품, 재고 추이' })).toBeVisible();
  await expect(page.getByRole('img', { name: '오전조와 오후조 생산량' })).toBeVisible();
  await expect(page.getByRole('heading', { name: '제조 인원 출근 현황' })).toBeVisible();
  await expect(page.getByText('손익관리', { exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: '목표 저장' })).toBeDisabled();
  await page.getByLabel('목표 종류').selectOption('Delivery');
  await expect(page.getByLabel('목표 종류')).toHaveValue('Delivery');
  await page.getByLabel('목표 수량').fill('0');
  await expect(page.getByRole('button', { name: '목표 저장' })).toBeEnabled();
  await page.getByLabel('목표 수량').fill('');
  await page.getByRole('button', { name: '실사 입력', exact: true }).click();
  await page.getByLabel('실사 수량').fill('');
  await expect(page.getByRole('dialog').getByRole('button', { name: '저장' })).toBeDisabled();
  await page.getByLabel('실사 수량').fill('0');
  await expect(page.getByRole('dialog').getByRole('button', { name: '저장' })).toBeEnabled();
  await page.getByRole('dialog').getByRole('button', { name: '닫기' }).click();

  await moveG2HomeMonth(page, today, tomorrow);
  const desktopHomeRange = page.getByRole('group', { name: '홈 표시 기간' });
  await setG2DateRange(desktopHomeRange, tomorrow, futureRangeEnd);
  await desktopHomeRange.getByRole('button', { name: '전체 기간' }).click();
  const productionTable = page.getByRole('table', { name: '생산 현황' });
  await page.getByRole('button', { name: '불량 상세 보기', exact: true }).click();
  await expect(page.getByLabel(`${koreanDate(tomorrow)} 신규 불량 임시 계산값`)).toHaveCount(0);
  const inventoryRow = productionTable.getByRole('rowheader', { name: '재고', exact: true }).locator('..');
  const tomorrowColumn = (await productionTable.locator('thead th').allTextContents()).findIndex(value => value.includes(koreanDate(tomorrow)));
  expect(tomorrowColumn).toBeGreaterThan(0);
  await expect(inventoryRow.locator('td').nth(tomorrowColumn - 1)).toHaveText('—');
  await moveG2HomeMonth(page, tomorrow, today);
  await setG2DateRange(desktopHomeRange, today, today);
  const previewLabel = `${koreanDate(today)} 신규 불량 임시 계산값`;
  const previewBefore = await getDay(request, today, 'dev-sales');
  const centeredPreviewInput = page.getByLabel(previewLabel);
  await expect(centeredPreviewInput).toHaveCSS('appearance', 'textfield');
  const previewInputGaps = await centeredPreviewInput.evaluate(element => {
    const inputBounds = element.getBoundingClientRect();
    const cellBounds = element.closest('td')!.getBoundingClientRect();
    return { left: inputBounds.left - cellBounds.left, right: cellBounds.right - inputBounds.right };
  });
  expect(Math.abs(previewInputGaps.left - previewInputGaps.right)).toBeLessThanOrEqual(1);
  await centeredPreviewInput.fill('5');
  expect((await getDay(request, today, 'dev-sales')).defect?.quantity).toBe(previewBefore.defect?.quantity);
  await page.reload();
  await page.getByRole('button', { name: '불량 상세 보기', exact: true }).click();
  await expect(page.getByLabel(previewLabel)).toHaveValue('1');
  await capture(page, '01-g2-home-desktop-1440.png');

  await page.goto('/g2/operations');
  await expect(page.getByRole('heading', { name: '생산/출하 관리' })).toBeVisible();
  await expect(page.getByRole('table', { name: '월간 생산·출하 현황' })).toBeVisible();
  await page.getByRole('button', { name: '데이터 입력' }).first().click();
  await page.getByRole('button', { name: '실적 입력' }).click();
  const actualDialog = page.getByRole('dialog', { name: '실적 입력' });
  await expect(actualDialog.locator('input[type="date"]')).toHaveValue(today);
  await actualDialog.getByRole('tab', { name: '납품' }).click();
  await actualDialog.getByLabel('납품 실적').fill('0');
  await actualDialog.getByRole('button', { name: '저장' }).click();
  await expect(actualDialog).toHaveCount(0);
  expect((await getDay(request, today, 'dev-sales')).delivery?.quantity).toBe(0);
  expect((await getDay(request, today, 'dev-sales')).inventory).toBe(20);

  await selectG2OperationsMonth(page, tomorrow);
  const futureRow = page.locator('.g2-date-row').filter({ has: page.getByRole('button', { name: `${tomorrow} 상세 펼치기` }) });
  await expect(futureRow).toContainText('8');
  await expect(futureRow).toContainText('5');
  const cells = futureRow.locator('td');
  await expect(cells.nth(3)).toBeEmpty();
  await expect(cells.nth(4)).toBeEmpty();
  await expect(cells.nth(6)).toBeEmpty();
  await expect(cells.nth(7)).toBeEmpty();
  await expect(cells.nth(8)).toBeEmpty();
  await futureRow.getByRole('button', { name: `${tomorrow} 상세 펼치기` }).click();
  await expect(page.getByText('다음 출하 계획')).toBeVisible();
  await page.getByRole('button', { name: '데이터 입력' }).first().click();
  await page.getByRole('button', { name: '계획 입력' }).click();
  const planDialog = page.getByRole('dialog', { name: '계획 입력' });
  await planDialog.getByLabel('계획 시작일').fill(tomorrow);
  await expect(planDialog.getByLabel('납품 계획')).toHaveValue('8');
  await planDialog.getByRole('button', { name: '취소' }).click();

  await page.getByLabel('개발 사용자').selectOption('dev-manufacturing');
  await page.goto('/g2/operations');
  await selectG2OperationsMonth(page, today);
  await page.getByRole('button', { name: '데이터 입력' }).first().click();
  await page.getByRole('button', { name: '실적 입력' }).click();
  const manufacturingDialog = page.getByRole('dialog', { name: '실적 입력' });
  await expect(manufacturingDialog.getByLabel('오전 생산 실적')).toBeEnabled();
  await expect(manufacturingDialog.getByLabel('오전 수리 실적')).toBeEnabled();
  await manufacturingDialog.getByRole('tab', { name: '오후' }).click();
  await expect(manufacturingDialog.getByLabel('신규 불량 실적')).toBeEnabled();
  await manufacturingDialog.getByRole('tab', { name: '납품' }).click();
  await expect(manufacturingDialog.getByLabel('납품 실적')).toHaveCount(0);
  await manufacturingDialog.getByRole('button', { name: '취소' }).click();

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('heading', { name: '생산/출하 관리' })).toBeVisible();
  await selectG2OperationsMonth(page, tomorrow);
  await page.getByRole('button', { name: `${tomorrow} 상세 보기` }).click();
  const mobileDetail = page.getByRole('dialog');
  await expect(mobileDetail.getByRole('table', { name: '납품·생산' })).toContainText('8');
  await expect(mobileDetail.getByRole('table', { name: '납품·생산' })).toContainText('5');
  await expect(mobileDetail.getByRole('table', { name: '재고·실사' }).locator('td').first()).toBeEmpty();
  await expect(mobileDetail.getByText('다음 출하 계획')).toBeVisible();
  await mobileDetail.getByRole('button', { name: '닫기' }).click();
  await assertNoPageOverflow(page);
  await capture(page, '02-g2-operations-mobile-390.png');

  await page.goto('/g2');
  await expect(page.getByRole('heading', { name: 'G2 홈' })).toBeVisible();
  const homeRange = page.getByRole('group', { name: '홈 표시 기간' });
  await setG2DateRange(homeRange, today, today);
  await page.getByRole('button', { name: '불량 상세 보기', exact: true }).click();
  await expectInputWidth(homeRange.getByLabel('시작일'), 120);
  await expectInputWidth(homeRange.getByLabel('종료일'), 120);
  await expectInputWidth(page.getByLabel('적용 시작일'), 136);
  await expect(page.getByLabel(previewLabel)).toHaveValue('1');
  await page.getByRole('button', { name: '수리 상세 보기', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByLabel(`${koreanDate(today)} 오전 수리 임시 계산값`)).toBeVisible();
  await page.getByRole('button', { name: '수리 상세 접기', exact: true }).click();
  await moveG2HomeMonth(page, today, tomorrow);
  await setG2DateRange(homeRange, tomorrow, futureRangeEnd);
  await expect(page.getByLabel(`${koreanDate(tomorrow)} 신규 불량 임시 계산값`)).toHaveCount(0);
  await homeRange.getByRole('button', { name: '전체 기간' }).click();
  await assertNoPageOverflow(page);
  await capture(page, '03-g2-home-mobile-390.png');

  await page.goto('/g2/attendance');
  await expect(page.getByRole('heading', { name: '제조 인원 출근 관리' })).toBeVisible();
  await page.getByLabel('입력 날짜').fill(today);
  const attendanceRange = page.getByRole('group', { name: '출근 현황 표시 기간' });
  await setG2DateRange(attendanceRange, today, today);
  await expect(page.getByRole('button', { name: `${koreanDate(today)} 오전 합계 8명 세부 인원 보기` })).toBeVisible();
  await attendanceRange.getByRole('button', { name: '전체 기간' }).click();
  await expectInputWidth(attendanceRange.getByLabel('시작일'), 120);
  await expectInputWidth(attendanceRange.getByLabel('종료일'), 120);
  await assertNoPageOverflow(page);
  await capture(page, '04-g2-attendance-mobile-390.png');

  const inventoryBeforeCount = (await getDay(request, tomorrow, 'dev-sales')).inventory;
  await expectStatus(request.put(`${apiBaseUrl}/api/g2/defect-inventory-counts/${today}`, {
    headers: devHeaders('dev-logistics'), data: { quantity: 0, expectedVersion: null }
  }), 403);
  await expectStatus(request.put(`${apiBaseUrl}/api/g2/defect-inventory-counts/${tomorrow}`, {
    headers: devHeaders('dev-sales'), data: { quantity: 0, expectedVersion: null }
  }), 400);
  await page.goto('/g2');
  await expect(page.getByRole('heading', { name: 'G2 홈' })).toBeVisible();
  await setG2DateRange(page.getByRole('group', { name: '홈 표시 기간' }), today, today);
  await page.getByRole('button', { name: '실사 입력', exact: true }).click();
  await page.getByLabel('실사 구분').selectOption('defect');
  const countDialog = page.getByRole('dialog', { name: '불량재고 실사 입력' });
  await expect(countDialog.getByLabel('실사 날짜')).toHaveValue(today);
  await expect(countDialog.getByRole('button', { name: '저장', exact: true })).toBeDisabled();
  await countDialog.getByLabel('실사 수량').fill('0');
  await assertNoPageOverflow(page);
  await page.screenshot({ path: path.join(screenshotDirectory, '05-defect-count-dialog-mobile-390.png'), animations: 'disabled' });
  await countDialog.getByRole('button', { name: '저장', exact: true }).click();
  await expect(countDialog).toHaveCount(0);
  const countedDay = await getDay(request, today, 'dev-sales');
  expect(countedDay.defectInventoryCount?.quantity).toBe(0);
  expect(countedDay.defectInventory).toBe(0);
  expect(countedDay.defect?.quantity).toBe(1);
  expect((await getDay(request, tomorrow, 'dev-sales')).inventory).toBe(inventoryBeforeCount);
  await expectStatus(request.put(`${apiBaseUrl}/api/g2/defect-inventory-counts/${today}`, {
    headers: devHeaders('dev-sales'), data: { quantity: 9, expectedVersion: null }
  }), 409);
  await page.getByRole('button', { name: '불량 상세 보기', exact: true }).click();
  await expect(page.getByRole('table', { name: '생산 현황', exact: true }).getByText('실사', { exact: true })).toBeVisible();
  await page.getByRole('table', { name: '생산 현황', exact: true }).getByText('실사', { exact: true }).scrollIntoViewIfNeeded();
  await page.getByRole('heading', { name: '생산 현황', exact: true }).scrollIntoViewIfNeeded();
  await assertNoPageOverflow(page);
  await page.screenshot({ path: path.join(screenshotDirectory, '06-defect-count-table-mobile-390.png'), animations: 'disabled' });
  await page.setViewportSize({ width: 1440, height: 960 });
  await page.getByRole('button', { name: `${Number(today.slice(-2))}일 불량 실사 0대 · 수정`, exact: true }).click();
  await expect(countDialog.getByLabel('실사 수량')).toHaveValue('0');
  await page.screenshot({ path: path.join(screenshotDirectory, '07-defect-count-dialog-desktop-1440.png'), animations: 'disabled' });
  await countDialog.getByLabel('실사 수량').fill('4');
  await countDialog.getByRole('button', { name: '저장', exact: true }).click();
  await expect(countDialog).toHaveCount(0);
  expect((await getDay(request, today, 'dev-sales')).defectInventoryCount?.quantity).toBe(4);
  await page.getByRole('button', { name: `${Number(today.slice(-2))}일 불량 실사 4대 · 수정`, exact: true }).click();
  await countDialog.getByRole('button', { name: '실사 삭제', exact: true }).click();
  await expect(countDialog).toHaveCount(0);
  const deletedCount = (await getDay(request, today, 'dev-sales')).defectInventoryCount;
  expect(deletedCount?.quantity).toBeNull();
  expect(deletedCount?.version).toBe(3);
  await expect(page.getByRole('button', { name: `${Number(today.slice(-2))}일 불량 실사 4대 · 수정`, exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: '실사 입력', exact: true }).click();
  await page.getByRole('dialog').getByLabel('실사 구분').selectOption('defect');
  await expect(countDialog.getByLabel('실사 수량')).toHaveValue('');
  await expect(countDialog.getByRole('button', { name: '실사 삭제', exact: true })).toHaveCount(0);
  await countDialog.getByLabel('실사 수량').fill('6');
  const recountRequest = page.waitForRequest(request => request.url().includes(`/api/g2/defect-inventory-counts/${today}`) && request.method() === 'PUT');
  await countDialog.getByRole('button', { name: '저장', exact: true }).click();
  expect((await recountRequest).postDataJSON()).toEqual({ quantity: 6, expectedVersion: 3 });
  await expect(countDialog).toHaveCount(0);
  expect((await getDay(request, today, 'dev-sales')).defectInventoryCount?.quantity).toBe(6);
  expect((await getDay(request, today, 'dev-sales')).defectInventoryCount?.version).toBe(4);
  expect((await getDay(request, tomorrow, 'dev-sales')).inventory).toBe(inventoryBeforeCount);
  expect(consoleErrors).toEqual([]);
});

async function getDay(request: APIRequestContext, date: string, userKey: string) {
  const response = await getJson<{ days: G2DayPayload[] }>(request, `/api/g2/days?from=${date}&to=${date}`, userKey);
  expect(response.days).toHaveLength(1);
  return response.days[0];
}

async function getJson<T>(request: APIRequestContext, pathName: string, userKey: string): Promise<T> {
  const response = await request.get(`${apiBaseUrl}${pathName}`, { headers: devHeaders(userKey) });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json() as Promise<T>;
}

async function expectOk(responsePromise: Promise<APIResponse>) {
  const response = await responsePromise;
  expect(response.ok(), await response.text()).toBeTruthy();
}

async function expectStatus(responsePromise: Promise<APIResponse>, status: number) {
  const response = await responsePromise;
  expect(response.status(), await response.text()).toBe(status);
}

function devHeaders(userKey: string) {
  return { 'X-Dev-User': userKey };
}

function addDays(value: string, days: number) {
  const date = new Date(`${value}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

function koreanDate(value: string) {
  const [, month, day] = value.split('-').map(Number);
  return `${month}월 ${day}일`;
}

async function selectG2OperationsMonth(page: Page, date: string) {
  await page.getByRole('button', { name: '조회 연월 선택' }).click();
  const dialog = page.getByRole('dialog', { name: '조회 연월 선택' });
  await dialog.getByLabel('조회 연도').fill(date.slice(0, 4));
  await dialog.getByLabel('조회 월').selectOption(String(Number(date.slice(5, 7))));
  await dialog.getByRole('button', { name: '조회', exact: true }).click();
  await expect(page.getByRole('button', { name: `${date} 상세 펼치기` }).or(page.getByRole('button', { name: `${date} 상세 보기` }))).toBeVisible();
}

async function moveG2HomeMonth(page: Page, currentDate: string, targetDate: string) {
  const current = new Date(`${currentDate.slice(0, 7)}-01T00:00:00Z`);
  const target = new Date(`${targetDate.slice(0, 7)}-01T00:00:00Z`);
  const monthDifference = (target.getUTCFullYear() - current.getUTCFullYear()) * 12
    + target.getUTCMonth() - current.getUTCMonth();
  const direction = monthDifference < 0 ? '이전 달' : '다음 달';
  for (let index = 0; index < Math.abs(monthDifference); index += 1) {
    await page.getByRole('button', { name: direction }).click();
  }
  const [targetYear, targetMonth] = targetDate.split('-').map(Number);
  await expect(page.getByText(`${targetYear}년 ${targetMonth}월`, { exact: true })).toBeVisible();
}

async function setG2DateRange(range: Locator, from: string, to: string) {
  const monthStart = `${from.slice(0, 7)}-01`;
  const [year, month] = from.split('-').map(Number);
  const monthEnd = new Date(Date.UTC(year, month, 0)).toISOString().slice(0, 10);
  const startInput = range.getByLabel('시작일');
  const endInput = range.getByLabel('종료일');
  await expect(startInput).toHaveAttribute('min', monthStart);
  await expect(endInput).toHaveAttribute('max', monthEnd);
  await startInput.fill(from);
  await endInput.fill(to);
  await expect(startInput).toHaveValue(from);
  await expect(endInput).toHaveValue(to);
}

async function capture(page: Page, filename: string) {
  await page.screenshot({ path: path.join(screenshotDirectory, filename), animations: 'disabled', fullPage: true });
}

async function assertNoPageOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBe(0);
}

async function expectInputWidth(locator: Locator, maximum: number) {
  const box = await locator.boundingBox();
  expect(box).not.toBeNull();
  expect(box!.width).toBeLessThanOrEqual(maximum);
}
