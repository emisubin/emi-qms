import { expect, test, type Page } from "@playwright/test";
import type { BusbarWorkspace } from "../../src/interiorBusbar";
const familyId = "00000000-0000-0000-0000-000000000001",
  materialId = "00000000-0000-0000-0000-000000000002",
  workerId = "00000000-0000-0000-0000-000000000003",
  productId = "00000000-0000-0000-0000-000000000004",
  projectId = "00000000-0000-0000-0000-000000000005";
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
        id: "plan",
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
        if (body.quantity > 30)
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
  await page.getByRole("button", { name: "사진·QR 보기" }).click();
  await page
    .getByLabel("앨범에서 앞면 선택")
    .setInputFiles({ name: "front.png", mimeType: "image/png", buffer: png });
  await expect(page.getByRole("img", { name: "앞면 등록 사진" })).toBeVisible();
  expect(data.products[0].status).toBe("Draft");
  await page
    .getByLabel("앨범에서 뒷면 선택")
    .setInputFiles({ name: "back.png", mimeType: "image/png", buffer: png });
  await expect(page.getByText("생산 완료", { exact: true })).toBeVisible();
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
  const writes = await mock(page, fixture());
  await page.goto("/interior-busbar");
  await page.getByRole("tab", { name: "납품 프로젝트" }).click();
  await page.getByRole("button", { name: "합성 납품 현장" }).click();
  await page.getByRole("button", { name: "분할 출하", exact: true }).click();
  await page.getByLabel("이번 출하 수량").fill("40");
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
  await page.getByRole("button", { name: "사진·QR 보기", exact: true }).click();
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
  await expect(page.getByText("취소", { exact: true })).toBeVisible();
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
