import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type APIRequestContext, type APIResponse, type Locator, type Page } from '@playwright/test';

const apiBaseUrl = `http://127.0.0.1:${process.env.E2E_BACKEND_PORT ?? '5082'}`;
const screenshotDirectory = path.resolve(process.cwd(), 'test-results/g2-operations');

type MetricPayload = { quantity: number; version: number } | null;
type G2DayPayload = {
  isForecast: boolean;
  morningProduction: MetricPayload;
  afternoonProduction: MetricPayload;
  delivery: MetricPayload;
  defect: MetricPayload;
  morningRepair: MetricPayload;
  afternoonRepair: MetricPayload;
  repairTotal: number | null;
  defectInventory: number;
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
  const consoleErrors: string[] = [];
  page.on('console', message => { if (message.type() === 'error') consoleErrors.push(message.text()); });
  await fs.mkdir(screenshotDirectory, { recursive: true });

  const initialHome = await getJson<{ today: string }>(request, '/api/g2/home', 'dev-sales');
  const today = initialHome.today;
  const tomorrow = addDays(today, 1);
  const dayAfterTomorrow = addDays(today, 2);
  const farFuture = '2200-01-02';

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
    headers: devHeaders('dev-sales'),
    data: {
      morningProduction: { quantity: 10, expectedVersion: null },
      afternoonProduction: { quantity: 4, expectedVersion: null },
      delivery: { quantity: 3, expectedVersion: null },
      defect: { quantity: 1, expectedVersion: null }
    }
  }));

  const competing = await Promise.all([
    request.put(`${apiBaseUrl}/api/g2/operations/${today}`, {
      headers: devHeaders('dev-sales'),
      data: { morningProduction: { quantity: 11, expectedVersion: 1 } }
    }),
    request.put(`${apiBaseUrl}/api/g2/operations/${today}`, {
      headers: devHeaders('dev-sales'),
      data: { morningProduction: { quantity: 12, expectedVersion: 1 } }
    })
  ]);
  expect(competing.map(response => response.status()).sort()).toEqual([200, 409]);

  todayData = await getDay(request, today, 'dev-sales');
  expect(todayData.morningProduction).not.toBeNull();
  const winningMorningVersion = todayData.morningProduction!.version;
  await Promise.all([
    expectOk(request.put(`${apiBaseUrl}/api/g2/operations/${today}`, {
      headers: devHeaders('dev-sales'),
      data: { morningProduction: { quantity: 13, expectedVersion: winningMorningVersion } }
    })),
    expectOk(request.put(`${apiBaseUrl}/api/g2/operations/${today}`, {
      headers: devHeaders('dev-sales'),
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
  await expectOk(request.put(`${apiBaseUrl}/api/g2/operations/${today}`, {
    headers: devHeaders('dev-logistics'),
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

  await expectOk(request.put(`${apiBaseUrl}/api/g2/operations/${tomorrow}`, {
    headers: devHeaders('dev-sales'),
    data: {
      morningProduction: { quantity: 2, expectedVersion: null },
      afternoonProduction: { quantity: 3, expectedVersion: null },
      delivery: { quantity: 8, expectedVersion: null },
      defect: { quantity: 2, expectedVersion: null }
    }
  }));
  await expectOk(request.put(`${apiBaseUrl}/api/g2/attendance/${tomorrow}`, {
    headers: devHeaders('dev-sales'),
    data: {
      morningEmiAttendance: { quantity: 7, expectedVersion: null },
      afternoonEmiAttendance: { quantity: 6, expectedVersion: null }
    }
  }));
  await expectStatus(request.put(`${apiBaseUrl}/api/g2/inventory-counts/${tomorrow}`, {
    headers: devHeaders('dev-sales'),
    data: { quantity: 99, expectedVersion: null }
  }), 400);
  await expectOk(request.put(`${apiBaseUrl}/api/g2/operations/${farFuture}`, {
    headers: devHeaders('dev-sales'),
    data: { morningProduction: { quantity: 1, expectedVersion: null } }
  }));
  await expectOk(request.put(`${apiBaseUrl}/api/g2/operations/${farFuture}`, {
    headers: devHeaders('dev-manufacturing'),
    data: { defect: { quantity: 4, expectedVersion: null } }
  }));
  const farFutureData = await getDay(request, farFuture, 'dev-viewer');
  expect(farFutureData.isForecast).toBe(true);
  expect(farFutureData.defect!.quantity).toBe(4);

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
  expect(tomorrowData.productionTotal).toBe(5);
  expect(tomorrowData.defect!.quantity).toBe(2);
  expect(tomorrowData.inventory).toBe(29);
  expect(tomorrowData.dailyProductionTarget!.quantity).toBe(24);
  expect(tomorrowData.deliveryTarget!.quantity).toBe(15);
  expect(tomorrowData.inventoryTarget!.quantity).toBe(18);
  expect((await getDay(request, dayAfterTomorrow, 'dev-viewer')).inventory).toBe(32);

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

  const previewLabel = `${koreanDate(tomorrow)} 신규 불량 임시 예상값`;
  await page.getByRole('button', { name: '불량 상세 보기', exact: true }).click();
  const previewBefore = await getDay(request, tomorrow, 'dev-sales');
  const centeredPreviewInput = page.getByLabel(previewLabel);
  await expect(centeredPreviewInput).toHaveCSS('appearance', 'textfield');
  const previewInputGaps = await centeredPreviewInput.evaluate(element => {
    const inputBounds = element.getBoundingClientRect();
    const cellBounds = element.closest('td')!.getBoundingClientRect();
    return {
      left: inputBounds.left - cellBounds.left,
      right: cellBounds.right - inputBounds.right
    };
  });
  expect(Math.abs(previewInputGaps.left - previewInputGaps.right)).toBeLessThanOrEqual(1);
  await page.getByLabel(previewLabel).fill('5');
  const productionTable = page.getByRole('table', { name: '생산 현황' });
  const inventoryRow = productionTable.getByRole('rowheader', { name: '재고', exact: true }).locator('..');
  const tomorrowColumn = (await productionTable.locator('thead th').allTextContents()).findIndex(value => value.includes(koreanDate(tomorrow)));
  const dayAfterTomorrowColumn = (await productionTable.locator('thead th').allTextContents()).findIndex(value => value.includes(koreanDate(dayAfterTomorrow)));
  expect(tomorrowColumn).toBeGreaterThan(0);
  expect(dayAfterTomorrowColumn).toBeGreaterThan(0);
  await expect(inventoryRow.locator('td').nth(tomorrowColumn - 1)).toHaveText('29');
  await expect(inventoryRow.locator('td').nth(dayAfterTomorrowColumn - 1)).toHaveText('29');
  expect((await getDay(request, tomorrow, 'dev-sales')).defect!.quantity).toBe(previewBefore.defect!.quantity);
  await page.reload();
  await page.getByRole('button', { name: '불량 상세 보기', exact: true }).click();
  await expect(page.getByLabel(previewLabel)).toHaveValue('2');
  expect((await getDay(request, tomorrow, 'dev-sales')).inventory).toBe(29);
  expect((await getDay(request, dayAfterTomorrow, 'dev-sales')).inventory).toBe(32);
  await capture(page, '01-g2-home-desktop-1440.png');

  await page.goto('/g2/operations');
  await expect(page.getByRole('heading', { name: '생산/출하 관리' })).toBeVisible();
  await expect(page.getByLabel('입력 날짜')).toHaveValue(today);
  await page.getByLabel('입력 날짜').fill(tomorrow);
  await expect(page.getByText('미래 날짜의 예상 수량을 입력하고 있습니다.')).toBeVisible();
  await page.getByLabel('일일 납품량').fill('0');
  await page.getByLabel('일일 불량 수량').fill('3');
  await page.getByRole('button', { name: '변경한 값 저장' }).click();
  await expect(page.getByText('생산·납품 수량을 저장했습니다.')).toBeVisible();
  expect((await getDay(request, tomorrow, 'dev-sales')).delivery!.quantity).toBe(0);
  expect((await getDay(request, tomorrow, 'dev-sales')).defect!.quantity).toBe(3);
  const beforeRepair = await getDay(request, tomorrow, 'dev-sales');
  const nextBeforeRepair = await getDay(request, dayAfterTomorrow, 'dev-sales');
  await page.getByLabel('오전 수리량').fill('1');
  await page.getByLabel('오후 수리량').fill('1');
  const repairSave = page.waitForResponse(response => response.url().includes(`/api/g2/operations/${tomorrow}`) && response.request().method() === 'PUT');
  await page.getByRole('button', { name: '변경한 값 저장' }).click();
  expect((await repairSave).ok()).toBeTruthy();
  await expect(page.getByText('생산·납품 수량을 저장했습니다.')).toBeVisible();
  const afterRepair = await getDay(request, tomorrow, 'dev-sales');
  expect(afterRepair.repairTotal).toBe(2);
  expect(afterRepair.inventory).toBe(beforeRepair.inventory);
  expect(afterRepair.defectInventory).toBe(beforeRepair.defectInventory - 2);
  expect((await getDay(request, dayAfterTomorrow, 'dev-sales')).inventory).toBe(nextBeforeRepair.inventory! + 2);
  await page.reload();
  await page.getByLabel('입력 날짜').fill(tomorrow);
  await expect(page.getByLabel('오전 수리량')).toHaveValue('1');
  await expect(page.getByLabel('오후 수리량')).toHaveValue('1');

  await page.getByLabel('개발 사용자').selectOption('dev-manufacturing');
  await page.goto('/g2/operations');
  await expect(page.getByLabel('오전 생산량')).toBeEnabled();
  await expect(page.getByLabel('일일 납품량')).toBeDisabled();
  await expect(page.getByLabel('일일 불량 수량')).toBeEnabled();
  await expect(page.getByLabel('오전 수리량')).toBeEnabled();
  await expect(page.getByLabel('오후 수리량')).toBeEnabled();

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('heading', { name: '생산/출하 관리' })).toBeVisible();
  const operationsRange = page.getByRole('group', { name: '입력 현황 표시 기간' });
  await expectInputWidth(operationsRange.getByLabel('시작일'), 120);
  await expectInputWidth(operationsRange.getByLabel('종료일'), 120);
  await assertNoPageOverflow(page);
  await capture(page, '02-g2-operations-mobile-390.png');

  await page.goto('/g2');
  await expect(page.getByRole('heading', { name: 'G2 홈' })).toBeVisible();
  const homeRange = page.getByRole('group', { name: '홈 표시 기간' });
  await page.getByRole('button', { name: '불량 상세 보기', exact: true }).click();
  await expectInputWidth(homeRange.getByLabel('시작일'), 120);
  await expectInputWidth(homeRange.getByLabel('종료일'), 120);
  await expectInputWidth(page.getByLabel('적용 시작일'), 136);
  await expect(page.getByLabel(previewLabel).locator('..')).toHaveClass(/g2-forecast-column/u);
  await expect(page.getByLabel(previewLabel)).toHaveCSS('color', 'rgb(37, 99, 235)');
  await expect(page.getByLabel(`${koreanDate(firstWeekendInMonth(today))} 신규 불량 임시 예상값`)).toHaveCSS('color', 'rgb(220, 38, 38)');
  await page.getByRole('button', { name: '수리 상세 보기', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByLabel(`${koreanDate(tomorrow)} 오전 수리 임시 예상값`)).toHaveValue('1');
  await page.getByLabel(`${koreanDate(tomorrow)} 오전 수리 임시 예상값`).fill('2');
  expect((await getDay(request, tomorrow, 'dev-sales')).morningRepair!.quantity).toBe(1);
  await page.getByRole('button', { name: `${koreanDate(tomorrow)} 수리 3대 상세 접기`, exact: true }).click();
  await expect(page.getByLabel(`${koreanDate(tomorrow)} 오전 수리 임시 예상값`)).toHaveCount(0);
  await assertNoPageOverflow(page);
  await capture(page, '03-g2-home-mobile-390.png');

  await page.goto('/g2/attendance');
  await expect(page.getByRole('heading', { name: '제조 인원 출근 관리' })).toBeVisible();
  const attendanceRange = page.getByRole('group', { name: '출근 현황 표시 기간' });
  await expectInputWidth(attendanceRange.getByLabel('시작일'), 120);
  await expectInputWidth(attendanceRange.getByLabel('종료일'), 120);
  await expect(page.getByRole('button', { name: `${koreanDate(tomorrow)} 오전 합계 7명 세부 인원 보기` })).toHaveCSS('color', 'rgb(37, 99, 235)');
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
  await page.getByRole('button', { name: '불량재고 실사 입력', exact: true }).click();
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
  expect((await getDay(request, today, 'dev-sales')).defectInventoryCount).toBeNull();
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

function firstWeekendInMonth(value: string) {
  const [year, month] = value.split('-').map(Number);
  for (let day = 1; day <= 7; day += 1) {
    const candidate = new Date(Date.UTC(year, month - 1, day));
    if (candidate.getUTCDay() === 0 || candidate.getUTCDay() === 6) return candidate.toISOString().slice(0, 10);
  }
  throw new Error('The calendar month does not contain a weekend in its first seven days.');
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
