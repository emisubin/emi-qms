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
const qrPng = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAcgAAAHIAQAAAADi2kdHAAADFklEQVR4nO1Y0W4CMQy7//9pJtrYccqhSTwZYZAGV+pOchPHyfX49HUFGYYSCcmVaEJU87erw7Vf6/H53l+eC+vjWmv9tHYGacxQXe3eW3vqjLW2wHXCAAXpyRBve+3rW6+r32v1h7ESpDlDO2V3qte+HQ0MgyC/i6Gdx6rKTPcZM0FaM4S/Uoip03sZMXGv8UFaMcS9/77feLAgnRhi8zJMMW6+yjFlHJuDtGVISitbHvY41fGUdZ72OEhLhmB79RieUgYZAi7qHaQpQ+15yzeptRpf1FQFacsQFvkLMpt7O+XVGQfpyFDXV/lgPs/W9vBgQRoyJBFBG4Xyi3a2j5iVN0g/huiIRy3GxsryjofRuQbpx1DdeV12pS/7VBkjsvsJ0pohqrNKN26/2x9qdZDeDPXu+tqDfS5XVut/C9KTIV6wuN+KBaQ3vsNFB2nMEDcTIqZ4xEp7qiB9GeL9i1cCipOJM7WDNGaoMxyThpcZIbtZUfYgLRmCKstOrvSYkKOmQ6mDtGNIVJkDJNhjrrShGn4qSEOG+HsV2D6DHWyNEe8m+UG6McRIUJfUBZcqzkocpDNDVGH1wpBqmixK9Y3GB2nEEIcOZ6HFE9IdIh6kNUNYnIOGqrO4/VGNg3RmCOmKUBA/VYWW5x3OOEhLhnDz9WslMrayiWULFKQ1Q7C+UnyxkxZKGp1TqYM0Y6hhyGFOHuCJu8mZ7jlIQ4Z6O/OYsg2TVcLNQAjSmiFkMVQbNpjpjfNn5Q3SkaFheXucdPyEcJmaEKQfQ9rSSM1Fe8MfMYcI0pwhpmzbJLhl7sahhwcL0pIhXDcrrdhi9jiQ8hEJQRoy1NvxJJOIHSI9fjp6pCDdGBoltyOCvY/44ruZVJBmDOHVhXWMJCDOOEdjKEhDhsQk4QAItDzu5z4nSFuG4J7aHHdWa0cEzQ7SnCHeO1R6djf0VEjrIL+IIbgqWZEDRucapDND/YF+BlW2y+19ZgfpxZAo9d6PLoezX0bHi3YHaceQanAPCsVRIS5KyIO0ZuijV5BhKJGQXIkmRDUfP1sd/gAjti4sTxln8gAAAABJRU5ErkJggg==", "base64");
const panelQrPng = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAOgAAADoAQAAAADN0pXVAAAAqklEQVR4nO2WSw7AIAgFuf+lbYoPxKbtupmCUYPjhq/aeBNr2t7o3Ph6LdgUV61oWCrTY+yASE/rzzX98Qs6/kYj/niqdde4VP3LXnsdh6bsGpe6K6r9U6HS7GV1kGkeaL/8RnBUXy9/nWOS6W3Gc2lVll+4NN8nzfkfAVOZ7vHPa2Qa/dtDr4t8GifRzPA0ny0VPZaWbC8QS1c3W/kOpo/StL3RuTE+XAsHwr51lsnwSsYAAAAASUVORK5CYII=", "base64");

async function selectSection(page: Page, name: string) {
  const labels: Record<string, string> = { "종합 현황": "홈", "납품 프로젝트": "프로젝트", "생산·사진·QR": "생산", "구매·자재": "발주, 입고관리" };
  const mobile = page.getByRole("button", { name: "메뉴 열기", exact: true });
  const isMobile = (page.viewportSize()?.width ?? 1440) <= 860;
  await expect(page.locator(".app-shell")).toHaveAttribute("data-layout-mode", isMobile ? "mobile" : "desktop");
  if (isMobile) await mobile.click();
  const container = page.locator(isMobile ? ".mobile-menu-drawer" : ".app-sidebar");
  await expect(container).toBeVisible();
  const parent = container.getByRole("button", { name: "인테리어 부스바", exact: true });
  if (await parent.getAttribute("aria-expanded") !== "true") await parent.click();
  await container.locator(".app-nav-children, .mobile-menu-children").getByRole("button", { name: labels[name] ?? name, exact: true }).click();
}

function fixture(canWrite = true): BusbarWorkspace {
  return {
    canWrite,
    permissions: { projects: canWrite, planning: canWrite, production: canWrite, purchases: canWrite, administration: canWrite, mastersRead: true, mastersWrite: canWrite, manageMasterPermissions: canWrite },
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
async function mock(page: Page, data: BusbarWorkspace, denied = false, commercialAvailable: () => boolean = () => true) {
  const writes: Array<{ path: string; body: unknown }> = [];
  await page.route("http://localhost:5080/**", async (route) => {
    const req = route.request(),
      url = new URL(req.url()),
      path = url.pathname;
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
    if (path === "/api/interior-busbar/access") return json(data.permissions);
    if (path === "/api/interior-busbar/master-access" && req.method() === "GET") return json([{userId:workerId,displayName:"합성 사용자",departmentName:"생산관리",access:"None",automatic:false}]);
    if (path === "/api/interior-busbar/masters") return data.permissions?.mastersRead ? json(data) : json({message:"기준정보 접근 권한이 없습니다."},403);
    if (path === "/api/interior-busbar/workspace")
      return denied
        ? json({ message: "청주에서만 사용할 수 있습니다." }, 403)
        : json(data);
    if (/^\/api\/interior-busbar\/projects\/[^/]+$/.test(path) && req.method() === "GET") {
      const project = data.projects.find((item) => item.id === path.split("/").at(-1));
      return project
        ? json({
            project,
            shipments: data.shipments.filter((item) => item.projectId === project.id),
            panels: data.products.filter((item) => "shipmentId" in item),
          })
        : json({ message: "프로젝트를 찾을 수 없습니다." }, 404);
    }
    if (/^\/api\/interior-busbar\/projects\/[^/]+\/scan$/.test(path) && req.method() === "GET") {
      const code = url.searchParams.get("code")?.trim();
      const panel = data.products.find((item) => item.number === code || `https://synthetic.invalid/p/${item.id}` === code);
      return panel
        ? json({ ...panel, hasFront: undefined, hasBack: undefined })
        : json({ message: "등록된 패널 QR 또는 제품번호를 확인하세요." }, 400);
    }
    if (/^\/api\/interior-busbar\/products\/[^/]+$/.test(path) && req.method() === "GET")
      return json(data.products.find((p) => p.id === path.split("/").at(-1)));
    if (path.endsWith("/ecount-status")) return json({transmissionEnabled:false,jobs:[{id:projectId,kind:"Order",state:"Pending",needsReview:false,message:null,slipNumber:null,attemptCount:0}]});
    if (path.endsWith("/commercial-preview")) {
      if (!commercialAvailable()) return json({ message: "합성 일시 오류" }, 503);
      return json({unitPrice:12500, quantity:60, supplyAmount:750000, vatAmount:75000, totalAmount:825000, missingFields:[], customerCode:"SYN-C",warehouseCode:"SYNWH",productCode:"SYN-P",transmissionEnabled:false});
    }
    if (path.endsWith("/qr") && req.method() === "GET") return route.fulfill({ contentType: "image/png", body: qrPng });
    if (path.includes("/photos/") && req.method() === "GET") {
      const front = path.endsWith("/front");
      const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="640" height="420"><rect width="640" height="420" fill="${front ? "#dbeafe" : "#dcfce7"}"/><rect x="70" y="80" width="500" height="260" rx="18" fill="none" stroke="${front ? "#2563eb" : "#16a34a"}" stroke-width="12"/><text x="320" y="225" font-family="sans-serif" font-size="42" text-anchor="middle" fill="#0f172a">${front ? "합성 앞면" : "합성 뒷면"}</text></svg>`;
      return route.fulfill({ contentType: "image/svg+xml", body: Buffer.from(svg) });
    }
    if (path.startsWith("/api/interior-busbar/") && req.method() !== "GET") {
      if (!data.canWrite)
        return json({ message: "입력 권한이 없습니다." }, 403);
      writes.push({
        path,
        body: req.headers()["content-type"]?.includes("application/json")
          ? req.postDataJSON()
          : null,
      });
      const lifecycle = path.match(/\/api\/interior-busbar\/(projects|plans|purchases|product-families|materials|workers|boms)\/([^/]+)\/(delete|restore)$/);
      if (lifecycle) {
        const key = lifecycle[1] === "product-families" ? "productFamilies" : lifecycle[1];
        const records = data[key as "projects"];
        const row = records.find(item => item.id === lifecycle[2]);
        if (row) row.isDeleted = lifecycle[3] === "delete";
      }
      if (path.endsWith("/reverse")) {
        const record = data.ledger.find(item => item.id === path.split("/").at(-2));
        if (record) record.reversed = true;
      }
      if (path.endsWith("/cancel")) {
        const product = data.products.find(item => item.id === path.split("/").at(-2));
        if (product) product.status = "Cancelled";
      }
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
  const input = page.getByLabel("계획 월", { exact: true });
  if (await input.isVisible()) await input.fill(month);
  else await input.evaluate((element, value) => {
    const field = element as HTMLInputElement;
    const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")?.set;
    setter?.call(field, value);
    field.dispatchEvent(new Event("input", { bubbles: true }));
    field.dispatchEvent(new Event("change", { bubbles: true }));
  }, month);
}
test("six workspaces desktop and 390px without horizontal page overflow", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await mock(page, fixture());
  await page.goto("/interior-busbar");
  await expect(
    page.getByRole("heading", { name: "홈", exact: true }),
  ).toBeVisible();
  for (const width of [1440, 1200, 1101, 390]) {
    await page.setViewportSize({ width, height: 900 });
    for (const label of [
      "종합 현황",
      "납품 프로젝트",
      "생산계획",
      "생산·사진·QR",
      "구매·자재",
      "기준정보",
    ]) {
      await selectSection(page, label);
      await expect(page.locator(".busbar-page")).toBeVisible();
      await expect(page.locator(".app-shell")).toHaveAttribute("data-osan-project-theme", "true");
      await expect(page.locator(".busbar-page .osan-dashboard")).toHaveCount(1);
      expect(
        await page.evaluate(
          () =>
            document.documentElement.scrollWidth -
            document.documentElement.clientWidth,
        ),
      ).toBe(0);
      await page.screenshot({
        path: test.info().outputPath(`emi-busbar-${width}-${label.replaceAll("·", "-")}.png`),
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
  await selectSection(page, "생산·사진·QR");
  await page.getByRole("button", { name: "사진등록" }).click();
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
  ).toHaveCount(0);
  await expect(page.getByLabel("카메라로 앞면 촬영")).toHaveCount(0);
  await expect(page.getByLabel("앨범에서 앞면 선택")).toHaveCount(0);
  await page.screenshot({
    path: test.info().outputPath("emi-busbar-photo-complete.png"),
    fullPage: true,
  });
});
test("panel shipment rejection keeps the exact request and panel order for retry", async ({
  page,
}) => {
  const data = fixture();
  data.products[0] = {
    ...data.products[0],
    status: "Complete",
    number: "IB-00000001",
    manufacturedAtUtc: "2026-09-09T06:00:00Z",
    hasFront: true,
    hasBack: true,
  };
  const writes = await mock(page, data);
  await page.goto("/interior-busbar");
  await selectSection(page, "납품 프로젝트");
  await page.getByRole("cell", { name: "합성 납품 현장", exact: true }).click();
  await page.getByRole("button", { name: "패널 QR로 분할 출하", exact: true }).click();
  await page.getByLabel("QR 스캐너 또는 제품번호").fill("IB-00000001");
  await page.getByLabel("QR 스캐너 또는 제품번호").press("Enter");
  await expect(page.getByRole("heading", { name: "IB-00000001 · 이 패널을 출하할까요?", exact: true })).toBeVisible();
  data.productFamilies[0].balance = 0; // Concurrent shipment after this form opened.
  await page.getByRole("button", { name: "예, 출하 등록", exact: true }).click();
  await expect(page.getByText("완제품 재고가 부족합니다.", { exact: false })).toBeVisible();
  await expect(page.getByRole("button", { name: "아니요", exact: true })).toBeDisabled();
  await page.getByRole("button", { name: "같은 패널 다시 확인", exact: true }).click();
  await expect(page.getByText("완제품 재고가 부족합니다.", { exact: false })).toBeVisible();
  expect(writes).toHaveLength(2);
  expect((writes[0].body as { requestId: string }).requestId).toBe(
    (writes[1].body as { requestId: string }).requestId,
  );
  expect(writes[0].body).toMatchObject({ quantity: 1, productIds: [productId] });
  expect(writes[1].body).toEqual(writes[0].body);
});
test("read only users see no mutation controls", async ({ page }) => {
  const writes = await mock(page, fixture(false));
  await page.goto("/interior-busbar/projects");
  await expect(
    page.getByText("이 화면은 조회만 가능합니다.", { exact: false }),
  ).toBeVisible();
  for (const label of [
    "납품 프로젝트",
    "생산계획",
    "생산·사진·QR",
    "구매·자재",
    "기준정보",
  ])
    await selectSection(page, label);
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
  await selectSection(page, "기준정보");
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
    path: test.info().outputPath("emi-busbar-bom-reloaded.png"),
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


test("planned QR number survives worker selection and mobile preview then save", async ({
  page,
}) => {
  const data = fixture();
  data.products[0].number = "IB-00000001";
  data.products[0].workerId = null;
  data.products[0].workerName = null;
  await mock(page, data);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/interior-busbar");
  await selectSection(page, "생산계획");
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
    page.getByRole("button", { name: "IB-00000001 사진 등록", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "IB-00000001 사진 등록", exact: true })
    .click();
  await expect(page.getByLabel("모바일 앞면 사진 선택")).toBeDisabled();
  await expect(page.getByLabel("모바일 앞면 카메라 촬영")).toBeDisabled();
  await page
    .getByRole("combobox", {
      name: "실제 제조 작업자",
      exact: true,
    })
    .selectOption(workerId);
  await expect(page.getByLabel("모바일 앞면 사진 선택")).toBeEnabled();
  const upload = page.waitForRequest(
    (request) =>
      request.method() === "PUT" && request.url().endsWith("/photos/front"),
  );
  await page
    .getByLabel("모바일 앞면 사진 선택")
    .setInputFiles({ name: "front.png", mimeType: "image/png", buffer: png });
  await expect(page.getByRole("img", {name: "앞면 저장 전 미리보기"})).toBeVisible();
  expect(data.products[0].hasFront).toBe(false);
  await page.getByRole("button", {name: "앞면 사진 저장", exact: true}).click();
  const uploadRequest = await upload;
  expect(uploadRequest.postDataBuffer()?.toString()).toContain(
    'name="workerId"',
  );
  expect(uploadRequest.postDataBuffer()?.toString()).toContain(workerId);
  await expect(page.getByRole("img", { name: "앞면 등록 사진" })).toBeVisible();
  expect(data.products[0].number).toBe("IB-00000001");
  await page.screenshot({
    path: test.info().outputPath("emi-busbar-plan-draft-390.png"),
    fullPage: true,
  });
  await page.getByRole("tab", {name: "뒷면 미등록"}).click();
  await page
    .getByLabel("모바일 뒷면 사진 선택")
    .setInputFiles({ name: "back.png", mimeType: "image/png", buffer: png });
  await page.getByRole("button", {name: "뒷면 사진 저장", exact: true}).click();
  await expect(page.locator(".busbar-completion")).toBeFocused();
  await expect(page.locator(".busbar-completion strong")).toHaveText(
    "IB-00000001",
  );
  await expect(
    page.getByRole("button", { name: "QR 인쇄 준비", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByText("생산계획 때 발급된 QR을 계속 사용합니다.", { exact: false }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () =>
        document.documentElement.scrollWidth -
        document.documentElement.clientWidth,
    ),
  ).toBe(0);
  await page.screenshot({
    path: test.info().outputPath("emi-busbar-plan-complete-390.png"),
    fullPage: true,
  });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.screenshot({
    path: test.info().outputPath("emi-busbar-plan-complete-1440.png"),
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
  await selectSection(page, "생산계획");
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
  await expect(page).toHaveURL(/\/interior-busbar\/plans$/);
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
    await selectSection(page, "생산·사진·QR");
    await page.getByLabel("계획 시작일", { exact: true }).fill("2026-09-09");
    await page.getByLabel("계획 종료일", { exact: true }).fill("2026-09-09");
    await expect(
      page.getByRole("button", { name: "사진등록", exact: true }),
    ).toHaveCount(1);
    await page
      .getByRole("button", { name: "사진등록", exact: true })
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
      await selectSection(page, "생산계획");
      await selectPlanMonth(page, "2026-09");
      await selectPlanDate(page, "2026-09-12");
      await expect(page.getByRole("button", { name: "합성 제품군 A 2026-09-12 제품 보기", exact: true })).toBeEnabled();
      releaseUpload();
      await page
        .getByRole("button", { name: "합성 제품군 A 2026-09-12 제품 보기", exact: true })
        .click();
    }
    await expect(
      page.getByRole("cell", { name: "2026-09-12 · 대기 1번", exact: true }),
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
      page.getByRole("cell", { name: "2026-09-12 · 대기 1번", exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText("2026-09-09 · 대기 1번", { exact: true }),
    ).toHaveCount(0);
    expect(workspacePlans.at(-1)).toBe("2026-09-12");
  });
}

test("project row opens independent detail with family shipment context", async ({ page }) => {
  await mock(page, fixture());
  await page.goto("/interior-busbar");
  await selectSection(page, "납품 프로젝트");
  await expect(page.getByRole("columnheader", { name: "작업", exact: true })).toHaveCount(0);
  await page.getByRole("cell", { name: "합성 납품 현장", exact: true }).click();
  await expect(page).toHaveURL(`/interior-busbar/projects/${projectId}`);
  await expect(page.locator(".busbar-shippable")).toHaveCount(0);
  await expect(page.getByRole("columnheader", { name: "현재고", exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "해당 제품군 생산계획" })).toBeVisible();
  await expect(page.getByRole("cell", { name: "59개", exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByRole("heading", { name: "합성 납품 현장", exact: true })).toBeVisible();
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await page.screenshot({ path: test.info().outputPath(`emi-busbar-project-context-${width}.png`), fullPage: true });
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
  }
});

test("project refresh retries a failed commercial preview", async ({ page }) => {
  let previewAvailable = false;
  let previewRequests = 0;
  await mock(page, fixture(), false, () => {
    previewRequests += 1;
    return previewAvailable;
  });
  await page.goto(`/interior-busbar/projects/${projectId}`);
  await expect(page.getByText("금액 정보를 조회하지 못했습니다. 잠시 후 새로고침해 주세요.", { exact: true })).toBeVisible();
  const requestsBeforeRefresh = previewRequests;
  previewAvailable = true;
  await page.getByRole("button", { name: "새로고침", exact: true }).click();
  await expect(page.getByText("공급가액", { exact: true })).toBeVisible();
  expect(previewRequests).toBeGreaterThan(requestsBeforeRefresh);
});

test("project detail shows linked panels, legacy history and read-only photos", async ({ page }) => {
  const data = fixture();
  const linkedShipmentId = "00000000-0000-0000-0000-000000000008";
  data.shipments = [
    {
      id: linkedShipmentId,
      projectId,
      quantity: 1,
      createdAtUtc: "2026-09-15T02:00:00Z",
      shippedByName: "합성 출하 담당자",
      projectNameSnapshot: "출하 당시 프로젝트",
      destinationSnapshot: "출하 당시 도착지",
      taskNumberSnapshot: "SYN-HISTORY",
    },
    {
      id: "00000000-0000-0000-0000-000000000009",
      projectId,
      quantity: 2,
      createdAtUtc: "2026-09-14T02:00:00Z",
    },
  ];
  data.projects[0].shippedQuantity = 3;
  data.products[0] = Object.assign(
    {
      ...data.products[0],
      status: "Complete" as const,
      number: "IB-00000001",
      manufacturedAtUtc: "2026-09-09T06:00:00Z",
      workerName: "합성 외주 작업자",
      hasFront: true,
      hasBack: true,
      revision: 2,
    },
    { shipmentId: linkedShipmentId, releasedAtUtc: "2026-09-15T02:00:00Z" },
  );
  await mock(page, data);
  await page.goto(`/interior-busbar/projects/${projectId}`);
  await expect(page.getByText("출하 당시 프로젝트", { exact: true })).toBeVisible();
  await expect(page.getByText("출하 당시 도착지", { exact: true })).toBeVisible();
  await expect(page.getByText("기록 없음 (기존 출하)", { exact: true })).toHaveCount(3);
  await expect(page.getByText("패널 연결 기록 없음", { exact: true })).toBeVisible();
  await expect(page.getByRole("cell", { name: "IB-00000001", exact: true })).toBeVisible();
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await page.screenshot({ path: test.info().outputPath(`emi-busbar-project-history-${width}.png`), fullPage: true });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
  }
  await page.getByRole("button", { name: "앞·뒤 사진 보기", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "IB-00000001 출하 사진" });
  await expect(dialog.getByRole("img", { name: "앞면 등록 사진" })).toBeVisible();
  await expect(dialog.getByRole("img", { name: "뒷면 등록 사진" })).toBeVisible();
  await expect(dialog.locator('input[type="file"]')).toHaveCount(0);
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await dialog.screenshot({ path: test.info().outputPath(`emi-busbar-project-photos-${width}.png`) });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
  }
});

test("keyboard QR scan rejects invalid and duplicate values then ships selected IDs", async ({ page }) => {
  const data = fixture();
  data.products[0] = {
    ...data.products[0],
    status: "Complete",
    number: "IB-00000001",
    manufacturedAtUtc: "2026-09-09T06:00:00Z",
    hasFront: true,
    hasBack: true,
  };
  const writes = await mock(page, data);
  await page.goto(`/interior-busbar/projects/${projectId}`);
  const openShipment = page.getByRole("button", { name: "패널 QR로 분할 출하", exact: true });
  await openShipment.click();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog", { name: "패널 QR 분할 출하" })).toHaveCount(0);
  await expect(openShipment).toBeFocused();
  await openShipment.click();
  const scanInput = page.getByLabel("QR 스캐너 또는 제품번호");
  await scanInput.fill("NOT-A-PANEL");
  await scanInput.press("Enter");
  await expect(page.getByText("등록된 패널 QR 또는 제품번호를 확인하세요.", { exact: true })).toBeVisible();
  const qrUrl = `https://synthetic.invalid/p/${productId}`;
  const request = page.waitForRequest((item) => new URL(item.url()).pathname.endsWith(`/projects/${projectId}/scan`));
  await scanInput.fill(qrUrl);
  await scanInput.press("Enter");
  expect(new URL((await request).url()).searchParams.get("code")).toBe(qrUrl);
  await expect(page.getByRole("heading", { name: "IB-00000001 · 이 패널을 출하할까요?", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "아니요", exact: true }).click();
  await scanInput.fill(qrUrl);
  await scanInput.press("Enter");
  await expect(page.getByRole("heading", { name: "IB-00000001 · 이 패널을 출하할까요?", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "예, 출하 등록", exact: true }).click();
  await expect.poll(() => writes.filter((item) => item.path.endsWith("/shipments")).length).toBe(1);
  expect(writes.find((item) => item.path.endsWith("/shipments"))?.body).toMatchObject({
    projectId,
    quantity: 1,
    productIds: [productId],
  });
  await expect(scanInput).toBeVisible();
  await scanInput.fill(qrUrl);
  await scanInput.press("Enter");
  await expect(page.getByText("이미 출하 등록한 패널입니다. 다음 패널을 스캔하세요.", { exact: true })).toBeVisible();
  expect(writes.filter((item) => item.path.endsWith("/shipments"))).toHaveLength(1);
  data.products.push({ ...data.products[0], id: "00000000-0000-0000-0000-000000000099", number: "IB-00000002" });
  await scanInput.fill("IB-00000002");
  await scanInput.press("Enter");
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await expect(page.getByRole("img", { name: "앞면 등록 사진" })).toBeVisible();
    await expect(page.getByRole("img", { name: "뒷면 등록 사진" })).toBeVisible();
    await page.getByRole("dialog", { name: "패널 QR 분할 출하" }).screenshot({ path: test.info().outputPath(`emi-busbar-quick-scan-${width}.png`) });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(true);
  }
  await page.getByRole("button", { name: "예, 출하 등록", exact: true }).click();
  await expect.poll(() => writes.filter((item) => item.path.endsWith("/shipments")).length).toBe(2);
  expect(writes[1].body).toMatchObject({ quantity: 1, productIds: ["00000000-0000-0000-0000-000000000099"] });
  expect((writes[0].body as {requestId:string}).requestId).not.toBe((writes[1].body as {requestId:string}).requestId);

});

test("camera start lazy-loads ZXing and decodes a synthetic QR stream", async ({ page }) => {
  const data = fixture();
  data.products[0] = {
    ...data.products[0],
    status: "Complete",
    number: "IB-00000001",
    manufacturedAtUtc: "2026-09-09T06:00:00Z",
    hasFront: true,
    hasBack: true,
  };
  const qrDataUrl = `data:image/png;base64,${panelQrPng.toString("base64")}`;
  await page.addInitScript((source) => {
    const mediaDevices = navigator.mediaDevices ?? {} as MediaDevices;
    Object.defineProperty(navigator, "mediaDevices", { configurable: true, value: mediaDevices });
    Object.defineProperty(mediaDevices, "getUserMedia", {
      configurable: true,
      value: async () => {
        const image = new Image();
        image.src = source;
        await image.decode();
        const canvas = document.createElement("canvas");
        canvas.width = image.naturalWidth;
        canvas.height = image.naturalHeight;
        canvas.getContext("2d")!.drawImage(image, 0, 0);
        return canvas.captureStream(12);
      },
    });
  }, qrDataUrl);
  await mock(page, data);
  const decoded: string[] = [];
  await page.route(`**/api/interior-busbar/projects/${projectId}/scan?**`, async (route) => {
    decoded.push(new URL(route.request().url()).searchParams.get("code") ?? "");
    await route.fulfill({ contentType: "application/json", body: JSON.stringify(data.products[0]) });
  });
  await page.goto(`/interior-busbar/projects/${projectId}`);
  await page.getByRole("button", { name: "패널 QR로 분할 출하", exact: true }).click();
  await page.getByRole("button", { name: "카메라 시작", exact: true }).click();
  await expect(page.getByRole("heading", { name: "IB-00000001 · 이 패널을 출하할까요?", exact: true })).toBeVisible({ timeout: 15_000 });
  expect(decoded).toHaveLength(1);
  expect(decoded[0]).toBe("IB-00000001");
  await page.getByRole("button", { name: "예, 출하 등록", exact: true }).click();
  await expect(page.getByRole("button", { name: "카메라 끄기", exact: true })).toBeVisible();
  await page.waitForTimeout(2200);
  expect(decoded).toHaveLength(1);
  await expect(page.getByRole("region", { name: "출하 패널 확인" })).toHaveCount(0);
  expect(await page.locator("video").evaluate((v) => (v as HTMLVideoElement).srcObject instanceof MediaStream && ((v as HTMLVideoElement).srcObject as MediaStream).getVideoTracks()[0].readyState === "live")).toBe(true);
  await page.getByRole("button", { name: "카메라 끄기", exact: true }).click();
});

test("shipment reversal retry keeps its request ID and first reason", async ({ page }) => {
  const data = fixture();
  const shipmentId = "00000000-0000-0000-0000-000000000010";
  data.shipments = [{
    id: shipmentId,
    projectId,
    quantity: 1,
    createdAtUtc: "2026-09-15T02:00:00Z",
    projectNameSnapshot: "합성 납품 현장",
    destinationSnapshot: "합성 업체",
    taskNumberSnapshot: "SYN-001",
  }];
  data.projects[0].shippedQuantity = 1;
  await mock(page, data);
  const attempts: Array<{ requestId: string; reason: string }> = [];
  await page.route(`**/api/interior-busbar/ledger/${shipmentId}/reverse`, async (route) => {
    attempts.push(route.request().postDataJSON());
    await route.fulfill({
      status: attempts.length === 1 ? 409 : 200,
      contentType: "application/json",
      body: JSON.stringify(attempts.length === 1 ? { message: "합성 동시 처리 충돌" } : { id: shipmentId }),
    });
  });
  await page.goto(`/interior-busbar/projects/${projectId}`);
  await page.getByRole("button", { name: "출하 정정", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "출하 정정" });
  const reason = dialog.getByLabel("정정 사유", { exact: true });
  await reason.fill("합성 출하 대상 확인");
  await dialog.getByRole("button", { name: "출하 정정", exact: true }).click();
  await expect(dialog.getByText("합성 동시 처리 충돌", { exact: true })).toBeVisible();
  await expect(reason).toBeDisabled();
  await dialog.getByRole("button", { name: "출하 정정", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  expect(attempts).toHaveLength(2);
  expect(attempts[1]).toEqual(attempts[0]);
});

test("monthly calendar selection and photo filters send server-side conditions", async ({ page }) => {
  const data = fixture();
  data.productFamilies.push({ id: "family-b", name: "합성 제품군 B", code: "SYN-B", isActive: true });
  await mock(page, data);
  await page.goto("/interior-busbar");
  await selectSection(page, "생산계획");
  await selectPlanMonth(page, "2026-09");
  await expect(page.getByRole("button", { name: "2026-09-09 생산계획 선택" })).toContainText("계획 60개 · 완료 1개");
  await page.getByRole("button", { name: "다음 달", exact: true }).click();
  await expect(page.getByLabel("계획 월")).toHaveValue("2026-10");
  await page.getByRole("button", { name: "이전 달", exact: true }).click();
  await selectPlanDate(page, "2026-09-09");
  await page.getByRole("button", { name: "생산계획 팝업 닫기" }).click();
  const selectedDay = page.getByRole("button", { name: "2026-09-09 생산계획 선택", exact: true });
  await expect(selectedDay.locator(".busbar-calendar-plan strong")).toHaveCSS("color", "rgb(51, 65, 85)");
  await page.screenshot({ path: test.info().outputPath("emi-busbar-final-selected-calendar.png"), fullPage: true });
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
  await selectSection(page, "생산·사진·QR");
  await page.getByLabel("생산 상태 필터", { exact: true }).selectOption("Draft");
  await page.getByRole("button", { name: "사진등록", exact: true }).click();
  await page.getByLabel("앨범에서 앞면 선택").setInputFiles({ name: "front.png", mimeType: "image/png", buffer: png });
  await expect(page.getByRole("img", { name: "앞면 등록 사진" })).toBeVisible();
  await page.getByLabel("앨범에서 뒷면 선택").setInputFiles({ name: "back.png", mimeType: "image/png", buffer: png });
  await expect(page.getByRole("dialog").locator(".busbar-completion strong")).toHaveText("IB-00000001");
  await expect(page.locator(".busbar-completion")).toBeFocused();
  await expect(page.getByLabel("생산 상태 필터", { exact: true })).toHaveValue("Draft");
  await expect(page.getByRole("button", { name: "사진등록", exact: true })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "QR 인쇄 준비", exact: true })).toHaveCount(0);
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
  await selectSection(page, "생산계획");
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
  await expect(page).toHaveURL(/\/interior-busbar\/plans$/);
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
    await page.screenshot({ path: test.info().outputPath(`emi-busbar-plan-popup-${width}.png`), fullPage: false });
    await page.getByRole("button", { name: "생산계획 팝업 닫기" }).click();
    if (width === 390) {
      const selectedDate = page.getByRole("button", { name: "2026-09-09 생산계획 선택", exact: true });
      const selectedDateCircle = selectedDate.locator(".busbar-calendar-date b");
      await expect(selectedDate).toHaveCSS("background-color", "rgba(0, 0, 0, 0)");
      await expect(selectedDateCircle).toHaveCSS("width", "32px");
      await expect(selectedDateCircle).toHaveCSS("height", "32px");
      await expect(selectedDateCircle).toHaveCSS("border-radius", "50%");
      await expect(selectedDateCircle).toHaveCSS("background-color", "rgb(40, 40, 40)");
      await expect(selectedDateCircle).toHaveCSS("color", "rgb(255, 255, 255)");
      await expect(selectedDate.locator(".busbar-calendar-mobile-indicator i")).toHaveCSS("background-color", "rgb(85, 85, 85)");
    }
    await page.screenshot({ path: test.info().outputPath(`emi-busbar-month-calendar-${width}.png`), fullPage: true });
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
  await selectSection(page, "생산계획");
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
  await selectSection(page, "생산계획");
  await selectPlanMonth(page, "2026-09");
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await expect(page.getByRole("button", { name: "2026-09-23 생산계획 선택" })).toContainText("고전류 특수 패널");
    const heights = await page.locator(".busbar-calendar-grid > *").evaluateAll((cells) => cells.map((cell) => cell.getBoundingClientRect().height));
    expect(heights).toHaveLength(35);
    expect(Math.max(...heights) - Math.min(...heights)).toBeLessThan(1);
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
    await page.screenshot({ path: test.info().outputPath(`emi-busbar-calendar-equal-${width}.png`), fullPage: true });
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
  const rows = page.getByRole("region", { name: "납기 상태 목록", exact: true }).getByRole("row");
  await expect(rows).toHaveCount(4);
  const names = await rows.allTextContents();
  expect(names.slice(1).map((name) => name.match(/합성 .*? 현장/)?.[0])).toEqual(["합성 빠른 현장", "합성 A 현장", "합성 B 현장"]);
  await expect(page.getByText("합성 완료 현장", { exact: true })).toHaveCount(0);
  await expect(page.getByText("합성 나중 현장", { exact: true })).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "홈", exact: true })).toBeVisible();
  await expect(rows.nth(2)).not.toHaveAttribute("tabindex");
  await expect(rows.nth(2)).not.toHaveAttribute("aria-expanded");
});

test("photo popup contains registration only and table follows production order", async ({ page }) => {
  const data = fixture(); await mock(page, data);
  await page.goto("/interior-busbar");
  await selectSection(page, "생산·사진·QR");
  const table = page.getByRole("region", { name: "선택 목록", exact: true });
  await expect(table.getByRole("button", { name: /제품 관리$/ })).toHaveCount(0);
  await expect(table.getByRole("columnheader").first().getByRole("checkbox", {name: "현재 목록 출력 가능 제품 모두 선택"})).toBeVisible();
  await expect(table.getByRole("columnheader")).toHaveText(["", "사진등록", "제품군", "계획일", "작업자", "생산일시", "생산 상태", "제품번호", "라벨 상태", "외부게시"]);
  await expect(table.getByRole("cell", { name: "사진 등록 전", exact: true })).toBeVisible();
  await table.getByRole("button", { name: "사진등록" }).click();
  const dialog = page.getByRole("dialog");
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await page.screenshot({ path: test.info().outputPath(`emi-busbar-photo-popup-${width}.png`) });
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
  }
  await expect(dialog.getByRole("button", { name: /생산 취소$/ })).toBeVisible();
  await dialog.getByLabel("모바일 앞면 사진 선택").setInputFiles({ name: "front.png", mimeType: "image/png", buffer: png });
  await dialog.getByRole("button", { name: "앞면 사진 저장", exact: true }).click();
  await expect(page.locator(".busbar-production-mobile-list")).toContainText("1차 사진 등록 완료");
  await dialog.getByRole("tab", { name: /뒷면/ }).click();
  await dialog.getByLabel("모바일 뒷면 사진 선택").setInputFiles({ name: "back.png", mimeType: "image/png", buffer: png });
  await dialog.getByRole("button", { name: "뒷면 사진 저장", exact: true }).click();
  await expect(dialog.locator(".busbar-completion strong")).toHaveText("IB-00000001");
  await expect(dialog.getByRole("button", { name: /QR/ })).toHaveCount(0);
  await expect(dialog.getByRole("button", { name: /생산 취소$/ })).toBeVisible();
  await dialog.getByRole("button", { name: "사진 팝업 닫기" }).click();
  await expect(page.getByRole("heading", { name: "생산 제품 목록", exact: true })).toBeFocused();
});

function publishedFixture() {
  const data = fixture(); const draft = data.products[0];
  data.products = [1, 2].map((n) => ({ ...draft, id: `published-${n}`, number: `IB-0000000${n}`, status: "Complete", hasFront: true, hasBack: true, revision: 2, publishedRevision: 2, publicationState: "Published" }));
  data.products.push({ ...draft, id: "waiting", number: "IB-WAITING", revision: 1, publishedRevision: 1, publicationState: "Published" }, { ...draft, id: "stale", status: "Complete", number: "IB-STALE", revision: 3, publishedRevision: 2, publicationState: "Published" });
  return data;
}
test("bulk QR prepares all eligible labels and prints separate cards", async ({ page }) => {
  const data = publishedFixture(); await mock(page, data);
  await page.goto("/interior-busbar"); await selectSection(page, "생산·사진·QR");
  await expect(page.locator(".busbar-production-desktop").getByLabel("IB-STALE QR 선택")).toBeDisabled();
  await page.getByRole("checkbox", { name: "현재 목록 출력 가능 제품 모두 선택" }).check();
  await expect(page.getByText("3개 선택", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "선택 QR 인쇄", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.locator(".busbar-qr-preview img")).toHaveCount(3);
  await expect(dialog.getByRole("button", { name: "인쇄", exact: true })).toBeEnabled();
  expect(await dialog.locator(".osan-qr-label").first().evaluate((element) => (element as HTMLElement).style.getPropertyValue("--label-size"))).toBe("30mm");
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await page.screenshot({ path: test.info().outputPath(`emi-busbar-bulk-qr-${width}.png`) });
  }
  await dialog.getByLabel("50 × 50mm").check();
  expect(await dialog.locator(".osan-qr-label").first().evaluate((element) => (element as HTMLElement).style.getPropertyValue("--label-size"))).toBe("50mm");
  await page.evaluate(() => {
    const observer = new MutationObserver((records) => {
      for (const record of records) for (const node of record.addedNodes) {
        if (!(node instanceof HTMLIFrameElement) || node.title !== "부스바 QR 인쇄" || !node.contentWindow) continue;
        node.contentWindow.print = () => {
          const printDocument = node.contentDocument!;
          document.body.dataset.busbarPrintEvidence = JSON.stringify({
            sheets: printDocument.querySelectorAll(".sheet").length,
            labels: printDocument.querySelectorAll(".osan-qr-label").length,
            images: Array.from(printDocument.images).every((image) => image.complete && image.naturalWidth > 0),
            page50: printDocument.head.textContent?.includes("@page{size:50mm 50mm;margin:0}"),
          });
          observer.disconnect();
        };
      }
    });
    observer.observe(document.body, { childList: true });
  });
  await dialog.getByRole("button", { name: "인쇄", exact: true }).click();
  await expect(page.locator("body")).toHaveAttribute("data-busbar-print-evidence", JSON.stringify({ sheets: 3, labels: 3, images: true, page50: true }));
  data.products[0].status = "Cancelled";
  await dialog.getByRole("button", { name: "인쇄", exact: true }).click();
  await expect(dialog.getByRole("alert")).toBeVisible();
  await expect(dialog.locator(".busbar-qr-preview img")).toHaveCount(0);
});
test("one failed QR request never offers partial labels", async ({ page }) => {
  const data = publishedFixture(); await mock(page, data);
  await page.route("**/products/published-2/qr", (route) => route.fulfill({ status: 409, contentType: "application/json", body: JSON.stringify({ message: "QR 준비 실패" }) }));
  await page.goto("/interior-busbar"); await selectSection(page, "생산·사진·QR");
  await page.getByRole("checkbox", { name: "현재 목록 출력 가능 제품 모두 선택" }).check();
  await page.getByRole("button", { name: "선택 QR 인쇄", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByRole("alert")).toContainText("QR 준비 실패");
  await expect(dialog.locator(".busbar-qr-preview img")).toHaveCount(0);
  await expect(dialog.getByRole("button", { name: "인쇄", exact: true })).toHaveCount(0);
});

test("project status filter combines with text search", async ({ page }) => {
  const data = fixture(), project = data.projects[0];
  data.projects = [
    { ...project, id: "a", name: "합성 A 진행" },
    { ...project, id: "b", name: "합성 A 완료", shippedQuantity: 60 },
    { ...project, id: "c", name: "합성 B 진행" },
  ];
  await mock(page, data); await page.goto("/interior-busbar");
  await selectSection(page, "납품 프로젝트");
  const table = page.getByRole("region", { name: "프로젝트명 목록", exact: true });
  await expect(table.getByRole("row")).toHaveCount(4);
  await page.getByLabel("검색", { exact: true }).fill("합성 A");
  await page.getByLabel("프로젝트 상태", { exact: true }).selectOption("InProgress");
  await expect(table.getByRole("row")).toHaveCount(2);
  await expect(table.getByRole("cell", { name: "합성 A 진행", exact: true })).toBeVisible();
  await page.getByLabel("프로젝트 상태", { exact: true }).selectOption("Complete");
  await expect(table.getByRole("row")).toHaveCount(2);
  await expect(table.getByRole("cell", { name: "합성 A 완료", exact: true })).toBeVisible();
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
  await selectSection(page, "생산·사진·QR");
  const firstResponse = page.waitForResponse((response) => new URL(response.url()).pathname === `/api/interior-busbar/products/${productId}`);
  await page.getByRole("button", { name: "사진등록", exact: true }).first().click();
  await page.getByRole("button", { name: "사진 팝업 닫기" }).click();
  await page.getByRole("button", { name: "사진등록", exact: true }).nth(1).click();
  await expect(page.getByRole("dialog")).toHaveAttribute("aria-label", "2026-09-09 · 대기 2번 · 합성 제품군 A");
  release();
  await firstResponse;
  await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => resolve())));
  await expect(page.getByRole("dialog")).toContainText("합성 두번째 작업자");
  await expect(page.getByRole("dialog")).toHaveAttribute("aria-label", "2026-09-09 · 대기 2번 · 합성 제품군 A");
});


test("QR changed during preparation cannot create a printable preview", async ({ page }) => {
  const data = publishedFixture(); await mock(page, data);
  await page.route("**/products/published-2/qr", async (route) => {
    data.products[0].revision++;
    await route.fulfill({ contentType: "image/png", body: qrPng });
  });
  await page.goto("/interior-busbar"); await selectSection(page, "생산·사진·QR");
  await page.getByRole("checkbox", { name: "현재 목록 출력 가능 제품 모두 선택" }).check();
  await page.getByRole("button", { name: "선택 QR 인쇄", exact: true }).click();
  await expect(page.getByRole("dialog").getByRole("alert")).toContainText("게시 상태가 변경됐습니다");
  await expect(page.locator(".busbar-qr-preview img")).toHaveCount(0);
  await page.emulateMedia({ media: "print" });
  await expect(page.locator(".busbar-print-sheet")).toHaveCount(0);
  await expect(page.locator(".busbar-print-dialog")).toBeHidden();
});

test("calendar today stays pastel red when selected", async ({ page }) => {
  await page.clock.setFixedTime(new Date("2026-09-10T06:00:00Z"));
  await mock(page, fixture()); await page.goto("/interior-busbar");
  await selectSection(page, "생산계획");
  const day = page.getByRole("button", { name: "2026-09-10 생산계획 선택", exact: true });
  await expect(day).toHaveAttribute("aria-current", "date");
  await expect(day).toHaveCSS("background-color", "rgb(254, 226, 226)");
  await day.click(); await page.getByRole("button", { name: "생산계획 팝업 닫기" }).click();
  await expect(day).toHaveAttribute("aria-pressed", "true");
  await expect(day).toHaveCSS("background-color", "rgb(254, 226, 226)");
  await expect(day).toHaveCSS("outline-color", "rgb(37, 99, 235)");
  await page.screenshot({ path: test.info().outputPath("emi-busbar-calendar-today.png"), fullPage: true });
});


test("deadline rows highlight only today through D-3 and selected calendar day is gray", async ({ page }) => {
  await page.clock.setFixedTime(new Date("2026-09-10T06:00:00Z"));
  const data = fixture(), project = data.projects[0];
  data.projects = ["09", "10", "13", "14"].map((day) => ({ ...project, id: `day-${day}`, name: `합성 납기 ${day}`, dueDate: `2026-09-${day}` }));
  await mock(page, data);
  await page.goto("/interior-busbar");
  for (const day of ["09", "10", "13", "14"]) {
    const row = page.getByRole("row").filter({ hasText: `합성 납기 ${day}` });
    if (["10", "13"].includes(day)) await expect(row.getByRole("cell").first()).toHaveCSS("background-color", "rgb(254, 249, 195)");
    else await expect(row).not.toHaveClass(/busbar-due-soon/);
  }
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await page.screenshot({ path: test.info().outputPath(`emi-busbar-deadline-${width}.png`), fullPage: true });
  }
  await selectSection(page, "생산계획");
  const day = page.getByRole("button", { name: "2026-09-11 생산계획 선택", exact: true });
  await day.click();
  await page.keyboard.press("Escape");
  await expect(day).toHaveCSS("background-color", "rgba(0, 0, 0, 0)");
  await expect(day.locator(".busbar-calendar-date b")).toHaveCSS("background-color", "rgb(40, 40, 40)");
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await page.screenshot({ path: test.info().outputPath(`emi-busbar-gray-calendar-${width}.png`), fullPage: true });
  }
});


test("worker correction is in photo dialog and publication retry stays in external column", async ({ page }) => {
  const data = fixture();
  data.products[0] = { ...data.products[0], status: "Complete", number: "IB-00000001", manufacturedAtUtc: "2026-09-10T01:00:00Z", publicationState: "Failed" };
  const writes = await mock(page, data);
  await page.goto("/interior-busbar");
  await selectSection(page, "생산·사진·QR");
  const row = page.getByRole("row").filter({ hasText: "IB-00000001" });
  await expect(row.getByRole("cell").last().getByRole("button", { name: "게시 재시도" })).toBeVisible();
  await row.getByRole("button", { name: "게시 재시도" }).click();
  expect(writes[0].path).toBe(`/api/interior-busbar/products/${productId}/publication/retry`);
  await row.getByRole("button", { name: "사진보기" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByRole("button", { name: "게시 재시도" })).toHaveCount(0);
  await expect(dialog.getByRole("button", { name: /생산 취소$/ })).toBeVisible();
  await dialog.getByRole("button", { name: "작업자 정정", exact: true }).click();
  await expect(dialog.getByLabel("정정 사유", { exact: true })).toHaveAttribute("required", "");
  await dialog.getByLabel("정정 사유", { exact: true }).fill("합성 작업자 확인");
  await dialog.getByRole("button", { name: "저장", exact: true }).click();
  expect(writes.some((item) => item.path === `/api/interior-busbar/products/${productId}`)).toBe(true);
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await page.screenshot({ path: test.info().outputPath(`emi-busbar-worker-correction-${width}.png`), fullPage: true });
  }
});


test("commercial price is family only and preview stays read only", async ({page}) => {
  const data=fixture();
  data.productFamilies[0].standardUnitPrice=12500;
  data.productFamilies[0].ecountProductCode="SYN-P";
  const writes=await mock(page,data);
  await page.goto("/interior-busbar");
  await selectSection(page, "기준정보");
  await expect(page.getByRole("cell",{name:"12,500",exact:true})).toBeVisible();
  await selectSection(page, "납품 프로젝트");
  await page.getByRole("button",{name:"프로젝트 등록",exact:true}).click();
  await page.getByRole("combobox",{name:"제품군",exact:true}).selectOption(familyId);
  await expect(page.getByLabel("적용 단가 (원·부가세 별도)")).toHaveCount(0);
  await page.getByLabel("프로젝트명",{exact:true}).fill("합성 가격 확인");
  await page.getByLabel("LSE Task No").fill("SYN-WO");
  await page.getByLabel("요청 수량",{exact:true}).fill("60");
  await page.getByLabel("도착지 / 업체명",{exact:true}).fill("합성 도착지");
  for (const width of [1440,390]) {
    await page.setViewportSize({width,height:1000});
    await page.screenshot({path:test.info().outputPath(`emi-busbar-commercial-editor-${width}.png`),fullPage:true});
    expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth+1)).toBe(true);
  }
  await page.getByRole("button",{name:"저장",exact:true}).click();
  await expect.poll(()=>writes.filter(w=>w.path.endsWith("/projects")).length).toBe(1);
  expect(writes.find(w=>w.path.endsWith("/projects"))?.body).toMatchObject({customerJobNumber:"SYN-WO"});
  expect(writes.find(w=>w.path.endsWith("/projects"))?.body).not.toHaveProperty("unitPrice");
  await page.getByText("합성 납품 현장",{exact:true}).click();
  await expect(page.getByText("750,000원",{exact:true})).toBeVisible();
  for (const width of [1440,390]) {
    await page.setViewportSize({width,height:1000});
    await page.getByText("공급가액",{exact:true}).scrollIntoViewIfNeeded();
    await page.screenshot({path:test.info().outputPath(`emi-busbar-commercial-preview-${width}.png`)});
  }
  expect(writes.filter(w=>w.path.endsWith("/projects")).length).toBe(1);
});

test("ecount status blocks uncertain retries and records a reason for definite failure", async ({page}) => {
  const writes = await mock(page, fixture());
  let retried = false;
  await page.route("**/api/interior-busbar/projects/*/ecount-status", route => route.fulfill({contentType:"application/json", body:JSON.stringify({transmissionEnabled:false,jobs:[
    {id:projectId,kind:"Order",state:retried?"Pending":"Failed",needsReview:false,message:retried?null:"전표 미생성 확인",slipNumber:null,attemptCount:1},
    {id:familyId,kind:"Sale",state:"Unknown",needsReview:false,message:"전표 생성 여부 확인 필요",slipNumber:null,attemptCount:1}
  ]})}));
  await page.goto("/interior-busbar");
  await selectSection(page, "납품 프로젝트");
  await page.getByText("합성 납품 현장", {exact:true}).click();
  await expect(page.getByText("결과 확인 필요", {exact:true})).toBeVisible();
  await expect(page.getByRole("button", {name:"다시 대기",exact:true})).toHaveCount(1);
  await page.getByRole("button", {name:"다시 대기",exact:true}).click();
  await page.getByLabel("다시 대기 사유").fill("합성 설정 오류 수정");
  for (const width of [1440,390]) {
    await page.setViewportSize({width,height:1000});
    await page.getByRole("heading", {name:"이카운트 주문·판매"}).scrollIntoViewIfNeeded();
    await page.screenshot({path:test.info().outputPath(`emi-busbar-ecount-status-${width}.png`)});
    expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth+1)).toBe(true);
  }
  retried = true;
  await page.getByRole("button", {name:"대기 등록",exact:true}).click();
  await expect.poll(()=>writes.filter(w=>w.path.endsWith("/retry")).length).toBe(1);
  expect(writes.find(w=>w.path.endsWith("/retry"))?.body).toEqual({reason:"합성 설정 오류 수정"});
  await expect(page.getByRole("region", {name:"이카운트 전송 상태",exact:true}).getByText("전송 대기", {exact:true})).toBeVisible();
  await expect(page.getByRole("button", {name:"다시 대기",exact:true})).toHaveCount(0);
});


test("ecount connection resumes separately and manual verification records checked slip", async ({page}) => {
  const writes=await mock(page,fixture());
  await page.route("**/api/interior-busbar/projects/*/commercial-preview",route=>route.fulfill({contentType:"application/json",body:JSON.stringify({unitPrice:12500,quantity:60,supplyAmount:750000,vatAmount:75000,totalAmount:825000,missingFields:[],transmissionEnabled:true})}));
  await page.route("**/api/interior-busbar/projects/*/ecount-status",route=>route.fulfill({contentType:"application/json",body:JSON.stringify({transmissionEnabled:true,environment:"Test",paused:true,connectionMessage:"전표 생성 여부 확인 필요 · 자동 전송 중지",jobs:[
    {id:projectId,kind:"Order",state:"Unknown",needsReview:false,message:"전표 생성 여부 확인 필요",slipNumber:null,attemptCount:1}
  ]})}));
  await page.goto("/interior-busbar");
  await selectSection(page, "납품 프로젝트");
  await page.getByText("합성 납품 현장",{exact:true}).click();
  await expect(page.getByText("테스트 연결 · 자동 전송 중지",{exact:true})).toBeVisible();
  await page.getByRole("button",{name:"자동 전송 재개",exact:true}).click();
  await page.getByLabel("재개 사유",{exact:true}).fill("합성 연결 설정 확인");
  await page.getByRole("button",{name:"재개 요청",exact:true}).click();
  await expect.poll(()=>writes.filter(w=>w.path.endsWith("/ecount/resume")).length).toBe(1);
  await page.getByRole("button",{name:"전표 확인 반영",exact:true}).click();
  await page.getByLabel("확인한 전표번호",{exact:true}).fill("SYN-ORDER-001");
  await page.getByLabel("확인 사유",{exact:true}).fill("합성 이카운트 전표 조회 확인");
  for(const width of [1440,390]) {
    await page.setViewportSize({width,height:1000});
    await page.getByRole("heading",{name:"이카운트 주문·판매"}).scrollIntoViewIfNeeded();
    await page.locator(".busbar-ecount-status").screenshot({path:test.info().outputPath(`emi-busbar-ecount-connection-${width}.png`)});
    expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth+1)).toBe(true);
  }
  await page.getByRole("button",{name:"확인 반영",exact:true}).click();
  await expect.poll(()=>writes.filter(w=>w.path.endsWith("/reconcile")).length).toBe(1);
  expect(writes.find(w=>w.path.endsWith("/reconcile"))?.body).toEqual({outcome:"Recorded",slipNumber:"SYN-ORDER-001",reason:"합성 이카운트 전표 조회 확인"});
  expect(writes.filter(w=>w.path.endsWith("/retry"))).toHaveLength(0);
});


test("employee name is automatic without a mapping editor", async ({page}) => {
  await mock(page,fixture());
  await page.goto("/interior-busbar/masters");
  await expect(page.getByText("주문서·판매 담당자는 프로젝트 최초 등록자의 PMS 이름으로 자동 입력됩니다.")).toBeVisible();
  await expect(page.getByRole("button",{name:"담당자 연결",exact:true})).toHaveCount(0);
  for(const width of [1440,390]) {
    await page.setViewportSize({width,height:1000});
    await page.screenshot({path:test.info().outputPath(`emi-busbar-employee-auto-${width}.png`),fullPage:true});
    expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1)).toBe(true);
  }
});

test("independent busbar URLs survive reload and browser history", async ({ page }) => {
  await mock(page, fixture());
  await page.setViewportSize({ width: 1440, height: 900 });
  for (const [path, label] of [["overview", "홈"], ["projects", "프로젝트"], ["plans", "생산계획"], ["purchases", "발주, 입고관리"], ["production", "생산"], ["masters", "기준정보"]]) {
    await page.goto(`/interior-busbar/${path}`);
    await expect(page.getByRole("heading", { name: label, exact: true }).first()).toBeVisible();
    await expect(page.getByRole("tablist", { name: "인테리어 부스바 업무" })).toHaveCount(0);
    await page.reload();
    await expect(page.locator(".busbar-page-heading h1")).toHaveText(label);
  }
  await selectSection(page, "프로젝트");
  await page.getByRole("button", { name: "프로젝트 등록", exact: true }).click();
  await expect(page.getByRole("region", { name: "납품 프로젝트 등록", exact: true })).toBeVisible();
  await page.goBack();
  await expect(page.locator(".busbar-page-heading h1")).toHaveText("기준정보");
  await expect(page.getByRole("region", { name: "납품 프로젝트 등록", exact: true })).toHaveCount(0);
  await page.goForward();
  await expect(page.locator(".busbar-page-heading h1")).toHaveText("프로젝트");
});

test("shipment sales show individual quantities and ERP slips on desktop and mobile", async ({page}) => {
  await mock(page, fixture());
  await page.route("**/api/interior-busbar/projects/*/ecount-status", route => route.fulfill({contentType:"application/json",body:JSON.stringify({transmissionEnabled:true,environment:"Test",paused:false,jobs:[
    {id:projectId,kind:"Order",state:"Succeeded",needsReview:false,message:null,slipNumber:"2026/09/15 -1",attemptCount:1},
    {id:"sale-one",kind:"Sale",shipmentId:"shipment-one",shipmentQuantity:20,shippedAtUtc:"2026-09-15T00:00:00Z",state:"Succeeded",needsReview:false,message:null,slipNumber:"2026/09/15 -2",attemptCount:1},
    {id:"sale-two",kind:"Sale",shipmentId:"shipment-two",shipmentQuantity:15,shippedAtUtc:"2026-09-15T01:00:00Z",state:"Pending",needsReview:false,message:null,slipNumber:null,attemptCount:0}
  ]})}));
  await page.goto("/interior-busbar/projects");
  await page.getByText("합성 납품 현장",{exact:true}).click();
  const status = page.locator(".busbar-ecount-status");
  await expect(status.getByRole("cell",{name:"출하별 판매",exact:true})).toHaveCount(2);
  await expect(status.getByRole("cell",{name:"20",exact:true})).toBeVisible();
  await expect(status.getByRole("cell",{name:"15",exact:true})).toBeVisible();
  await expect(status.getByRole("cell",{name:"2026/09/15 -2",exact:true})).toBeVisible();
  for (const width of [1440,390]) {
    await page.setViewportSize({width,height:1000});
    await status.scrollIntoViewIfNeeded();
    await status.screenshot({path:test.info().outputPath(`emi-busbar-shipment-sales-${width}.png`)});
    expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth+1)).toBe(true);
  }
});

test("department permissions show only owned input actions and LSE Task No", async ({ page }) => {
  for (const team of ["sales", "planning", "production", "admin"]) {
    const data = fixture();
    data.permissions = { projects: team === "sales" || team === "admin", planning: team === "planning" || team === "admin",
      purchases: team === "planning" || team === "admin", production: team === "production" || team === "admin", administration: team === "admin" };
    await mock(page, data);
    await page.goto("/interior-busbar/projects");
    await expect(page.getByRole("button", {name:"프로젝트 등록",exact:true})).toHaveCount(data.permissions.projects ? 1 : 0);
    await expect(page.getByRole("columnheader", {name:"LSE Task No",exact:true})).toBeVisible();
    if (team === "sales") {
      await page.getByRole("button", {name:"프로젝트 등록",exact:true}).click();
      await expect(page.getByLabel("LSE Task No (선택)",{exact:true})).toBeVisible();
    }
    await page.goto("/interior-busbar/purchases");
    await expect(page.getByRole("button", {name:"자재 기초재고",exact:true})).toHaveCount(data.permissions.administration ? 1 : 0);
    await page.goto("/interior-busbar/production");
    await expect(page.getByRole("button", {name:"사진등록",exact:true})).toHaveCount(data.permissions.production ? 1 : 0);
    await page.screenshot({path:test.info().outputPath(`emi-busbar-access-${team}.png`),fullPage:true});
  }
});

test("weekend date colors survive today and selection on desktop and mobile", async ({ page }) => {
  await page.clock.setFixedTime(new Date("2026-09-12T06:00:00Z"));
  await mock(page,fixture());
  for (const width of [1440,390]) {
    await page.setViewportSize({width,height:900});
    await page.goto("/interior-busbar/plans");
    const saturday = page.getByRole("button",{name:"2026-09-12 생산계획 선택",exact:true});
    const sunday = page.getByRole("button",{name:"2026-09-13 생산계획 선택",exact:true});
    const unselectedSaturday = page.getByRole("button",{name:"2026-09-05 생산계획 선택",exact:true});
    const unselectedSunday = page.getByRole("button",{name:"2026-09-06 생산계획 선택",exact:true});
    await expect(saturday.locator(".busbar-calendar-date")).toHaveCSS("color","rgb(37, 99, 235)");
    await expect(unselectedSaturday.locator(".busbar-calendar-date b")).toHaveCSS("color", width === 390 ? "rgb(40, 100, 189)" : "rgb(37, 41, 48)");
    await expect(unselectedSunday.locator(".busbar-calendar-date b")).toHaveCSS("color", width === 390 ? "rgb(200, 42, 50)" : "rgb(37, 41, 48)");
    await expect(saturday).toHaveCSS("background-color", width === 390 ? "rgba(0, 0, 0, 0)" : "rgb(254, 226, 226)");
    if (width === 390) {
      await expect(saturday.locator(".busbar-calendar-date b")).toHaveCSS("background-color", "rgb(40, 100, 189)");
      await expect(saturday.locator(".busbar-calendar-date b")).toHaveCSS("color", "rgb(255, 255, 255)");
    }
    await sunday.click(); await page.getByRole("button", {name:"생산계획 팝업 닫기"}).click();
    await expect(sunday.locator(".busbar-calendar-date")).toHaveCSS("color","rgb(220, 38, 38)");
    await expect(sunday).toHaveCSS("background-color", width === 390 ? "rgba(0, 0, 0, 0)" : "rgb(241, 245, 249)");
    if (width === 390) {
      await expect(sunday.locator(".busbar-calendar-date b")).toHaveCSS("background-color", "rgb(200, 42, 50)");
      await expect(sunday.locator(".busbar-calendar-date b")).toHaveCSS("color", "rgb(255, 255, 255)");
    }
    await page.screenshot({path:test.info().outputPath(`emi-busbar-weekend-${width}.png`),fullPage:true});
  }
});

test("home shows current work, near deliveries and material shortage instead of cumulative totals", async ({page}) => {
  const data=fixture();
  data.overview={asOfDate:"2026-09-15",productionToday:[{productFamilyId:familyId,quantity:3}]};
  data.productFamilies[0].plannedQuantity=99999;data.productFamilies[0].producedQuantity=88888;
  const base=data.projects[0];
  data.projects=[
    {...base,id:"late",name:"합성 지연",dueDate:"2026-09-14",requestedQuantity:20,shippedQuantity:10},
    {...base,id:"soon",name:"합성 임박",dueDate:"2026-09-18",requestedQuantity:40,shippedQuantity:10},
    {...base,id:"edge",name:"합성 7일",dueDate:"2026-09-22",requestedQuantity:5,shippedQuantity:0},
    {...base,id:"future",name:"합성 먼 미래",dueDate:"2026-09-23"},
    {...base,id:"done",name:"합성 완료",dueDate:"2026-09-14",requestedQuantity:10,shippedQuantity:10}];
  data.plans=[{id:"old",productFamilyId:familyId,planDate:"2026-09-14",quantity:10,actualQuantity:8},
    {id:"today",productFamilyId:familyId,planDate:"2026-09-15",quantity:5,actualQuantity:1}];
  await mock(page,data);
  for(const width of [1440,390]) {
    await page.setViewportSize({width,height:1000});await page.goto("/interior-busbar/overview");
    const projects=page.getByRole("region",{name:"진행중인 프로젝트",exact:true});
    await expect(projects.getByText("합성 먼 미래")).toHaveCount(0);await expect(projects.getByText("합성 완료",{exact:true})).toHaveCount(0);
    await expect(projects.getByRole("row").filter({hasText:"합성 지연"}).getByRole("cell").first()).toHaveCSS("background-color","rgb(254, 226, 226)");
    await expect(projects.getByRole("row").filter({hasText:"합성 임박"}).getByRole("cell").first()).toHaveCSS("background-color","rgb(254, 249, 195)");
    await expect(projects.getByText("합성 7일")).toBeVisible();
    await expect(projects.getByRole("row").filter({hasText:"합성 지연"}).getByRole("cell")).toHaveText(["납기 지연","합성 지연","합성 업체","합성 제품군 A","2026-09-14","20","10","10"]);
    await expect(page.getByRole("columnheader",{name:"전체 계획",exact:true})).toHaveCount(0);
    const family=page.getByRole("region",{name:"제품군별 현황",exact:true}).getByRole("row").last();
    await expect(family.getByRole("cell")).toHaveText(["합성 제품군 A","5","3","2","30","45","15"]);
    const material=page.getByRole("region",{name:"부족 자재",exact:true}).getByRole("row").last();
    await expect(material.getByRole("cell")).toHaveText(["합성 동대","m","-2","12","14"]);
    await page.screenshot({path:test.info().outputPath(`emi-busbar-home-current-${width}.png`),fullPage:true});
    expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth+1)).toBeTruthy();
  }
});


test("shipment button stays visible with its unavailable reason", async ({ page }) => {
  const data = fixture();
  await mock(page, data);
  for (const reason of ["완제품 재고 없음", "출하 완료", "출하 권한 없음"]) {
    data.productFamilies[0].balance = reason === "완제품 재고 없음" ? 0 : 30;
    data.projects[0].shippedQuantity = reason === "출하 완료" ? data.projects[0].requestedQuantity : 0;
    data.permissions.projects = reason !== "출하 권한 없음";
    await page.goto(`/interior-busbar/projects/${projectId}`);
    const button = page.getByRole("button", { name: "패널 QR로 분할 출하", exact: true });
    await expect(button).toBeVisible();
    await expect(button).toBeDisabled();
    await expect(button).toHaveAccessibleDescription(reason);
    if (reason === "완제품 재고 없음") {
      for (const width of [1440, 390]) {
        await page.setViewportSize({ width, height: 900 });
        await page.screenshot({ path: test.info().outputPath(`emi-busbar-shipment-disabled-${width}.png`) });
      }
    }
  }
});

test("project Excel menu and monthly family totals", async ({ page }) => {
  const data = fixture();
  await mock(page, data);
  await page.route("**/api/interior-busbar/projects/import/preview", route => route.fulfill({ contentType: "application/json", body: JSON.stringify({ rows: [{name: "합성 업로드", requestedQuantity: 2}], errors: [] }) }));
  await page.goto("/interior-busbar/projects");
  const menu = page.getByRole("button", { name: "프로젝트 엑셀", exact: false });
  await expect(menu).toBeVisible();
  await expect(page.getByRole("button", {name:"양식 다운로드",exact:true})).toHaveCount(0);
  await menu.click();
  await expect(page.getByRole("button", {name:"양식 다운로드",exact:true})).toBeVisible();
  const chooser = page.waitForEvent("filechooser");
  await page.getByRole("button", {name:"업로드",exact:true}).click();
  await (await chooser).setFiles({name:"synthetic.xlsx",mimeType:"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",buffer:Buffer.from("synthetic")});
  await expect(page.getByRole("dialog", {name:"프로젝트 엑셀 미리보기"})).toBeVisible();
  await expect(page.getByText("합성 업로드",{exact:true})).toBeVisible();
  await page.getByRole("button",{name:"프로젝트 엑셀 미리보기 닫기"}).click();
  for (const width of [1440,390]) {
    await page.setViewportSize({width,height:900});
    await page.screenshot({path:test.info().outputPath(`emi-busbar-excel-menu-${width}.png`)});
  }
  await page.goto("/interior-busbar/plans");
  await selectPlanMonth(page, "2026-09");
  const totals = page.getByLabel("선택 월 제품군별 생산 현황");
  await expect(totals).toContainText("계획 60대");
  await expect(totals).toContainText("생산 완료 1대");
  for (const width of [1440,390]) {
    await page.setViewportSize({width,height:900});
    if (width === 1440) {
      const monthBox = await page.getByLabel("계획 월", {exact:true}).boundingBox();
      const nextBox = await page.getByRole("button", {name:"다음 달",exact:true}).boundingBox();
      expect(Math.abs((monthBox!.y + monthBox!.height) - (nextBox!.y + nextBox!.height))).toBeLessThan(3);
    }
    await page.screenshot({path:test.info().outputPath(`emi-busbar-month-totals-${width}.png`)});
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(true);
  }
  await selectPlanMonth(page, "2026-10");
  await expect(totals).toContainText("계획 0대");
  await expect(totals).toContainText("생산 완료 0대");
});

test("project detail edits its own project and places Ecount below shipments", async ({page}) => {
  const writes = await mock(page, fixture());
  await page.goto(`/interior-busbar/projects/${projectId}`);
  const basic = page.getByRole("region",{name:"프로젝트 기본 정보",exact:true});
  await expect(basic.getByText("공급가액",{exact:true})).toBeVisible();
  await expect(basic.getByText("합계",{exact:true})).toHaveCount(0);
  const shipping = page.getByRole("region",{name:"출하 이력",exact:true});
  const ecount = page.getByRole("region",{name:"이카운트 전송 상태",exact:true});
  expect((await shipping.boundingBox())!.y).toBeLessThan((await ecount.boundingBox())!.y);
  await page.getByRole("button",{name:"프로젝트 수정",exact:true}).click();
  const editor = page.getByRole("region",{name:"프로젝트 수정",exact:true});
  await expect(editor.getByLabel("프로젝트명",{exact:true})).toHaveValue("합성 납품 현장");
  await editor.getByLabel("도착지 / 업체명",{exact:true}).fill("수정된 합성 도착지");
  await editor.getByLabel("정정 사유",{exact:true}).fill("도착지 확인");
  await editor.getByRole("button",{name:"저장",exact:true}).click();
  await expect(editor).toHaveCount(0);
  expect(writes.find(w=>w.path.endsWith("/projects"))?.body).toMatchObject({id:projectId,destination:"수정된 합성 도착지",reason:"도착지 확인"});
});


test("masters designation controls menu read edit and administrator grants", async ({page}) => {
  test.setTimeout(60000);
  for (const mode of ["None","Read","Edit","Admin"]) {
    const data=fixture();
    data.permissions={projects:true,planning:false,production:false,purchases:false,administration:mode === "Admin",mastersRead:mode !== "None",mastersWrite:["Edit","Admin"].includes(mode),manageMasterPermissions:mode === "Admin"};
    const writes=await mock(page,data);
    await page.goto("/interior-busbar/purchases");
    await expect(page.getByRole("heading",{name:"입고·생산·출하·재고 정정 이력"})).toHaveCount(0);
    if(mode === "None") {
      await expect(page.getByRole("button",{name:"기준정보",exact:true})).toHaveCount(0);
      await page.goto("/interior-busbar/masters");
      await expect(page.getByText("기준정보 접근 권한이 없습니다.")).toBeVisible();
      await expect(page.getByRole("heading",{name:"제품군",exact:true})).toHaveCount(0);
      continue;
    }
    await selectSection(page,"기준정보");
    await expect(page.getByRole("heading",{name:"제품군",exact:true})).toBeVisible();
    await expect(page.getByRole("button",{name:"제품군 등록",exact:true})).toHaveCount(mode === "Read" ? 0 : 1);
    await expect(page.getByRole("heading",{name:"기준정보 접근 사용자",exact:true})).toHaveCount(mode === "Admin" ? 1 : 0);
    if(mode === "Admin") {
      await page.getByRole("button",{name:"권한 설정 수정",exact:true}).click();
      await page.getByRole("checkbox",{name:"합성 사용자 수정 권한",exact:true}).click();
      await expect.poll(()=>writes.filter(w=>w.path.endsWith("/master-access")).length).toBe(1);
      expect(writes.find(w=>w.path.endsWith("/master-access"))?.body).toMatchObject({userId:workerId,access:"Edit"});
    }
  }
});

test("master access popup toggles immediately and keeps a hundred users out of the page", async ({page}) => {
  await mock(page,fixture());
  const users=Array.from({length:100},(_,i)=>({userId:`user-${i}`,displayName:`합성 사용자 ${i}`,departmentName:"합성 부서",access:i===1 ? "Read" : "None",automatic:i===0}));
  const writes:Array<{userId:string;access:string;reason:string}>=[];
  let failAfterSave=false;
  await page.route("**/api/interior-busbar/master-access",async route=>{
    if(route.request().method()==="PUT") {
      const body=route.request().postDataJSON();writes.push(body);
      users.find(u=>u.userId===body.userId)!.access=body.access;
      await route.fulfill({status:failAfterSave?500:200,contentType:"application/json",body:JSON.stringify(failAfterSave?{message:"합성 응답 오류"}:{id:body.userId})});
      return;
    }
    await route.fulfill({contentType:"application/json",body:JSON.stringify(users)});
  });
  await page.goto("/interior-busbar/masters");
  const granted=page.getByRole("list",{name:"기준정보 접근 사용자"});
  await expect(granted.getByRole("listitem")).toHaveCount(2);
  await expect(granted.getByText("합성 사용자 99",{exact:true})).toHaveCount(0);
  for(const width of [1440,390]) {
    await page.setViewportSize({width,height:1000});
    await page.screenshot({path:test.info().outputPath(`emi-busbar-access-summary-${width}.png`),fullPage:true});
  }
  await page.getByRole("button",{name:"권한 설정 수정",exact:true}).click();
  const dialog=page.getByRole("dialog",{name:"기준정보 권한 설정 수정"});
  await expect(dialog.getByRole("checkbox")).toHaveCount(200);
  await expect(dialog.getByRole("checkbox",{name:"합성 사용자 0 조회 권한",exact:true})).toBeDisabled();
  for(const width of [1440,390]) {
    await page.setViewportSize({width,height:1000});
    await page.screenshot({path:test.info().outputPath(`emi-busbar-access-popup-${width}.png`),fullPage:true});
    expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1)).toBe(true);
    expect(await page.locator(".busbar-access-list").evaluate(el=>el.scrollHeight>el.clientHeight && el.clientHeight<=innerHeight*.51)).toBe(true);
    expect(await page.locator(".busbar-access-list").evaluate(el=>el.scrollWidth<=el.clientWidth+1)).toBe(true);
    expect(await dialog.evaluate(el=>Math.abs(el.getBoundingClientRect().width-innerWidth)<2)).toBe(true);
  }
  await dialog.getByLabel("사용자 검색").fill("합성 사용자 99");
  const read=dialog.getByRole("checkbox",{name:"합성 사용자 99 조회 권한",exact:true});
  const edit=dialog.getByRole("checkbox",{name:"합성 사용자 99 수정 권한",exact:true});
  await read.click();await expect.poll(()=>writes.length).toBe(1);await expect(read).toBeEnabled();
  await edit.click();await expect.poll(()=>writes.length).toBe(2);await expect(edit).toBeEnabled();
  await edit.click();await expect.poll(()=>writes.length).toBe(3);await expect(edit).toBeEnabled();
  await expect(read).toBeChecked();
  await read.click();await expect.poll(()=>writes.length).toBe(4);await expect(read).toBeEnabled();
  expect(writes.map(w=>w.access)).toEqual(["Read","Edit","Read","None"]);
  failAfterSave=true;
  await edit.click();
  await expect(dialog.getByText("합성 응답 오류",{exact:true})).toBeVisible();
  await expect(edit).toBeChecked();await expect(read).toBeChecked();
  await page.getByRole("button",{name:"권한 설정 닫기"}).click();
  await expect(granted.getByRole("listitem")).toHaveCount(3);
  await page.reload();
  await expect(granted.getByRole("listitem")).toHaveCount(3);
});

test("purchase workspace shows remaining receipts and opens Excel beside registration", async ({ page }) => {
  const data = fixture();
  data.pagination!.ledgerCount = 8;
  data.purchases = [{ id: "synthetic-order", orderNumber: "SYN-PO-30", materialId, orderDate: "2026-09-09", quantity: 30, receivedQuantity: 20 }];
  await mock(page, data);
  await page.route("**/api/interior-busbar/purchases/import/preview", route => route.fulfill({contentType:"application/json", body:JSON.stringify({rows:[{orderNumber:"SYN-IMPORT",materialId,quantity:5}],errors:[]})}));
  await page.goto("/interior-busbar/purchases");
  await expect(page.getByRole("heading", { name:"발주·입고 현황" })).toBeVisible();
  await expect(page.getByRole("group", { name:"기록 페이지" })).toHaveCount(0);
  await expect(page.getByRole("row").filter({hasText:"SYN-PO-30"}).getByRole("cell",{name:"10",exact:true})).toBeVisible();
  const toolbar = page.getByRole("group",{name:"발주 검색"});
  await expect(toolbar.getByRole("button",{name:"발주 등록",exact:true})).toBeVisible();
  await toolbar.getByRole("button",{name:/발주 엑셀/}).click();
  await expect(toolbar.getByRole("button",{name:"양식 다운로드",exact:true})).toBeVisible();
  for (const width of [1440,390]) {
    await page.setViewportSize({width,height:900});
    await page.screenshot({path:test.info().outputPath(`emi-busbar-purchase-cleanup-${width}.png`),fullPage:true});
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(true);
  }
  const chooser = page.waitForEvent("filechooser");
  await toolbar.getByRole("button",{name:"업로드",exact:true}).click();
  await (await chooser).setFiles({name:"synthetic.xlsx",mimeType:"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",buffer:Buffer.from("synthetic")});
  const dialog = page.getByRole("dialog",{name:"발주 엑셀 미리보기",exact:true});
  await expect(dialog.getByText("SYN-IMPORT",{exact:true})).toBeVisible();
  await expect(dialog.getByRole("button",{name:"검토한 내용 적용"})).toBeEnabled();
  await page.getByRole("button",{name:"발주 엑셀 미리보기 닫기"}).click();
  await expect(dialog).toHaveCount(0);
});


for (const width of [1440, 390]) {
  test(`deletion and restore preserve project detail at ${width}`, async ({page}, testInfo) => {
    await page.setViewportSize({width, height:900});
    const data = fixture(); data.projects.push({...data.projects[0],id:"second-project",name:"보존할 다른 프로젝트"}); const writes = await mock(page, data);
    await page.goto(`/interior-busbar/projects/${projectId}`);
    await page.getByRole("button", {name: `${data.projects[0].name} 삭제`, exact:true}).click();
    const dialog = page.getByRole("dialog", {name:"삭제 확인"});
    await expect(dialog.getByRole("button", {name:"삭제 확인",exact:true})).toBeDisabled();
    await dialog.getByLabel("삭제 사유").fill("합성 삭제 시험");
    await expect(dialog.getByText(/이카운트 전표는 자동 취소하지 않습니다/)).toBeVisible();
    await page.screenshot({path:testInfo.outputPath(`delete-confirm-${width}.png`),fullPage:true});
    await dialog.getByRole("button", {name:"삭제 확인",exact:true}).click();
    await expect(page.getByText("삭제된 프로젝트입니다.", {exact:false})).toBeVisible();
    await expect(page.getByRole("button", {name:"프로젝트 수정",exact:true})).toHaveCount(0);
    await expect(page.getByRole("button", {name:"패널 QR로 분할 출하"})).toBeDisabled();
    await page.getByRole("button", {name:"프로젝트 목록",exact:true}).click();
    await expect(page.getByRole("button", {name:`${data.projects[0].name} 복원`,exact:true})).toHaveCount(0);
    await expect(page.getByRole("button",{name:"보존할 다른 프로젝트 삭제",exact:true})).toHaveCount(0);
    await expect(page.getByText("보존할 다른 프로젝트", {exact:true})).toBeVisible();
    await page.getByLabel("삭제된 항목 보기").check();
    await page.getByRole("button", {name:`${data.projects[0].name} 복원`,exact:true}).click();
    const restore = page.getByRole("dialog", {name:"복원 확인"});
    await restore.getByLabel("복원 사유").fill("합성 복원 시험");
    await restore.getByRole("button", {name:"복원 확인",exact:true}).click();
    await expect(page.getByLabel("삭제된 항목 보기")).toBeFocused();
    await expect(page.getByText(`${data.projects[0].name} 복원했습니다.`,{exact:true})).toBeVisible();
    await page.getByLabel("삭제된 항목 보기").uncheck();
    await expect(page.getByRole("button", {name:`${data.projects[0].name} 삭제`,exact:true})).toHaveCount(0);
    await expect(page.getByText(data.projects[0].name, {exact:true})).toBeVisible();
    expect(writes.map(w => w.path)).toEqual([`/api/interior-busbar/projects/${projectId}/delete`,`/api/interior-busbar/projects/${projectId}/restore`]);
    await page.screenshot({path:testInfo.outputPath(`restored-project-${width}.png`),fullPage:true});
  });
}


test("deletion controls cover plans purchases and master records", async ({page}) => {
  const data = fixture();
  data.purchases = [{id:"po",orderNumber:"합성 발주",materialId,quantity:2,receivedQuantity:0,orderDate:"2026-09-17"}];
  const writes = await mock(page,data);
  async function confirm(label: string) {
    await page.getByRole("button",{name:`${label} 삭제`,exact:true}).click();
    const dialog = page.getByRole("dialog",{name:"삭제 확인",exact:true});
    if (label.includes("계획")) await expect(dialog.getByText(/미착수 패널도 모두 취소됩니다/)).toBeVisible();
    await dialog.getByLabel("삭제 사유").fill("잘못 등록한 합성 자료");
    await dialog.getByRole("button",{name:"삭제 확인",exact:true}).click();
    await expect(page.getByRole("button",{name:`${label} 삭제`,exact:true})).toHaveCount(0);
  }
  await page.goto("/interior-busbar/plans");
  await selectPlanMonth(page, "2026-09");
  await page.getByRole("button",{name:"2026-09-09 생산계획 선택",exact:true}).click();
  await page.getByRole("button",{name:"합성 제품군 A 2026-09-09 계획 삭제",exact:true}).click();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog",{name:"삭제 확인",exact:true})).toHaveCount(0);
  await expect(page.getByRole("dialog",{name:"2026-09-09 제품군별 생산계획",exact:true})).toBeVisible();
  await confirm("합성 제품군 A 2026-09-09 계획");
  await page.getByRole("button",{name:"생산계획 팝업 닫기",exact:true}).click();
  await page.getByLabel("삭제된 항목 보기").check();
  await expect(page.getByRole("button",{name:"합성 제품군 A 2026-09-09 계획 복원",exact:true})).toBeVisible();
  await page.goto("/interior-busbar/purchases"); await confirm("합성 발주");
  await page.goto("/interior-busbar/masters");
  await confirm("합성 외주 작업자"); await confirm("합성 동대");
  await page.getByLabel("소요량을 관리할 제품군").selectOption(familyId);
  await confirm("합성 제품군 A 소요량 버전 1");
  await confirm("합성 제품군 A");
  await expect(page.getByRole("button",{name:"새 버전 저장",exact:true})).toHaveCount(0);
  expect(writes.filter(w => w.path.endsWith("/delete"))).toHaveLength(6);
});

test("deletion failure stays visible and preserves the record", async ({page}) => {
  const data = fixture(); await mock(page,data);
  await page.route(`**/api/interior-busbar/projects/${projectId}/delete`, route => route.fulfill({status:400,contentType:"application/json",body:JSON.stringify({message:"이카운트 전송 중입니다. 결과 확인 후 다시 시도하세요."})}));
  await page.goto(`/interior-busbar/projects/${projectId}`);
  await page.getByRole("button",{name:`${data.projects[0].name} 삭제`,exact:true}).click();
  const dialog=page.getByRole("dialog",{name:"삭제 확인",exact:true});
  await dialog.getByLabel("삭제 사유").fill("합성 오류 시험");
  await dialog.getByRole("button",{name:"삭제 확인",exact:true}).click();
  await expect(dialog.getByText("이카운트 전송 중입니다. 결과 확인 후 다시 시도하세요.")).toBeVisible();
  expect(data.projects[0].isDeleted).not.toBe(true);
  await expect(dialog.getByRole("button",{name:"삭제 확인",exact:true})).toBeEnabled();
});

test("deletion controls are hidden without existing write permissions", async ({page}) => {
  const data = fixture(false); await mock(page,data);
  await page.goto(`/interior-busbar/projects/${projectId}`);
  await expect(page.getByRole("button",{name:`${data.projects[0].name} 삭제`,exact:true})).toHaveCount(0);
  await page.goto('/interior-busbar/masters');
  await expect(page.getByRole('button',{name:'합성 제품군 A 삭제',exact:true})).toHaveCount(0);
  await page.goto('/interior-busbar/production');
  await page.getByRole('button',{name:'사진보기',exact:true}).click();
  await expect(page.getByRole('button',{name:/생산 취소$/})).toHaveCount(0);
});

test("administrators can cancel stock records with a reason", async ({page}) => {
  const data=fixture();
  data.ledger=[{id:'receipt',kind:'Receipt',reason:'합성 입고',createdAtUtc:'2026-09-17T01:00:00Z'}];
  const writes=await mock(page,data);
  await page.goto('/interior-busbar/purchases');
  await page.getByRole('button',{name:/입고 처리 취소$/}).click();
  const dialog=page.getByRole('dialog',{name:'처리 취소 확인',exact:true});
  await expect(dialog.getByText(/재고에 반대 수량/)).toBeVisible();
  await dialog.getByLabel('처리 취소 사유').fill('합성 입고 취소');
  await dialog.getByRole('button',{name:'처리 취소 확인',exact:true}).click();
  await expect(page.getByText("재고 처리를 취소했습니다. 기존 기록은 보존했습니다.",{exact:true})).toBeVisible();
  await expect(page.getByRole("heading",{name:"입고·재고 처리 이력",exact:true})).toBeFocused();
  expect(writes[0]).toMatchObject({path:'/api/interior-busbar/ledger/receipt/reverse',body:{reason:'합성 입고 취소'}});
  expect((writes[0].body as {requestId:string}).requestId).toMatch(/^[a-f0-9-]{36}$/);
  await page.goto('/interior-busbar/production');
  await page.getByRole('button',{name:'사진등록',exact:true}).click();
  await page.getByRole('button',{name:/생산 취소$/}).click();
  const cancel=page.getByRole('dialog',{name:'생산 취소 확인',exact:true});
  await cancel.getByLabel('생산 취소 사유').fill('합성 생산 취소');
  await cancel.getByRole('button',{name:'생산 취소 확인',exact:true}).click();
  await expect(page.getByText("생산을 취소했습니다. 사진과 처리 이력은 보존했습니다.",{exact:true})).toBeVisible();
  expect(writes[1]).toMatchObject({path:`/api/interior-busbar/products/${productId}/cancel`,body:{reason:'합성 생산 취소'}});
});

test("stock cancellation retry preserves request and reason", async ({page}) => {
  const data=fixture(); data.ledger=[{id:'retry-receipt',kind:'Receipt',reason:'합성 입고',createdAtUtc:'2026-09-17T01:00:00Z'}];
  await mock(page,data);
  const requests:unknown[]=[];
  await page.route('**/api/interior-busbar/ledger/retry-receipt/reverse', route => {
    requests.push(route.request().postDataJSON());
    return route.fulfill({status:requests.length===1 ? 503 : 200,contentType:'application/json',body:JSON.stringify(requests.length===1 ? {message:'합성 응답 지연'} : {id:'reversal'})});
  });
  await page.goto('/interior-busbar/purchases');
  await page.getByRole('button',{name:/입고 처리 취소$/}).click();
  const dialog=page.getByRole('dialog',{name:'처리 취소 확인',exact:true});
  await dialog.getByLabel('처리 취소 사유').fill('합성 재시도');
  await dialog.getByRole('button',{name:'처리 취소 확인',exact:true}).click();
  await expect(dialog.getByText('합성 응답 지연',{exact:true})).toBeVisible();
  await expect(dialog.getByLabel('처리 취소 사유')).toBeDisabled();
  await dialog.getByRole('button',{name:'처리 취소 확인',exact:true}).click();
  await expect(dialog.getByText('처리 취소했습니다.',{exact:true})).toBeVisible();
  expect(requests).toHaveLength(2); expect(requests[1]).toEqual(requests[0]);
});

test("photo upload supports HEIC previews and rejects unsupported input without submitting", async ({ page }) => {
  const data = fixture(); const writes = await mock(page, data);
  await page.goto("/interior-busbar"); await selectSection(page, "생산·사진·QR");
  await page.getByRole("button", { name: "사진등록", exact: true }).click();
  const input = page.getByLabel("앨범에서 앞면 선택");
  await expect(input).toHaveAttribute("accept", /image\/heic/);
  await input.setInputFiles({ name: "document.pdf", mimeType: "application/pdf", buffer: Buffer.from("unsupported") });
  await expect(page.getByText("JPEG·PNG·HEIC·WebP 사진을 선택해 주세요.", { exact: true })).toBeVisible();
  expect(writes.filter(w => w.path.includes("/photos/"))).toHaveLength(0);
  const preview = page.waitForRequest(r => r.method() === "GET" && r.url().includes("/photos/front?preview=true"));
  await input.setInputFiles({ name: "camera.HEIC", mimeType: "image/heic", buffer: png });
  await preview;
  await expect(page.getByRole("img", { name: "앞면 등록 사진" })).toBeVisible();
  expect(writes.filter(w => w.path.includes("/photos/"))).toHaveLength(1);
});
