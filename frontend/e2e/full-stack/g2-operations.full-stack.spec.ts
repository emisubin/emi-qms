import fs from 'node:fs/promises';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { expect, test, type APIRequestContext, type APIResponse, type Page } from '@playwright/test';

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
  await provisionSyntheticG2MaterialCatalog();

  const initialHome = await getJson<{ today: string }>(request, '/api/g2/home', 'dev-sales');
  const today = initialHome.today;
  const tomorrow = addDays(today, 1);
  const dayAfterTomorrow = addDays(today, 2);

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

  const homeDate = addDays(today, -1);
  await expectOk(request.put(`${apiBaseUrl}/api/g2/operations/${homeDate}`, {
    headers: devHeaders('dev-admin'),
    data: {
      morningProduction: { quantity: 10, expectedVersion: null },
      afternoonProduction: { quantity: 8, expectedVersion: null },
      delivery: { quantity: 4, expectedVersion: null },
      defect: { quantity: 1, expectedVersion: null }
    }
  }));
  await expectOk(request.put(`${apiBaseUrl}/api/g2/attendance/${homeDate}`, {
    headers: devHeaders('dev-manufacturing'),
    data: {
      morningEmiAttendance: { quantity: 6, expectedVersion: null },
      morningContractorAttendance: { quantity: 2, expectedVersion: null },
      afternoonEmiAttendance: { quantity: 5, expectedVersion: null },
      afternoonContractorAttendance: { quantity: 1, expectedVersion: null }
    }
  }));
  await expectOk(request.put(`${apiBaseUrl}/api/g2/inventory-counts/${homeDate}`, {
    headers: devHeaders('dev-sales'),
    data: { quantity: 20, expectedVersion: null }
  }));
  await expectOk(request.put(`${apiBaseUrl}/api/g2/materials/${homeDate}`, {
    headers: devHeaders('dev-materials'),
    data: {
      items: Array.from({ length: 92 }, (_, index) => ({
        id: index + 1,
        working: index === 0 ? 10 : null,
        kitting: null,
        rack: null,
        materials: null,
        erp: null,
        transit: null
      }))
    }
  }));

  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto('/g2');
  await page.getByLabel('개발 사용자').selectOption('dev-admin');
  await page.goto('/g2');
  await expect(page.getByRole('heading', { name: 'G2 홈' })).toBeVisible();
  const home = page.locator('#g2-home-scope');
  await expect(home.getByText('최근 작업일', { exact: true })).toBeVisible();
  await expect(home.getByText(homeDate.replaceAll('-', '.'), { exact: false })).toBeVisible();
  await expect(home.locator('input, textarea, select')).toHaveCount(0);
  await expect(home.getByRole('button', { name: '실사 입력', exact: true })).toHaveCount(0);
  await expect(home.getByRole('button', { name: '목표 저장', exact: true })).toHaveCount(0);
  await expect(home.getByRole('heading', { name: 'EMI 생산' })).toBeVisible();
  await expect(home.getByRole('heading', { name: 'LSE 납품' })).toBeVisible();
  await expect(home.getByRole('heading', { name: '불량률' })).toBeVisible();
  await expect(home.getByRole('heading', { name: '제품 재고 · 납품 전망' })).toBeVisible();
  await expect(home.getByRole('heading', { name: /안전재고 미달 자재/ })).toBeVisible();
  await expect(home.getByRole('img', { name: /실적 18대/ })).toBeVisible();
  await expect(home.getByRole('img', { name: /실적 4대/ })).toBeVisible();
  await expect(home.getByText('오전 8명', { exact: true })).toBeVisible();
  await expect(home.getByText('오후 6명', { exact: true })).toBeVisible();
  await expect(home.getByRole('table', { name: '안전재고 미달 자재' })).toContainText('합성 자재 1');
  await expect(page.getByText('손익관리', { exact: true })).toHaveCount(0);
  await capture(page, '01-g2-home-current-desktop-1440.png');

  await page.goto('/g2/operations');
  await expect(page.getByRole('heading', { name: '생산/출하 관리' })).toBeVisible();
  const productionTable = page.getByRole('table', { name: '월간 생산·출하 현황' });
  await expect(productionTable).toBeVisible();
  const todayRow = productionTable.locator(`tr[data-row="${today}"]`);
  await expect(todayRow).toContainText('18');
  await expect(todayRow).toContainText('4');
  await expect(todayRow).toContainText('20');
  await todayRow.getByRole('button', { name: `${today} 상세 펼치기` }).click();
  await expect(page.locator(`#g2-detail-${today}`).getByRole('table', { name: '생산·수리 실적' })).toBeVisible();

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

  const inventoryBeforeCount = (await getDay(request, tomorrow, 'dev-sales')).inventory;
  await expectStatus(request.put(`${apiBaseUrl}/api/g2/defect-inventory-counts/${today}`, {
    headers: devHeaders('dev-logistics'), data: { quantity: 0, expectedVersion: null }
  }), 403);
  await expectStatus(request.put(`${apiBaseUrl}/api/g2/defect-inventory-counts/${tomorrow}`, {
    headers: devHeaders('dev-sales'), data: { quantity: 0, expectedVersion: null }
  }), 400);
  await page.getByRole('button', { name: '데이터 입력' }).first().click();
  await page.getByRole('button', { name: '실사 입력' }).click();
  const countDialog = page.getByRole('dialog', { name: '실사 입력' });
  await expect(countDialog.locator('input[type="date"]')).toHaveValue(today);
  await expect(countDialog.getByLabel('납품 가능 재고 실사')).toHaveValue('20');
  await expect(countDialog.getByLabel('불량 재고 실사')).toHaveValue('');
  await countDialog.getByLabel('불량 재고 실사').fill('0');
  await countDialog.getByRole('button', { name: '저장' }).click();
  await expect(countDialog).toHaveCount(0);
  let countedDay = await getDay(request, today, 'dev-sales');
  expect(countedDay.defectInventoryCount?.quantity).toBe(0);
  expect(countedDay.defectInventoryCount?.version).toBe(1);
  expect(countedDay.defectInventory).toBe(0);
  expect(countedDay.defect?.quantity).toBe(1);
  await expectStatus(request.put(`${apiBaseUrl}/api/g2/defect-inventory-counts/${today}`, {
    headers: devHeaders('dev-sales'), data: { quantity: 9, expectedVersion: null }
  }), 409);

  await page.getByRole('button', { name: '데이터 입력' }).first().click();
  await page.getByRole('button', { name: '실사 입력' }).click();
  await expect(countDialog.getByLabel('불량 재고 실사')).toHaveValue('0');
  await countDialog.getByLabel('불량 재고 실사').fill('4');
  await countDialog.getByRole('button', { name: '저장' }).click();
  await expect(countDialog).toHaveCount(0);
  countedDay = await getDay(request, today, 'dev-sales');
  expect(countedDay.defectInventoryCount?.quantity).toBe(4);
  expect(countedDay.defectInventoryCount?.version).toBe(2);

  await page.getByRole('button', { name: '데이터 입력' }).first().click();
  await page.getByRole('button', { name: '실사 입력' }).click();
  await countDialog.getByLabel('불량 재고 실사').fill('');
  await countDialog.getByRole('button', { name: '저장' }).click();
  await expect(countDialog).toHaveCount(0);
  countedDay = await getDay(request, today, 'dev-sales');
  expect(countedDay.defectInventoryCount?.quantity).toBeNull();
  expect(countedDay.defectInventoryCount?.version).toBe(3);

  await page.getByRole('button', { name: '데이터 입력' }).first().click();
  await page.getByRole('button', { name: '실사 입력' }).click();
  await expect(countDialog.getByLabel('불량 재고 실사')).toHaveValue('');
  await expect(countDialog.getByRole('button', { name: '실사 삭제', exact: true })).toHaveCount(0);
  await countDialog.getByLabel('불량 재고 실사').fill('6');
  const recountRequest = page.waitForRequest(candidate => candidate.url().includes(`/api/g2/workspace/${today}`) && candidate.method() === 'PUT');
  await countDialog.getByRole('button', { name: '저장' }).click();
  expect((await recountRequest).postDataJSON()).toEqual({ defectInventoryCount: { quantity: 6, expectedVersion: 3 } });
  await expect(countDialog).toHaveCount(0);
  countedDay = await getDay(request, today, 'dev-sales');
  expect(countedDay.defectInventoryCount?.quantity).toBe(6);
  expect(countedDay.defectInventoryCount?.version).toBe(4);
  expect((await getDay(request, tomorrow, 'dev-sales')).inventory).toBe(inventoryBeforeCount);

  await selectG2OperationsMonth(page, tomorrow);
  const futureRow = productionTable.locator(`tr[data-row="${tomorrow}"]`);
  await expect(futureRow).toContainText('8');
  await expect(futureRow).toContainText('5');
  const futureCells = futureRow.locator('td:not(.week)');
  for (const index of [2, 3, 5, 6, 7]) await expect(futureCells.nth(index)).toBeEmpty();
  await futureRow.getByRole('button', { name: `${tomorrow} 상세 펼치기` }).click();
  await expect(page.locator(`#g2-detail-${tomorrow}`).getByText('다음 출하 계획')).toBeVisible();
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

  await page.goto('/g2/attendance');
  await expect(page.getByRole('heading', { name: '제조 인원 출근 관리' })).toBeVisible();
  const attendanceTable = page.getByRole('table', { name: '월간 출근 현황' });
  await expect(attendanceTable).toBeVisible();
  const attendanceRow = attendanceTable.locator(`tr[data-row="${today}"]`);
  await expect(attendanceRow).toContainText('8');
  await expect(attendanceRow).toContainText('6');
  await expect(attendanceRow).toContainText('14');
  await attendanceRow.getByRole('button', { name: `${today} 출근 인원 수정` }).click();
  const attendanceEditor = page.getByRole('dialog', { name: '출근 인원 입력' });
  await expect(attendanceEditor.getByLabel('입력 날짜')).toHaveValue(today);
  await expect(attendanceEditor.getByRole('tabpanel').getByLabel('EMI')).toHaveValue('6');
  await expect(attendanceEditor.getByRole('tabpanel').getByLabel('도급')).toHaveValue('2');
  await attendanceEditor.getByRole('tab', { name: '오후' }).click();
  await expect(attendanceEditor.getByRole('tabpanel').getByLabel('EMI')).toHaveValue('5');
  await expect(attendanceEditor.getByRole('tabpanel').getByLabel('도급')).toHaveValue('1');
  await capture(page, '02-g2-attendance-current-desktop-1440.png');
  await attendanceEditor.getByRole('button', { name: '취소' }).click();

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('heading', { name: '제조 인원 출근 관리' })).toBeVisible();
  await page.getByRole('button', { name: `${today} 출근 상세` }).click();
  const attendanceDetail = page.getByRole('dialog', { name: `${today.slice(5).replace('-', '.')} 출근 현황` });
  await expect(attendanceDetail.getByRole('table', { name: '오전 오후 출근 상세' })).toContainText('8');
  await expect(attendanceDetail.getByRole('table', { name: '오전 오후 출근 상세' })).toContainText('6');
  await expect(attendanceDetail.getByText('14명', { exact: true })).toBeVisible();
  await capture(page, '03-g2-attendance-current-mobile-390.png');
  await attendanceDetail.getByRole('button', { name: '닫기' }).click();
  await assertNoPageOverflow(page);

  await page.goto('/g2/operations');
  await expect(page.getByRole('heading', { name: '생산/출하 관리' })).toBeVisible();
  await selectG2OperationsMonth(page, tomorrow);
  await page.getByRole('button', { name: `${tomorrow} 상세 보기` }).click();
  const mobileDetail = page.getByRole('dialog');
  const deliveryProduction = mobileDetail.getByRole('heading', { name: '납품·생산 현황' }).locator('..').getByRole('table');
  await expect(deliveryProduction).toContainText('8');
  await expect(deliveryProduction).toContainText('5');
  await expect(mobileDetail.getByRole('table', { name: '재고·실사' }).locator('td')).toHaveText(['', '', '', '']);
  await expect(mobileDetail.getByText('다음 출하 계획')).toBeVisible();
  await capture(page, '04-g2-operations-current-mobile-390.png');
  await mobileDetail.getByRole('button', { name: '닫기' }).click();
  await assertNoPageOverflow(page);

  await page.goto('/g2');
  await expect(page.getByRole('heading', { name: 'G2 홈' })).toBeVisible();
  await expect(page.locator('#g2-home-scope input, #g2-home-scope textarea, #g2-home-scope select')).toHaveCount(0);
  await expect(page.getByText(homeDate.replaceAll('-', '.'), { exact: false })).toBeVisible();
  await assertNoPageOverflow(page);
  await capture(page, '05-g2-home-current-mobile-390.png');

  await page.setViewportSize({ width: 1440, height: 960 });
  await page.getByLabel('개발 사용자').selectOption('dev-materials');
  await page.goto('/g2/materials');
  await expect(page.getByRole('heading', { name: '자재 재고 현황' })).toBeVisible();
  const materialsTable = page.getByRole('table', { name: '자재 재고 현황' });
  await expect(materialsTable).toBeVisible();
  await expect(materialsTable.getByRole('button')).toHaveCount(92);
  await expect(page.getByRole('navigation', { name: '자재 구분' }).getByRole('button', { name: '전체' })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByRole('button', { name: '저장', exact: true })).toBeDisabled();
  await materialsTable.getByRole('button').first().click();
  const itemInfo = page.getByRole('dialog', { name: '품목 정보' });
  await expect(itemInfo.getByRole('table', { name: '포장단위별 인입수량' })).toBeVisible();
  await capture(page, '06-g2-materials-current-desktop-1440.png');
  await itemInfo.getByRole('button', { name: '품목 정보 닫기' }).click();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/g2/materials');
  await expect(page.getByRole('heading', { name: '자재 재고 현황' })).toBeVisible();
  await expect(page.getByText('전체 92개', { exact: true })).toBeVisible();
  const materialItems = page.locator('#list button.item');
  await expect(materialItems).toHaveCount(92);
  await materialItems.first().click();
  const materialDetail = page.getByRole('dialog', { name: '자재 상세' });
  await expect(materialDetail.getByRole('button', { name: '재고 현황' })).toHaveAttribute('aria-pressed', 'true');
  await materialDetail.getByRole('button', { name: '품목 정보' }).click();
  await expect(materialDetail.getByRole('rowheader', { name: '1SET 수량' })).toBeVisible();
  await materialDetail.getByRole('button', { name: '실사 입력' }).click();
  const materialEditor = page.getByRole('dialog', { name: '실사 입력' });
  await expect(materialEditor.getByRole('group', { name: '제조 Part' })).toBeVisible();
  await expect(materialEditor.getByRole('group', { name: '자재 Part' })).toBeVisible();
  await expect(materialEditor.getByRole('status')).toContainText('변경한 값은 자동 저장됩니다');
  await capture(page, '07-g2-materials-current-mobile-390.png');
  await materialEditor.getByRole('button', { name: '입력 닫기' }).click();
  await materialDetail.getByRole('button', { name: '닫기' }).click();
  await assertNoPageOverflow(page);
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

async function selectG2OperationsMonth(page: Page, date: string) {
  await page.getByRole('button', { name: '조회 연월 선택' }).click();
  const dialog = page.getByRole('dialog', { name: '조회 연월 선택' });
  await dialog.getByLabel('조회 연도').selectOption(date.slice(0, 4));
  await dialog.getByLabel('조회 월').selectOption(String(Number(date.slice(5, 7))));
  await dialog.getByRole('button', { name: '조회', exact: true }).click();
  await expect(page.getByRole('button', { name: `${date} 상세 펼치기` }).or(page.getByRole('button', { name: `${date} 상세 보기` }))).toBeVisible();
}

async function provisionSyntheticG2MaterialCatalog() {
  expect(requireEnv('DATABASE_HOST')).toBe('127.0.0.1');
  expect(requireEnv('E2E_DATABASE_NAME')).toMatch(/^emi_qms_e2e_[a-z0-9_]+$/);
  expect(requireEnv('E2E_COMPOSE_PROJECT_NAME')).toMatch(/^emi-qms-e2e-[a-z0-9-]+$/);
  expect(requireEnv('E2E_POSTGRES_SERVICE')).toBe('e2e-postgres');
  const table = queryDatabase("select coalesce(to_regclass('public.g2_material_items')::text, '')");
  if (!table) {
    const migrationRoot = path.resolve(process.cwd(), '..', 'database', 'business-migrations', 'cheongju');
    const schema = await fs.readFile(path.join(migrationRoot, '0133_g2_material_inventory.sql'), 'utf8');
    const overrides = await fs.readFile(path.join(migrationRoot, '0134_g2_material_calculation_overrides.sql'), 'utf8');
    applyDatabaseSql(`${schema}\n${overrides}`);
  }
  expect(queryDatabase('select count(*) from g2_material_items')).toBe('0');
  applyDatabaseSql(`
    insert into g2_material_items(id, source_row, metadata)
    select id, id + 3, jsonb_build_object(
      'id', id,
      'sourceRow', id + 3,
      'category', case when id <= 30 then '사급' when id <= 61 then '도급' else '부자재' end,
      'vendor', '합성 거래처',
      'name', format('합성 자재 %s', id),
      'code', format('SYN-%s', lpad(id::text, 3, '0')),
      'spec', format('합성 규격 %s', id),
      'unit', 'EA',
      'setQuantity', 1,
      'packBox', '10',
      'packPalletEa', '100',
      'packPalletBox', '10',
      'packNote', '합성 전용 fixture',
      'imageKeys', jsonb_build_array(),
      'cellFills', jsonb_build_object()
    )
    from generate_series(1, 92) as fixture(id);
  `);
  expect(queryDatabase('select count(*) from g2_material_items')).toBe('92');
}

function applyDatabaseSql(sql: string) {
  execFileSync('docker', [
    'compose', '--project-name', requireEnv('E2E_COMPOSE_PROJECT_NAME'), '--file', requireEnv('E2E_COMPOSE_FILE'),
    'exec', '-T', requireEnv('E2E_POSTGRES_SERVICE'), 'psql', '--username', requireEnv('E2E_DATABASE_USER'),
    '--dbname', requireEnv('E2E_DATABASE_NAME'), '--no-psqlrc', '--set', 'ON_ERROR_STOP=1', '--file', '-'
  ], { input: sql, encoding: 'utf8' });
}

function queryDatabase(sql: string) {
  return execFileSync('docker', [
    'compose', '--project-name', requireEnv('E2E_COMPOSE_PROJECT_NAME'), '--file', requireEnv('E2E_COMPOSE_FILE'),
    'exec', '-T', requireEnv('E2E_POSTGRES_SERVICE'), 'psql', '--username', requireEnv('E2E_DATABASE_USER'),
    '--dbname', requireEnv('E2E_DATABASE_NAME'), '--no-psqlrc', '--tuples-only', '--no-align',
    '--set', 'ON_ERROR_STOP=1', '--command', sql
  ], { encoding: 'utf8' }).trim().split('\n').at(-1)?.trim() ?? '';
}

function requireEnv(name: string) {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is required for isolated full-stack validation.`);
  return value;
}

async function capture(page: Page, filename: string) {
  await page.screenshot({ path: path.join(screenshotDirectory, filename), animations: 'disabled', fullPage: true });
}

async function assertNoPageOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBe(0);
}
