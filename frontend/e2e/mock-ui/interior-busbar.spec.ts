import { expect, test, type Page } from "@playwright/test";
import type { BusbarWorkspace } from "../../src/interiorBusbar";
const familyId = "00000000-0000-0000-0000-000000000001",
  materialId = "00000000-0000-0000-0000-000000000002",
  workerId = "00000000-0000-0000-0000-000000000003",
  productId = "00000000-0000-0000-0000-000000000004",
  projectId = "00000000-0000-0000-0000-000000000005",
  planId = "00000000-0000-0000-0000-000000000007";
const png = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1cAAAAASUVORK5CYII=",
  "base64",
);
function fixture(canWrite = true): BusbarWorkspace {
  return {
    canWrite,
    settings: { commonProjectCode: "SYN-INTERIOR" },
    productFamilies: [
      {
        id: familyId,
        code: "SYN-A",
        name: "합성 제품군 A",
        isActive: true,
        balance: 30,
        plannedQuantity: 60,
        producedQuantity: 1,
      },
    ],
    materials: [
      {
        id: materialId,
        code: "SYN-M",
        name: "합성 동대",
        unit: "m",
        supplyType: "도급",
        isActive: true,
        balance: -2,
      },
    ],
    workers: [
      { id: workerId, code: "SYN-W", name: "합성 외주 작업자", isActive: true },
    ],
    boms: [
      {
        id: "bom",
        productFamilyId: familyId,
        version: 1,
        createdAtUtc: "2026-09-09T01:00:00Z",
      },
    ],
    bomLines: [{ bomId: "bom", materialId, quantity: 2 }],
    projects: [
      {
        id: projectId,
        name: "합성 납품 현장",
        customerJobNumber: "SYN-001",
        commonProjectCode: "SYN-INTERIOR",
        productFamilyId: familyId,
        requestedQuantity: 60,
        shippedQuantity: 0,
        destination: "합성 업체",
        dueDate: "2026-09-30",
      },
    ],
    plans: [
      {
        id: planId,
        productFamilyId: familyId,
        planDate: "2026-09-09",
        quantity: 60,
        actualQuantity: 1,
      },
    ],
    purchases: [],
    products: [
      {
        id: productId,
        planId,
        planSequence: 1,
        productFamilyId: familyId,
        workerId,
        workerName: "합성 외주 작업자",
        status: "Draft",
        hasFront: false,
        hasBack: false,
        revision: 0,
        publicationState: "Pending",
      },
    ],
    ledger: [],
    ledgerLines: [],
    shipments: [],
    receipts: [],
    pagination: { page: 1, pageSize: 100, productCount: 1, ledgerCount: 0 },
    publicationOutstandingCount: 0,
  };
}
async function mock(page: Page, data: BusbarWorkspace, denied = false) {
  const writes: Array<{ path: string; body: unknown }> = [];
  await page.route("http://localhost:5080/**", async (route) => {
    const req = route.request(),
      path = new URL(req.url()).pathname;
    const json = (body: unknown, status = 200) =>
      route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify(body),
      });
    if (req.method() === "OPTIONS") return route.fulfill({ status: 204 });
    if (path === "/health/ready")
      return json({
        status: "ok",
        database: { isReady: true, reason: "reachable" },
      });
    if (path === "/api/runtime-mode")
      return json({
        mode: "Development",
        mutationAllowed: true,
        reviewSafe: false,
        ready: true,
        databaseReadOnly: false,
      });
    if (path === "/api/me") {
      const principal = {
        userId: workerId,
        developmentUserKey: "dev-sales",
        displayName: "합성 담당자",
        email: null,
        authProvider: "Dev",
        isActive: true,
        approvalPending: false,
        department: "Sales",
        roles: data.canWrite ? ["interior-busbar-manager"] : [],
      };
      return json({
        ...principal,
        permissions: ["projects.read"],
        projectAccess: [],
        actualUser: principal,
        effectiveUser: principal,
        isTestUserSwitch: false,
        testUserKey: null,
        canUseAdminTestUserSwitch: false,
        businessUnitAccess: {
          status: "selected",
          selectedBusinessUnit: "CHEONGJU",
          allowedBusinessUnits: ["CHEONGJU"],
          isOverallAdministrator: false,
          errorCode: null,
        },
      });
    }
    if (path === "/api/interior-busbar/workspace")
      return denied
        ? json({ message: "청주에서만 사용할 수 있습니다." }, 403)
        : json(data);
    if (/^\/api\/interior-busbar\/products\/[^/]+$/.test(path) && req.method() === "GET")
      return json(data.products.find((p) => p.id === path.split("/").at(-1)));
    if (path.endsWith("/qr") && req.method() === "GET") return route.fulfill({ contentType: "image/png", body: png });
    if (path.includes("/photos/") && req.method() === "GET")
      return route.fulfill({ contentType: "image/png", body: png });
    if (path.startsWith("/api/interior-busbar/") && req.method() !== "GET") {
      if (!data.canWrite)
        return json({ message: "입력 권한이 없습니다." }, 403);
      writes.push({
        path,
        body: req.headers()["content-type"]?.includes("application/json")
          ? req.postDataJSON()
          : null,
      });
      if (
        path === `/api/interior-busbar/products/${productId}` &&
        req.method() === "PATCH"
      ) {
        const body = req.postDataJSON();
        data.products[0].workerId = body.workerId;
        data.products[0].workerName = data.workers.find(
          (worker) => worker.id === body.workerId,
        )!.name;
      }
      if (path === `/api/interior-busbar/products/${productId}/cancel`) {
        data.products[0].status = "Cancelled";
        data.products[0].revision++;
      }
      if (path.includes("/photos/")) {
        const p = data.products[0];
        const workerMatch = req
          .postDataBuffer()
          ?.toString()
          .match(/name="workerId"\r\n\r\n([^\r]+)/);
        if (workerMatch) {
          p.workerId = workerMatch[1];
          p.workerName =
            data.workers.find((worker) => worker.id === p.workerId)?.name ??
            null;
        }
        if (path.endsWith("/front")) p.hasFront = true;
        else p.hasBack = true;
        p.revision++;
        if (p.hasFront && p.hasBack) {
          p.status = "Complete";
          p.number = "IB-00000001";
          p.manufacturedAtUtc = "2026-09-09T06:00:00Z";
          data.productFamilies[0].balance = 31;
          data.materials[0].balance = -4;
        }
      }
      if (path.endsWith("/shipments")) {
        const body = req.postDataJSON();
        if (body.quantity > (data.productFamilies[0].balance ?? 0))
          return json({ message: "완제품 재고가 부족합니다." }, 409);
        data.projects[0].shippedQuantity += body.quantity;
        data.productFamilies[0].balance! -= body.quantity;
      }
      return json({ id: productId });
    }
    if (path === "/api/my-work/summary")
      return json({
        requestedCount: 0,
        inProgressCount: 0,
        completedCount: 0,
        blockingCount: 0,
        assignedProjectCount: 0,
        assignedProjectBreakdown: [],
      });
    if (path === "/api/notifications/summary")
      return json({ unreadCount: 0, blockingCount: 0 });
    if (path.includes("/site-access")) return json({ recorded: true });
    return json({ items: [], projects: [], canManage: false });
  });
  return writes;
}
async function selectPlanDate(page: Page, date: string) {
  if (await page.getByRole("dialog").count()) await page.getByRole("button", { name: "생산계획 팝업 닫기" }).click();
  await page.getByRole("button", { name: `${date} 생산계획 선택`, exact: true }).click();
}
async function selectPlanMonth(page: Page, month: string) {
  if (await page.getByRole("dialog").count()) await page.getByRole("button", { name: "생산계획 팝업 닫기" }).click();
  await page.getByLabel("계획 월", { exact: true }).fill(month);
}
test("six workspaces desktop and 390px without horizontal page overflow", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await mock(page, fixture());
  await page.goto("/interior-busbar");
  await expect(
    page.getByRole("heading", { name: "인테리어 부스바", exact: true }),
  ).toBeVisible();
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    for (const label of [
      "종합 현황",
      "납품 프로젝트",
      "생산계획",
      "생산·사진·QR",
      "구매·자재",
      "기준정보",
    ]) {
      await page.getByRole("tab", { name: label, exact: true }).click();
      await expect(page.locator(".busbar-page")).toBeVisible();
      expect(
        await page.evaluate(
          () =>
            document.documentElement.scrollWidth -
            document.documentElement.clientWidth,
        ),
      ).toBe(0);
      await page.screenshot({
        path: `/private/tmp/emi-busbar-${width}-${label.replaceAll("·", "-")}.png`,
        fullPage: true,
      });
    }
  }
  expect(errors).toEqual([]);
});
test("two photos auto complete with server time and block QR until publication", async ({
  page,
}) => {
  const data = fixture(),
    writes = await mock(page, data);
  await page.goto("/interior-busbar");
  await page.getByRole("tab", { name: "생산·사진·QR" }).click();
  await page.getByRole("button", { name: "사진 등록 계속하기" }).click();
  await page
    .getByLabel("앨범에서 앞면 선택")
    .setInputFiles({ name: "front.png", mimeType: "image/png", buffer: png });
  await expect(page.getByRole("img", { name: "앞면 등록 사진" })).toBeVisible();
  expect(data.products[0].status).toBe("Draft");
  await page
    .getByLabel("앨범에서 뒷면 선택")
    .setInputFiles({ name: "back.png", mimeType: "image/png", buffer: png });
  await expect(page.getByRole("cell", { name: "생산 완료", exact: true })).toBeVisible();
  expect(data.products[0].status).toBe("Complete");
  expect(writes.filter((x) => x.path.includes("/photos/"))).toHaveLength(2);
  await expect(
    page.getByRole("button", { name: "QR 인쇄 준비" }),
  ).toBeDisabled();
  await expect(page.getByLabel("카메라로 앞면 촬영")).toBeDisabled();
  await page.getByLabel("사진 정정 사유").fill("합성 사진 정정");
  await expect(page.getByLabel("카메라로 앞면 촬영")).toBeEnabled();
  await page.screenshot({
    path: "/private/tmp/emi-busbar-photo-complete.png",
    fullPage: true,
  });
});
test("server stock rejection stays visible and retry reuses operation ID", async ({
  page,
}) => {
  const data = fixture();
  const writes = await mock(page, data);
  await page.goto("/interior-busbar");
  await page.getByRole("tab", { name: "납품 프로젝트" }).click();
  await page.getByRole("button", { name: "합성 납품 현장" }).click();
  await page.getByRole("button", { name: "분할 출하", exact: true }).click();
  data.productFamilies[0].balance = 10; // Concurrent shipment after this form opened.
  await page.getByLabel("이번 출하 수량").fill("20");
  await page.getByRole("button", { name: "저장", exact: true }).click();
  await expect(page.getByText("완제품 재고가 부족합니다.")).toBeVisible();
  await page.getByRole("button", { name: "저장", exact: true }).click();
  await expect(page.getByText("완제품 재고가 부족합니다.")).toBeVisible();
  expect(writes).toHaveLength(2);
  expect((writes[0].body as { requestId: string }).requestId).toBe(
    (writes[1].body as { requestId: string }).requestId,
  );
});
test("read only users see no mutation controls", async ({ page }) => {
  const writes = await mock(page, fixture(false));
  await page.goto("/interior-busbar");
  await expect(
    page.getByText("조회 권한으로 접속했습니다.", { exact: false }),
  ).toBeVisible();
  for (const label of [
    "납품 프로젝트",
    "생산계획",
    "생산·사진·QR",
    "구매·자재",
    "기준정보",
  ])
    await page.getByRole("tab", { name: label, exact: true }).click();
  await expect(
    page.getByRole("button", { name: "외주 작업자 등록" }),
  ).toHaveCount(0);
  expect(writes).toHaveLength(0);
});
test("permission denial is distinguished from empty state", async ({
  page,
}) => {
  await mock(page, fixture(), true);
  await page.goto("/interior-busbar");
  await expect(page.getByText("청주에서만 사용할 수 있습니다.")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "다시 불러오기" }),
  ).toBeVisible();
});

test("BOM reload replaces stale editable quantities with the newest version", async ({
  page,
}) => {
  const data = fixture();
  const writes = await mock(page, data);
  await page.goto("/interior-busbar");
  await page.getByRole("tab", { name: "기준정보", exact: true }).click();
  await page.getByLabel("소요량을 관리할 제품군").selectOption(familyId);
  const quantity = page.getByLabel("합성 동대 (m)", { exact: true });
  await expect(quantity).toHaveValue("2");
  // A different manager publishes a new version while this form stays open.
  data.boms.push({
    id: "bom-new",
    productFamilyId: familyId,
    version: 2,
    createdAtUtc: "2026-09-09T02:00:00Z",
  });
  data.bomLines.push({ bomId: "bom-new", materialId, quantity: 5 });
  await page.getByRole("button", { name: "새로고침", exact: true }).click();
  await expect(page.getByText("현재 버전: 2", { exact: true })).toBeVisible();
  await expect(quantity).toHaveValue("5");
  await page.screenshot({
    path: "/private/tmp/emi-busbar-bom-reloaded.png",
    fullPage: true,
  });
  await page.getByRole("button", { name: "새 버전 저장", exact: true }).click();
  await expect(
    page.getByText("새 소요량 버전을 저장했습니다.", { exact: true }),
  ).toBeVisible();
  expect(writes).toHaveLength(1);
  expect(writes[0]).toEqual({
    path: "/api/interior-busbar/boms",
    body: { productFamilyId: familyId, lines: [{ materialId, quantity: 5 }] },
  });
});

test("draft products allow reasoned worker correction and cancellation without stock movement", async ({
  page,
}) => {
  const data = fixture();
  const correctedWorker = "00000000-0000-0000-0000-000000000006";
  data.workers.push({
    id: correctedWorker,
    code: "SYN-W2",
    name: "합성 정정 작업자",
    isActive: true,
  });
  const writes = await mock(page, data);
  await page.goto("/interior-busbar");
  await page.getByRole("tab", { name: "생산·사진·QR", exact: true }).click();
  await page
    .getByRole("button", { name: "사진 등록 계속하기", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "작업자 정정", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "생산 취소", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "작업자 정정", exact: true }).click();
  await page
    .getByRole("combobox", { name: "실제 제조 작업자", exact: true })
    .selectOption(correctedWorker);
  await expect(page.getByLabel("정정 사유", { exact: true })).toHaveAttribute(
    "required",
    "",
  );
  await page.getByRole("button", { name: "저장", exact: true }).click();
  expect(writes).toHaveLength(0);
  await page
    .getByLabel("정정 사유", { exact: true })
    .fill("합성 작업자 오선택 정정");
  await page.getByRole("button", { name: "저장", exact: true }).click();
  await expect(
    page.getByText("제조 작업자: 합성 정정 작업자 · 미완료", { exact: true }),
  ).toBeVisible();
  expect(writes[0]).toEqual({
    path: `/api/interior-busbar/products/${productId}`,
    body: { workerId: correctedWorker, reason: "합성 작업자 오선택 정정" },
  });
  await page.getByRole("button", { name: "생산 취소", exact: true }).click();
  await expect(
    page.getByText("미완료 등록을 취소합니다. 재고는 변경되지 않습니다.", {
      exact: true,
    }),
  ).toBeVisible();
  await expect(page.getByLabel("정정 사유", { exact: true })).toHaveAttribute(
    "required",
    "",
  );
  await page.getByRole("button", { name: "저장", exact: true }).click();
  expect(writes).toHaveLength(1);
  await page
    .getByLabel("정정 사유", { exact: true })
    .fill("중복 촬영 준비 취소");
  await page.getByRole("button", { name: "저장", exact: true }).click();
  await expect(page.getByRole("cell", { name: "취소", exact: true })).toBeVisible();
  expect(writes[1].path).toBe(
    `/api/interior-busbar/products/${productId}/cancel`,
  );
  expect(writes[1].body).toEqual({
    reason: "중복 촬영 준비 취소",
    requestId: expect.any(String),
  });
  expect(data.products[0].manufacturedAtUtc).toBeUndefined();
  await expect(
    page.getByRole("button", { name: "작업자 정정", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "생산 취소", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "게시 다시 요청", exact: true }),
  ).toHaveCount(0);
  expect(
    writes.filter((write) => /adjustments|shipments|receipts/.test(write.path)),
  ).toHaveLength(0);
});

test("planned draft requires worker before uploads and shows permanent number only after both photos", async ({
  page,
}) => {
  const data = fixture();
  data.products[0].workerId = null;
  data.products[0].workerName = null;
  await mock(page, data);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/interior-busbar");
  await page.getByRole("tab", { name: "생산계획", exact: true }).click();
  await selectPlanMonth(page, "2026-09");
  await selectPlanDate(page, "2026-09-09");
  const plannedRequest = page.waitForRequest(
    (request) =>
      request.url().includes("/workspace?") &&
      request.url().includes(`productFamilyId=${familyId}`) && request.url().includes("planDateFrom=2026-09-09"),
  );
  await page.getByRole("button", { name: "합성 제품군 A 2026-09-09 제품 보기", exact: true }).click();
  await plannedRequest;
  await expect(
    page.getByRole("button", { name: "새 제품 사진 등록", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByText("2026-09-09 · 대기 1번", { exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "사진 등록 계속하기", exact: true })
    .click();
  await expect(page.getByLabel("앨범에서 앞면 선택")).toBeDisabled();
  await expect(page.getByLabel("카메라로 뒷면 촬영")).toBeDisabled();
  await page
    .getByRole("combobox", {
      name: "촬영 제품의 실제 제조 작업자",
      exact: true,
    })
    .selectOption(workerId);
  await expect(page.getByLabel("앨범에서 앞면 선택")).toBeEnabled();
  const upload = page.waitForRequest(
    (request) =>
      request.method() === "PUT" && request.url().endsWith("/photos/front"),
  );
  await page
    .getByLabel("앨범에서 앞면 선택")
    .setInputFiles({ name: "front.png", mimeType: "image/png", buffer: png });
  const uploadRequest = await upload;
  expect(uploadRequest.postDataBuffer()?.toString()).toContain(
    'name="workerId"',
  );
  expect(uploadRequest.postDataBuffer()?.toString()).toContain(workerId);
  await expect(page.getByRole("img", { name: "앞면 등록 사진" })).toBeVisible();
  expect(data.products[0].number).toBeUndefined();
  await page.screenshot({
    path: "/private/tmp/emi-busbar-plan-draft-390.png",
    fullPage: true,
  });
  await page
    .getByLabel("앨범에서 뒷면 선택")
    .setInputFiles({ name: "back.png", mimeType: "image/png", buffer: png });
  await expect(page.locator(".busbar-completion")).toBeFocused();
  await expect(page.locator(".busbar-completion strong")).toHaveText(
    "IB-00000001",
  );
  await expect(
    page.getByRole("button", { name: "QR 인쇄 준비", exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByText("이 번호를 임시 스티커에 표시하세요.", { exact: false }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () =>
        document.documentElement.scrollWidth -
        document.documentElement.clientWidth,
    ),
  ).toBe(0);
  await page.screenshot({
    path: "/private/tmp/emi-busbar-plan-complete-390.png",
    fullPage: true,
  });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.screenshot({
    path: "/private/tmp/emi-busbar-plan-complete-1440.png",
    fullPage: true,
  });
});

test("calendar plan retry retains ID and saves without leaving selected date", async ({
  page,
}) => {
  const data = fixture();
  await mock(page, data);
  const submitted: Array<{
    id: string;
    productFamilyId: string;
    planDate: string;
    quantity: number;
  }> = [];
  await page.route(
    "http://localhost:5080/api/interior-busbar/plans",
    async (route) => {
      const body = route.request().postDataJSON();
      submitted.push(body);
      if (submitted.length === 1)
        return route.fulfill({
          status: 503,
          contentType: "application/json",
          body: JSON.stringify({ message: "합성 일시적 오류" }),
        });
      data.plans.push({ ...body, actualQuantity: 0 });
      data.products = [
        {
          id: productId,
          planId: body.id,
          planSequence: 1,
          productFamilyId: familyId,
          workerId: null,
          workerName: null,
          status: "Draft",
          hasFront: false,
          hasBack: false,
          revision: 0,
        },
      ];
      return route.fulfill({
        contentType: "application/json",
        body: JSON.stringify({ id: body.id }),
      });
    },
  );
  await page.goto("/interior-busbar");
  await page.getByRole("tab", { name: "생산계획", exact: true }).click();
  await selectPlanMonth(page, "2026-09");
  await selectPlanDate(page, "2026-09-09");
  await selectPlanDate(page, "2026-09-11");
  await page.getByRole("button", { name: "합성 제품군 A 2026-09-11 계획 등록", exact: true }).click();
  await expect(page.getByRole("combobox", { name: "제품군", exact: true })).toHaveValue(familyId);
  await expect(page.getByRole("combobox", { name: "제품군", exact: true })).toBeDisabled();
  await expect(page.getByLabel("생산일", { exact: true })).toHaveValue("2026-09-11");
  await expect(page.getByLabel("생산일", { exact: true })).toBeDisabled();
  await page.getByLabel("목표 수량", { exact: true }).fill("1");
  await page.getByRole("button", { name: "저장", exact: true }).click();
  await expect(
    page.getByText("합성 일시적 오류", { exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "저장", exact: true }).click();
  await expect(page.getByRole("tab", { name: "생산계획", exact: true })).toHaveAttribute("aria-selected", "true");
  await expect(page.getByRole("button", { name: "2026-09-11 생산계획 선택", exact: true })).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByRole("button", { name: "2026-09-11 생산계획 선택", exact: true })).toContainText("계획 1개 · 완료 0개");
  expect(submitted).toHaveLength(2);
  expect(submitted[0].id).toMatch(/^[0-9a-f-]{36}$/);
  expect(submitted[1]).toEqual(submitted[0]);
  await page
    .getByRole("button", { name: "합성 제품군 A 2026-09-11 계획 수정", exact: true })
    .click();
  await expect(
    page.getByRole("combobox", { name: "제품군", exact: true }),
  ).toBeDisabled();
  await expect(page.getByLabel("생산일", { exact: true })).toBeDisabled();
  await expect(page.getByLabel("목표 수량", { exact: true })).toBeEnabled();
});

for (const navigation of ["filter", "plan row"] as const) {
  test(`photo upload blocks closing and later navigation preserves the selected plan through ${navigation}`, async ({
    page,
  }) => {
    const data = fixture();
    const otherPlanId = "00000000-0000-0000-0000-000000000008";
    const otherProductId = "00000000-0000-0000-0000-000000000009";
    data.plans.push({
      id: otherPlanId,
      productFamilyId: familyId,
      planDate: "2026-09-12",
      quantity: 1,
      actualQuantity: 0,
    });
    data.products.push({
      ...data.products[0],
      id: otherProductId,
      planId: otherPlanId,
    });
    await mock(page, data);
    const workspacePlans: string[] = [];
    await page.route(
      "http://localhost:5080/api/interior-busbar/workspace?**",
      async (route) => {
        const selected =
          new URL(route.request().url()).searchParams.get("planDateFrom") ?? "";
        workspacePlans.push(selected);
        const products = data.products.filter(
          (product) => !selected || data.plans.find((plan) => plan.id === product.planId)?.planDate === selected,
        );
        await route.fulfill({
          contentType: "application/json",
          body: JSON.stringify({
            ...data,
            products,
            pagination: { ...data.pagination, productCount: products.length },
          }),
        });
      },
    );
    let releaseUpload!: () => void;
    const uploadGate = new Promise<void>((resolve) => {
      releaseUpload = resolve;
    });
    await page.route(
      `http://localhost:5080/api/interior-busbar/products/${productId}/photos/front`,
      async (route) => {
        await uploadGate;
        data.products[0].hasFront = true;
        data.products[0].revision++;
        await route.fulfill({
          contentType: "application/json",
          body: JSON.stringify({ id: productId }),
        });
      },
    );
    await page.goto("/interior-busbar");
    await page.getByRole("tab", { name: "생산·사진·QR", exact: true }).click();
    await page.getByLabel("계획 시작일", { exact: true }).fill("2026-09-09");
    await page.getByLabel("계획 종료일", { exact: true }).fill("2026-09-09");
    await expect(
      page.getByRole("button", { name: "사진 등록 계속하기", exact: true }),
    ).toHaveCount(1);
    await page
      .getByRole("button", { name: "사진 등록 계속하기", exact: true })
      .click();
    const started = page.waitForRequest(
      (request) =>
        request.url().endsWith(`/products/${productId}/photos/front`) &&
        request.method() === "PUT",
    );
    await page
      .getByLabel("앨범에서 앞면 선택")
      .setInputFiles({ name: "front.png", mimeType: "image/png", buffer: png });
    await started;
    await expect(page.getByRole("button", { name: "사진 팝업 닫기" })).toBeDisabled();
    await page.keyboard.press("Escape");
    await expect(page.getByRole("dialog")).toBeVisible();
    releaseUpload();
    await expect(page.locator(".busbar-page")).toHaveAttribute("aria-busy", "false");
    await page.getByRole("button", { name: "사진 팝업 닫기" }).click();
    if (navigation === "filter") {
      await page.getByLabel("계획 종료일", { exact: true }).fill("2026-09-12");
      await page.getByLabel("계획 시작일", { exact: true }).fill("2026-09-12");
    } else {
      await page.getByRole("tab", { name: "생산계획", exact: true }).click();
      await selectPlanMonth(page, "2026-09");
      await selectPlanDate(page, "2026-09-12");
      await expect(page.getByRole("button", { name: "합성 제품군 A 2026-09-12 제품 보기", exact: true })).toBeEnabled();
      releaseUpload();
      await page
        .getByRole("button", { name: "합성 제품군 A 2026-09-12 제품 보기", exact: true })
        .click();
    }
    await expect(
      page.getByText("2026-09-12 · 대기 1번", { exact: true }),
    ).toBeVisible();
    releaseUpload();
    await expect(page.locator(".busbar-page")).toHaveAttribute(
      "aria-busy",
      "false",
    );
    await expect(
      page.getByLabel("계획 시작일", { exact: true }),
    ).toHaveValue("2026-09-12");
    await expect(
      page.getByText("2026-09-12 · 대기 1번", { exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText("2026-09-09 · 대기 1번", { exact: true }),
    ).toHaveCount(0);
    expect(workspacePlans.at(-1)).toBe("2026-09-12");
  });
}

test("project row exposes family shipment context without aggregate KPI", async ({ page }) => {
  await mock(page, fixture());
  await page.goto("/interior-busbar");
  await expect(page.locator(".busbar-page .ds-kpi-grid")).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "제품군별 생산·납품 현황" })).toBeVisible();
  await page.getByRole("tab", { name: "납품 프로젝트", exact: true }).click();
  const row = page.getByRole("row").filter({ hasText: "합성 납품 현장" });
  await row.getByRole("cell", { name: "합성 업체", exact: true }).click();
  await expect(row).toHaveAttribute("aria-expanded", "true");
  await expect(page.locator(".busbar-shippable")).toHaveText("현재 최대 출하 가능30개");
  await expect(page.getByRole("heading", { name: "해당 제품군 날짜별 생산계획" })).toBeVisible();
  await expect(page.getByRole("cell", { name: "59개", exact: true })).toBeVisible();
  await row.getByRole("button", { name: "정정", exact: true }).click();
  await expect(row).toHaveAttribute("aria-expanded", "true");
  await page.getByRole("button", { name: "닫기", exact: true }).click();
  await row.focus(); await page.keyboard.press("Enter");
  await expect(row).toHaveAttribute("aria-expanded", "false");
  await page.keyboard.press("Space");
  await expect(row).toHaveAttribute("aria-expanded", "true");
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await page.screenshot({ path: `/private/tmp/emi-busbar-project-context-${width}.png`, fullPage: true });
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
  }
});

test("monthly calendar selection and photo filters send server-side conditions", async ({ page }) => {
  const data = fixture();
  data.productFamilies.push({ id: "family-b", name: "합성 제품군 B", code: "SYN-B", isActive: true });
  await mock(page, data);
  await page.goto("/interior-busbar");
  await page.getByRole("tab", { name: "생산계획", exact: true }).click();
  await selectPlanMonth(page, "2026-09");
  await expect(page.getByRole("button", { name: "2026-09-09 생산계획 선택" })).toContainText("계획 60개 · 완료 1개");
  await page.getByRole("button", { name: "다음 달", exact: true }).click();
  await expect(page.getByLabel("계획 월")).toHaveValue("2026-10");
  await page.getByRole("button", { name: "이전 달", exact: true }).click();
  await selectPlanDate(page, "2026-09-09");
  await page.getByRole("button", { name: "생산계획 팝업 닫기" }).click();
  await page.getByLabel("계획 제품군", { exact: true }).selectOption(familyId);
  await selectPlanDate(page, "2026-09-09");
  await expect(page.getByRole("cell", { name: "합성 제품군 B", exact: true })).toHaveCount(0);
  await page.getByRole("button", { name: "합성 제품군 A 2026-09-09 제품 보기" }).click();
  await expect(page.getByLabel("제품군 필터")).toHaveValue(familyId);
  await expect(page.getByLabel("계획 시작일")).toHaveValue("2026-09-09");
  await expect(page.getByLabel("계획 종료일")).toHaveValue("2026-09-09");
  const requested = page.waitForRequest((r) => r.url().includes("/workspace?") && r.url().includes("status=Draft"));
  await page.getByLabel("생산 상태 필터").selectOption("Draft");
  const params = new URL((await requested).url()).searchParams;
  expect(params.get("productFamilyId")).toBe(familyId);
  expect(params.get("planDateFrom")).toBe("2026-09-09");
  expect(params.get("planDateTo")).toBe("2026-09-09");
  expect(params.get("page")).toBe("1");
  expect(params.has("planId")).toBe(false);
});


test("completing a Draft-filtered product preserves its number after it leaves the list", async ({ page }) => {
  const data = fixture();
  await mock(page, data);
  await page.route("http://localhost:5080/api/interior-busbar/workspace?**", async (route) => {
    const status = new URL(route.request().url()).searchParams.get("status");
    const products = data.products.filter((product) => !status || product.status === status);
    await route.fulfill({ contentType: "application/json", body: JSON.stringify({ ...data, products, pagination: { ...data.pagination, productCount: products.length } }) });
  });
  await page.goto("/interior-busbar");
  await page.getByRole("tab", { name: "생산·사진·QR", exact: true }).click();
  await page.getByLabel("생산 상태 필터", { exact: true }).selectOption("Draft");
  await page.getByRole("button", { name: "사진 등록 계속하기", exact: true }).click();
  await page.getByLabel("앨범에서 앞면 선택").setInputFiles({ name: "front.png", mimeType: "image/png", buffer: png });
  await expect(page.getByRole("img", { name: "앞면 등록 사진" })).toBeVisible();
  await page.getByLabel("앨범에서 뒷면 선택").setInputFiles({ name: "back.png", mimeType: "image/png", buffer: png });
  await expect(page.getByRole("dialog").locator(".busbar-completion strong")).toHaveText("IB-00000001");
  await expect(page.locator(".busbar-completion")).toBeFocused();
  await expect(page.getByLabel("생산 상태 필터", { exact: true })).toHaveValue("Draft");
  await expect(page.getByRole("button", { name: "사진 등록 계속하기", exact: true })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "QR 인쇄 준비", exact: true })).toBeDisabled();
  await page.getByRole("button", { name: "사진 팝업 닫기" }).click();
  await expect(page.getByRole("heading", { name: "생산 제품 목록", exact: true })).toBeFocused();

});

test("calendar month boundaries and new plans preserve selected family and date", async ({ page }) => {
  const data = fixture();
  await mock(page, data);
  const submitted: Array<{ id: string; productFamilyId: string; planDate: string; quantity: number }> = [];
  await page.route("http://localhost:5080/api/interior-busbar/plans", async (route) => {
    const body = route.request().postDataJSON();
    submitted.push(body);
    const existing = data.plans.findIndex((plan) => plan.id === body.id);
    if (existing >= 0) data.plans[existing] = { ...data.plans[existing], ...body };
    else data.plans.push({ ...body, actualQuantity: 0 });
    await route.fulfill({ contentType: "application/json", body: JSON.stringify({ id: body.id }) });
  });
  await page.goto("/interior-busbar");
  await page.getByRole("tab", { name: "생산계획", exact: true }).click();
  await selectPlanMonth(page, "2026-12");
  await page.getByRole("button", { name: "다음 달", exact: true }).click();
  await expect(page.getByLabel("계획 월")).toHaveValue("2027-01");
  await page.getByRole("button", { name: "이전 달", exact: true }).click();
  await expect(page.getByLabel("계획 월")).toHaveValue("2026-12");
  await selectPlanMonth(page, "2028-02");
  await expect(page.getByRole("button", { name: /^2028-02-\d\d 생산계획 선택$/ })).toHaveCount(29);
  await selectPlanDate(page, "2028-02-29");
  await page.getByRole("button", { name: "합성 제품군 A 2028-02-29 계획 등록", exact: true }).click();
  await expect(page.getByLabel("목표 수량", { exact: true })).toBeFocused();
  await expect(page.getByLabel("생산일", { exact: true })).toHaveValue("2028-02-29");
  await expect(page.getByLabel("생산일", { exact: true })).toBeDisabled();
  await page.getByLabel("목표 수량", { exact: true }).fill("25");
  await page.getByRole("button", { name: "저장", exact: true }).click();
  await expect(page.getByRole("tab", { name: "생산계획", exact: true })).toHaveAttribute("aria-selected", "true");
  await expect(page.getByRole("button", { name: "2028-02-29 생산계획 선택", exact: true })).toContainText("계획 25개 · 완료 0개");
  await expect(page.getByRole("heading", { name: "2028-02-29 제품군별 생산계획", exact: true })).toBeFocused();
  expect(submitted[0]).toMatchObject({ productFamilyId: familyId, planDate: "2028-02-29", quantity: 25 });
  await page.getByRole("button", { name: "합성 제품군 A 2028-02-29 계획 수정", exact: true }).click();
  await page.getByLabel("목표 수량", { exact: true }).fill("30");
  await page.getByRole("button", { name: "저장", exact: true }).click();
  await expect(page.getByRole("button", { name: "2028-02-29 생산계획 선택", exact: true })).toContainText("계획 30개 · 완료 0개");
  expect(submitted[1].id).toBe(submitted[0].id);
  expect(data.plans.filter((plan) => plan.planDate === "2028-02-29")).toHaveLength(1);
  await selectPlanMonth(page, "2027-02");
  await expect(page.getByRole("button", { name: /^2027-02-\d\d 생산계획 선택$/ })).toHaveCount(28);
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await selectPlanMonth(page, "2026-09");
    await selectPlanDate(page, "2026-09-09");
    await page.screenshot({ path: `/private/tmp/emi-busbar-plan-popup-${width}.png`, fullPage: false });
    await page.getByRole("button", { name: "생산계획 팝업 닫기" }).click();
    await page.screenshot({ path: `/private/tmp/emi-busbar-month-calendar-${width}.png`, fullPage: true });
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
  }
});

test("plan popup traps focus and retains errors while blocking close during save", async ({ page }) => {
  const data = fixture(); await mock(page, data);
  let release!: () => void;
  const gate = new Promise<void>((resolve) => { release = resolve; });
  await page.route("http://localhost:5080/api/interior-busbar/plans", async (route) => {
    await gate;
    await route.fulfill({ status: 409, contentType: "application/json", body: JSON.stringify({ message: "합성 계획 저장 오류" }) });
  });
  await page.goto("/interior-busbar");
  await page.getByRole("tab", { name: "생산계획", exact: true }).click();
  await selectPlanMonth(page, "2026-09");
  await selectPlanDate(page, "2026-09-15");
  const dialog = page.getByRole("dialog", { name: "2026-09-15 제품군별 생산계획", exact: true });
  await expect(dialog).toBeVisible();
  await page.keyboard.press("Shift+Tab");
  await expect(dialog.getByRole("button", { name: "합성 제품군 A 2026-09-15 계획 등록" })).toBeFocused();
  await page.keyboard.press("Tab");
  await expect(dialog.getByRole("button", { name: "생산계획 팝업 닫기" })).toBeFocused();
  await dialog.getByRole("button", { name: "합성 제품군 A 2026-09-15 계획 등록" }).click();
  await expect(dialog.getByLabel("목표 수량")).toBeFocused();
  await dialog.getByLabel("목표 수량").fill("20");
  await dialog.getByRole("button", { name: "저장", exact: true }).click();
  await expect(dialog.getByRole("button", { name: "생산계획 팝업 닫기" })).toBeDisabled();
  await page.keyboard.press("Escape"); await expect(dialog).toBeVisible();
  release();
  await expect(dialog.getByText("합성 계획 저장 오류", { exact: true })).toBeVisible();
  await expect(dialog.getByLabel("목표 수량")).toHaveValue("20");
  await page.keyboard.press("Escape");
  await expect(dialog).toHaveCount(0);
  await expect(page.getByRole("button", { name: "2026-09-15 생산계획 선택", exact: true })).toBeFocused();
});

test("all calendar cells match the busiest day across weeks and viewports", async ({ page }) => {
  const data = fixture();
  for (let index = 0; index < 5; index++) {
    const id = `calendar-family-${index}`;
    data.productFamilies.push({ id, name: `합성 매우 긴 제품군 이름 ${index} 고전류 특수 패널`, code: `SYN-${index}`, isActive: true });
    data.plans.push({ id: `calendar-plan-${index}`, productFamilyId: id, planDate: "2026-09-23", quantity: 30 + index, actualQuantity: index });
  }
  await mock(page, data);
  await page.goto("/interior-busbar");
  await page.getByRole("tab", { name: "생산계획", exact: true }).click();
  await selectPlanMonth(page, "2026-09");
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await expect(page.getByRole("button", { name: "2026-09-23 생산계획 선택" })).toContainText("고전류 특수 패널");
    const heights = await page.locator(".busbar-calendar-grid > *").evaluateAll((cells) => cells.map((cell) => cell.getBoundingClientRect().height));
    expect(heights).toHaveLength(35);
    expect(Math.max(...heights) - Math.min(...heights)).toBeLessThan(1);
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
    await page.screenshot({ path: `/private/tmp/emi-busbar-calendar-equal-${width}.png`, fullPage: true });
  }
  await page.getByLabel("계획 제품군", { exact: true }).selectOption(familyId);
  const heights = await page.locator(".busbar-calendar-grid > *").evaluateAll((cells) => cells.map((cell) => cell.getBoundingClientRect().height));
  expect(Math.max(...heights) - Math.min(...heights)).toBeLessThan(1);
  expect(Math.max(...heights)).toBeLessThan(200);
});

test("overview excludes completed projects and orders pending projects by due date", async ({ page }) => {
  const data = fixture(), project = data.projects[0];
  data.projects = [
    { ...project, id: "later", name: "합성 나중 현장", dueDate: "2026-09-30" },
    { ...project, id: "complete", name: "합성 완료 현장", dueDate: "2026-09-01", shippedQuantity: 60 },
    { ...project, id: "same-b", name: "합성 B 현장", dueDate: "2026-09-15" },
    { ...project, id: "same-a", name: "합성 A 현장", dueDate: "2026-09-15", shippedQuantity: 20 },
    { ...project, id: "first", name: "합성 빠른 현장", dueDate: "2026-09-12" },
  ];
  await mock(page, data);
  await page.goto("/interior-busbar");
  const rows = page.getByRole("region", { name: "프로젝트명 목록", exact: true }).getByRole("row");
  await expect(rows).toHaveCount(5);
  const names = await rows.allTextContents();
  expect(names.slice(1).map((name) => name.match(/합성 .*? 현장/)?.[0])).toEqual(["합성 빠른 현장", "합성 A 현장", "합성 B 현장", "합성 나중 현장"]);
  await expect(page.getByText("합성 완료 현장", { exact: true })).toHaveCount(0);
  await rows.nth(2).getByRole("cell", { name: "합성 A 현장", exact: true }).click();
  await expect(page.getByRole("tab", { name: "종합 현황", exact: true })).toHaveAttribute("aria-selected", "true");
  await expect(rows.nth(2)).not.toHaveAttribute("tabindex");
  await expect(rows.nth(2)).not.toHaveAttribute("aria-expanded");
});

test("photo action is first column and popup supports completion, QR preview and print", async ({ page }) => {
  const data = fixture(); await mock(page, data);
  await page.goto("/interior-busbar");
  await page.getByRole("tab", { name: "생산·사진·QR", exact: true }).click();
  const table = page.getByRole("region", { name: "사진·QR 목록", exact: true });
  await expect(table.getByRole("columnheader").first()).toHaveText("사진·QR");
  await expect(table.getByRole("row").nth(1).getByRole("cell").first().getByRole("button", { name: "사진 등록 계속하기" })).toBeVisible();
  await table.getByRole("button", { name: "사진 등록 계속하기" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByLabel("앨범에서 앞면 선택")).toBeVisible();
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await page.screenshot({ path: `/private/tmp/emi-busbar-photo-popup-${width}.png` });
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
  }
  await dialog.getByLabel("앨범에서 앞면 선택").setInputFiles({ name: "front.png", mimeType: "image/png", buffer: png });
  await expect(dialog.getByRole("img", { name: "앞면 등록 사진" })).toBeVisible();
  await dialog.getByLabel("앨범에서 뒷면 선택").setInputFiles({ name: "back.png", mimeType: "image/png", buffer: png });
  await expect(dialog.locator(".busbar-completion strong")).toHaveText("IB-00000001");
  data.products[0].publicationState = "Published";
  await dialog.getByRole("button", { name: "게시 다시 요청" }).click();
  await dialog.getByRole("button", { name: "QR 인쇄 준비" }).click();
  await expect(dialog.getByRole("img", { name: "IB-00000001 QR", exact: true })).toBeVisible();
  await page.emulateMedia({ media: "print" });
  await expect(page.locator(".busbar-qr-print")).toBeVisible();
  await expect(page.locator(".busbar-qr-print p")).toHaveText("IB-00000001");
  await page.screenshot({ path: "/private/tmp/emi-busbar-photo-popup-print.png" });
  await page.emulateMedia({ media: "screen" });
  await dialog.getByRole("button", { name: "사진 팝업 닫기" }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await expect(table.getByRole("button", { name: "사진·QR 보기" })).toBeFocused();
});

test("project status filter combines with text search", async ({ page }) => {
  const data = fixture(), project = data.projects[0];
  data.projects = [
    { ...project, id: "a", name: "합성 A 진행" },
    { ...project, id: "b", name: "합성 A 완료", shippedQuantity: 60 },
    { ...project, id: "c", name: "합성 B 진행" },
  ];
  await mock(page, data); await page.goto("/interior-busbar");
  await page.getByRole("tab", { name: "납품 프로젝트", exact: true }).click();
  const table = page.getByRole("region", { name: "프로젝트명 목록", exact: true });
  await expect(table.getByRole("row")).toHaveCount(4);
  await page.getByLabel("검색", { exact: true }).fill("합성 A");
  await page.getByLabel("프로젝트 상태", { exact: true }).selectOption("InProgress");
  await expect(table.getByRole("row")).toHaveCount(2);
  await expect(table.getByRole("button", { name: "합성 A 진행", exact: true })).toBeVisible();
  await page.getByLabel("프로젝트 상태", { exact: true }).selectOption("Complete");
  await expect(table.getByRole("row")).toHaveCount(2);
  await expect(table.getByRole("button", { name: "합성 A 완료", exact: true })).toBeVisible();
  await page.getByLabel("프로젝트 상태", { exact: true }).selectOption("");
  await expect(table.getByRole("row")).toHaveCount(3);
});

test("late product detail response cannot replace a different photo popup", async ({ page }) => {
  const data = fixture();
  const otherId = "00000000-0000-0000-0000-000000000099";
  data.products.push({ ...data.products[0], id: otherId, planSequence: 2, workerName: "합성 두번째 작업자" });
  await mock(page, data);
  let release!: () => void;
  const gate = new Promise<void>((resolve) => { release = resolve; });
  await page.route(`http://localhost:5080/api/interior-busbar/products/${productId}`, async (route) => {
    await gate; await route.fulfill({ contentType: "application/json", body: JSON.stringify(data.products[0]) });
  });
  await page.goto("/interior-busbar");
  await page.getByRole("tab", { name: "생산·사진·QR", exact: true }).click();
  const firstResponse = page.waitForResponse((response) => new URL(response.url()).pathname === `/api/interior-busbar/products/${productId}`);
  await page.getByRole("button", { name: "사진 등록 계속하기", exact: true }).first().click();
  await page.getByRole("button", { name: "사진 팝업 닫기" }).click();
  await page.getByRole("button", { name: "사진 등록 계속하기", exact: true }).nth(1).click();
  await expect(page.getByRole("dialog")).toHaveAttribute("aria-label", "2026-09-09 · 대기 2번 · 합성 제품군 A");
  release();
  await firstResponse;
  await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => resolve())));
  await expect(page.getByRole("dialog")).toContainText("합성 두번째 작업자");
  await expect(page.getByRole("dialog")).toHaveAttribute("aria-label", "2026-09-09 · 대기 2번 · 합성 제품군 A");
});
