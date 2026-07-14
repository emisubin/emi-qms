import fs from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type Page } from '@playwright/test';

const referenceCanvas = { width: 1440, height: 810 };
const referencePanels = { brandWidth: 776, loginWidth: 664 };
const referenceGeometry = {
  title: { scope: 'login', x: 109, y: 208.5, width: 447, height: 28.5 },
  emiLogo: { scope: 'brand', x: 219.75, y: 372, width: 329.563, height: 46.656 },
  microsoftLogo: { scope: 'login', x: 274, y: 306.75, width: 116.469, height: 148.219 },
  loginButton: { scope: 'login', x: 211, y: 510, width: 243, height: 48.75 },
  guidance: { scope: 'login', x: 109, y: 472, width: 447, height: 18 },
  remember: { scope: 'login', x: 269, y: 567, width: 126, height: 32 },
  ellipse68: { scope: 'brand', x: -538.5, y: -468, width: 876, height: 876 }
} as const;

const loadingReferenceGeometry = {
  title: referenceGeometry.title,
  emiLogo: referenceGeometry.emiLogo,
  microsoftLogo: referenceGeometry.microsoftLogo,
  guidance: referenceGeometry.guidance,
  loadingIndicator: { scope: 'login', x: 307.625, y: 530, width: 48.75, height: 48.75 },
  ellipse68: referenceGeometry.ellipse68
} as const;

test('approved Entra auth shell fills PC windows with the Figma panels and preserves element ratios', async ({ page }, testInfo) => {
  let consoleErrorCount = 0;
  let requestFailureCount = 0;

  page.on('console', (message) => {
    if (message.type() === 'error') {
      consoleErrorCount += 1;
    }
  });
  page.on('requestfailed', () => {
    requestFailureCount += 1;
  });

  await page.addInitScript(() => {
    window.localStorage.setItem('emi-auth-remember-session', 'false');
  });

  const response = await page.goto('/');
  await expect(page.getByRole('button', { name: 'LOGIN' })).toBeVisible();
  await waitForResponsiveCanvas(page);

  const projection = await readProjection(page);
  const result = {
    status: response?.status() ?? 0,
    ...projection,
    consoleErrorCount,
    requestFailureCount,
    failureCode: 'NONE'
  };

  console.log(JSON.stringify(result));

  const { geometry, viewport, ...stableResult } = result;
  expect(stableResult).toEqual({
    status: 200,
    pageLoaded: true,
    expectedStructurePresent: true,
    authState: 'login',
    authLayout: 'login',
    brandAssetCount: 2,
    primaryButtonCount: 1,
    primaryButtonDisabled: false,
    secondaryButtonCount: 0,
    checkboxCount: 1,
    checkboxDisabled: false,
    rememberChecked: false,
    rememberVisualVariant: 'DEFAULT',
    rememberFigmaVariantMatches: true,
    loginGuidanceCount: 1,
    loginGuidanceTextMatches: true,
    loadingGuidanceTextMatches: false,
    authPanelBusy: false,
    loadingIndicatorCount: 0,
    loadingIndicatorAnimated: false,
    loadingIndicatorRed: false,
    unexpectedLoginContentCount: 0,
    ellipse68PatternCount: 1,
    horizontalOverflowPixels: 0,
    verticalOverflowPixels: 0,
    blankPage: false,
    targetNotFoundPresent: false,
    figmaBackgroundContractMatches: true,
    figmaConnectionContractMatches: true,
    consoleErrorCount: 0,
    requestFailureCount: 0,
    failureCode: 'NONE'
  });

  assertResponsiveGeometry(geometry, viewport);

  const screenshotDirectory = process.env.AUTH_SHELL_SCREENSHOT_DIR?.trim()
    || '/tmp/emi-qms-task-design-login-001';
  await fs.mkdir(screenshotDirectory, { recursive: true });
  await page.screenshot({
    path: path.join(screenshotDirectory, `${testInfo.project.name}.png`),
    fullPage: true
  });

  await page.getByRole('checkbox', { name: '로그인 상태 유지' }).click();
  await expect(page.getByRole('checkbox', { name: '로그인 상태 유지' })).toBeChecked();
  const checkedProjection = await readProjection(page);
  expect(checkedProjection.rememberVisualVariant).toBe('VARIANT_2');
  expect(checkedProjection.rememberFigmaVariantMatches).toBe(true);
  expect(await page.evaluate(() => window.localStorage.getItem('emi-auth-remember-session'))).toBe('true');
  await page.evaluate(() => new Promise<void>((resolve) => {
    requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
  }));

  if (testInfo.project.name === 'desktop-1440') {
    await page.screenshot({
      path: path.join(screenshotDirectory, 'remember-variant-2-desktop-1440.png'),
      fullPage: true
    });
    await page.setViewportSize({ width: 1024, height: 768 });
    await waitForResponsiveCanvas(page);
    const resized = await readProjection(page);

    assertResponsiveGeometry(resized.geometry, resized.viewport);
    expect(resized.horizontalOverflowPixels).toBe(0);
    expect(resized.verticalOverflowPixels).toBe(0);
  }
});

test('loading auth shell replaces the login controls with one rotating red indicator', async ({ page }, testInfo) => {
  let consoleErrorCount = 0;
  let requestFailureCount = 0;

  page.on('console', (message) => {
    if (message.type() === 'error') {
      consoleErrorCount += 1;
    }
  });
  page.on('requestfailed', () => {
    requestFailureCount += 1;
  });

  const response = await page.goto('/e2e/auth-shell/loading.html');
  await expect(page.getByRole('status', { name: '로그인 확인 중' })).toBeVisible();
  await waitForResponsiveCanvas(page);

  const projection = await readProjection(page);
  const result = {
    status: response?.status() ?? 0,
    ...projection,
    consoleErrorCount,
    requestFailureCount,
    failureCode: 'NONE'
  };

  console.log(JSON.stringify(result));

  const { geometry, viewport, ...stableResult } = result;
  expect(stableResult).toEqual({
    status: 200,
    pageLoaded: true,
    expectedStructurePresent: true,
    authState: 'loading',
    authLayout: 'login',
    brandAssetCount: 2,
    primaryButtonCount: 0,
    primaryButtonDisabled: null,
    secondaryButtonCount: 0,
    checkboxCount: 0,
    checkboxDisabled: null,
    rememberChecked: null,
    rememberVisualVariant: 'ABSENT',
    rememberFigmaVariantMatches: null,
    loginGuidanceCount: 1,
    loginGuidanceTextMatches: false,
    loadingGuidanceTextMatches: true,
    authPanelBusy: true,
    loadingIndicatorCount: 1,
    loadingIndicatorAnimated: true,
    loadingIndicatorRed: true,
    unexpectedLoginContentCount: 0,
    ellipse68PatternCount: 1,
    horizontalOverflowPixels: 0,
    verticalOverflowPixels: 0,
    blankPage: false,
    targetNotFoundPresent: false,
    figmaBackgroundContractMatches: true,
    figmaConnectionContractMatches: true,
    consoleErrorCount: 0,
    requestFailureCount: 0,
    failureCode: 'NONE'
  });

  assertResponsiveGeometry(geometry, viewport, loadingReferenceGeometry);

  const screenshotDirectory = process.env.AUTH_SHELL_SCREENSHOT_DIR?.trim()
    || '/tmp/emi-qms-task-design-login-001';
  await fs.mkdir(screenshotDirectory, { recursive: true });
  await page.screenshot({
    path: path.join(screenshotDirectory, `loading-${testInfo.project.name}.png`),
    fullPage: true
  });
});

function assertResponsiveGeometry(
  geometry: Awaited<ReturnType<typeof readProjection>>['geometry'],
  viewport: { width: number; height: number },
  expectedGeometry: Record<string, {
    scope: 'brand' | 'login';
    x: number;
    y: number;
    width: number;
    height: number;
  }> = referenceGeometry
) {
  const expectedScale = Math.min(
    viewport.width / referenceCanvas.width,
    viewport.height / referenceCanvas.height
  );
  const expectedBrandWidth = viewport.width * referencePanels.brandWidth / referenceCanvas.width;

  expect(geometry.scale).toBeCloseTo(expectedScale, 5);
  expect(geometry.layout).toEqual({ x: 0, y: 0, width: viewport.width, height: viewport.height });
  expect(geometry.panelsCoverViewport).toBe(true);
  expect(geometry.innerCanvasesFullyVisible).toBe(true);
  expect(geometry.brandPanel.x).toBeCloseTo(0, 1);
  expect(geometry.brandPanel.y).toBeCloseTo(0, 1);
  expect(geometry.brandPanel.width).toBeCloseTo(expectedBrandWidth, 1);
  expect(geometry.brandPanel.height).toBeCloseTo(viewport.height, 1);
  expect(geometry.loginPanel.x).toBeCloseTo(expectedBrandWidth, 1);
  expect(geometry.loginPanel.y).toBeCloseTo(0, 1);
  expect(geometry.loginPanel.width).toBeCloseTo(viewport.width - expectedBrandWidth + (0.5 * expectedScale), 1);
  expect(geometry.loginPanel.height).toBeCloseTo(viewport.height, 1);
  expect(geometry.loginPanelRadius).toBeCloseTo(51 * expectedScale, 1);
  expect(geometry.brandCanvas.width).toBeCloseTo(referencePanels.brandWidth * expectedScale, 2);
  expect(geometry.brandCanvas.height).toBeCloseTo(referenceCanvas.height * expectedScale, 2);
  expect(geometry.loginCanvas.width).toBeCloseTo(referencePanels.loginWidth * expectedScale, 2);
  expect(geometry.loginCanvas.height).toBeCloseTo(referenceCanvas.height * expectedScale, 2);

  for (const [name, expected] of Object.entries(expectedGeometry)) {
    const actual = geometry.normalized[name as keyof typeof geometry.normalized];
    expect(actual, `${name} geometry is missing`).not.toBeNull();
    expect(actual?.x, `${name}.x`).toBeCloseTo(expected.x, 1);
    expect(actual?.y, `${name}.y`).toBeCloseTo(expected.y, 1);
    expect(actual?.width, `${name}.width`).toBeCloseTo(expected.width, 1);
    expect(actual?.height, `${name}.height`).toBeCloseTo(expected.height, 1);
  }
}

async function waitForResponsiveCanvas(page: Page) {
  await page.waitForFunction(({ width, height }) => {
    const canvas = document.querySelector<HTMLElement>('.auth-login-canvas');
    if (!canvas) {
      return false;
    }

    const expectedScale = Math.min(window.innerWidth / width, window.innerHeight / height);
    const actualScale = Number(canvas.dataset.authCanvasScale ?? '0');
    return Math.abs(actualScale - expectedScale) < 0.00001;
  }, referenceCanvas);
}

async function readProjection(page: Page) {
  return page.evaluate(() => {
    const root = document.documentElement;
    const main = document.querySelector<HTMLElement>('main.auth-gate');
    const layout = document.querySelector<HTMLElement>('.auth-login-canvas');
    const brandPanel = document.querySelector<HTMLElement>('.auth-brand-panel');
    const loginPanel = document.querySelector<HTMLElement>('.auth-gate-panel');
    const brandCanvas = document.querySelector<HTMLElement>('.auth-brand-canvas');
    const loginCanvas = document.querySelector<HTMLElement>('.auth-gate-canvas');
    const layoutBounds = layout?.getBoundingClientRect() ?? null;
    const brandPanelBounds = brandPanel?.getBoundingClientRect() ?? null;
    const loginPanelBounds = loginPanel?.getBoundingClientRect() ?? null;
    const brandCanvasBounds = brandCanvas?.getBoundingClientRect() ?? null;
    const loginCanvasBounds = loginCanvas?.getBoundingClientRect() ?? null;
    const scale = Number(layout?.dataset.authCanvasScale ?? '0');
    const round = (value: number) => Math.round(value * 1000) / 1000;
    const roundPrecise = (value: number) => Math.round(value * 1_000_000) / 1_000_000;
    const bounds = (rectangle: DOMRect | null) => rectangle
      ? { x: round(rectangle.x), y: round(rectangle.y), width: round(rectangle.width), height: round(rectangle.height) }
      : { x: 0, y: 0, width: 0, height: 0 };
    const rect = (selector: string, scope: 'brand' | 'login') => {
      const element = document.querySelector<HTMLElement>(selector);
      const origin = scope === 'brand' ? brandCanvasBounds : loginCanvasBounds;
      if (!element || !origin || scale === 0) {
        return null;
      }

      const elementBounds = element.getBoundingClientRect();
      return {
        x: round((elementBounds.x - origin.x) / scale),
        y: round((elementBounds.y - origin.y) / scale),
        width: round(elementBounds.width / scale),
        height: round(elementBounds.height / scale)
      };
    };
    const titleMatches = document.querySelector('h1')?.textContent === 'EMI 프로젝트 통합관리시스템';
    const loginGuidance = document.querySelector<HTMLElement>('.auth-login-guidance');
    const primaryButton = document.querySelector<HTMLButtonElement>('.auth-primary-button');
    const checkbox = document.querySelector<HTMLInputElement>('input[type="checkbox"]');
    const rememberText = checkbox?.nextElementSibling instanceof HTMLElement ? checkbox.nextElementSibling : null;
    const checkboxStyle = checkbox ? getComputedStyle(checkbox) : null;
    const rememberTextStyle = rememberText ? getComputedStyle(rememberText) : null;
    const loadingIndicator = document.querySelector<HTMLElement>('.auth-loading-indicator');
    const loadingIndicatorStyle = loadingIndicator ? getComputedStyle(loadingIndicator, '::before') : null;
    const loginButtonMatches = [...document.querySelectorAll('button')]
      .some((button) => button.textContent === 'LOGIN');
    const loadingIndicatorMatches = Boolean(
      loadingIndicator
      && loadingIndicator.getAttribute('role') === 'status'
      && loadingIndicator.getAttribute('aria-label') === '로그인 확인 중'
    );
    const panelsCoverViewport = Boolean(
      brandPanelBounds
      && loginPanelBounds
      && Math.abs(brandPanelBounds.x) <= 0.1
      && Math.abs(brandPanelBounds.y) <= 0.1
      && Math.abs(brandPanelBounds.right - loginPanelBounds.x) <= 0.1
      && loginPanelBounds.right >= window.innerWidth - 0.1
      && Math.abs(brandPanelBounds.bottom - window.innerHeight) <= 0.1
      && Math.abs(loginPanelBounds.bottom - window.innerHeight) <= 0.1
    );
    const inside = (inner: DOMRect | null, outer: DOMRect | null) => Boolean(
      inner
      && outer
      && inner.x >= outer.x - 0.1
      && inner.y >= outer.y - 0.1
      && inner.right <= outer.right + 0.1
      && inner.bottom <= outer.bottom + 0.1
    );

    return {
      pageLoaded: document.readyState === 'complete',
      expectedStructurePresent: Boolean(
        main
        && layout
        && brandCanvas
        && loginCanvas
        && titleMatches
        && (main?.dataset.authState === 'loading' ? loadingIndicatorMatches : loginButtonMatches)
        && brandPanel
        && loginPanel
      ),
      authState: main?.dataset.authState ?? 'missing',
      authLayout: main?.dataset.authLayout ?? 'missing',
      brandAssetCount: document.querySelectorAll('.auth-brand-logo, .auth-microsoft-brand img').length,
      primaryButtonCount: document.querySelectorAll('.auth-primary-button').length,
      primaryButtonDisabled: primaryButton?.disabled ?? null,
      secondaryButtonCount: document.querySelectorAll('.auth-secondary-button').length,
      checkboxCount: document.querySelectorAll('input[type="checkbox"]').length,
      checkboxDisabled: checkbox?.disabled ?? null,
      rememberChecked: checkbox?.checked ?? null,
      rememberVisualVariant: checkbox ? (checkbox.checked ? 'VARIANT_2' : 'DEFAULT') : 'ABSENT',
      rememberFigmaVariantMatches: checkboxStyle && rememberTextStyle
        ? checkbox.checked
          ? checkboxStyle.backgroundColor === 'rgb(218, 33, 39)'
            && checkboxStyle.borderTopColor === 'rgb(218, 33, 39)'
            && checkboxStyle.backgroundImage !== 'none'
            && checkboxStyle.backgroundPosition === '50% 50%'
            && checkboxStyle.backgroundSize === '13.5px 13.5px'
            && rememberTextStyle.color === 'rgb(40, 40, 40)'
          : checkboxStyle.backgroundColor === 'rgb(255, 255, 255)'
            && checkboxStyle.borderTopColor === 'rgb(115, 115, 115)'
            && checkboxStyle.backgroundImage === 'none'
            && rememberTextStyle.color === 'rgb(115, 115, 115)'
        : null,
      loginGuidanceCount: document.querySelectorAll('.auth-login-guidance').length,
      loginGuidanceTextMatches: loginGuidance?.textContent === '회사 Microsoft 365 계정으로 로그인해 주세요.',
      loadingGuidanceTextMatches: loginGuidance?.textContent === 'Microsoft 365 로그인 정보를 확인하고 있습니다.',
      authPanelBusy: loginPanel?.getAttribute('aria-busy') === 'true',
      loadingIndicatorCount: document.querySelectorAll('.auth-loading-indicator').length,
      loadingIndicatorAnimated: loadingIndicatorStyle
        ? loadingIndicatorStyle.animationName === 'auth-login-loading-spin'
        : false,
      loadingIndicatorRed: loadingIndicatorStyle
        ? loadingIndicatorStyle.borderTopColor === 'rgb(218, 33, 39)'
        : false,
      unexpectedLoginContentCount: document.querySelectorAll(
        '.auth-gate-message:not(.auth-login-guidance), .auth-helper-text, .auth-secondary-button'
      ).length,
      ellipse68PatternCount: document.querySelectorAll('[data-figma-node-id="1:181"]').length,
      viewport: { width: window.innerWidth, height: window.innerHeight },
      geometry: {
        scale: roundPrecise(scale),
        layout: bounds(layoutBounds),
        brandPanel: bounds(brandPanelBounds),
        loginPanel: bounds(loginPanelBounds),
        loginPanelRadius: round(parseFloat(loginPanel ? getComputedStyle(loginPanel).borderTopLeftRadius : '0')),
        brandCanvas: bounds(brandCanvasBounds),
        loginCanvas: bounds(loginCanvasBounds),
        panelsCoverViewport,
        innerCanvasesFullyVisible: inside(brandCanvasBounds, brandPanelBounds) && inside(loginCanvasBounds, loginPanelBounds),
        normalized: {
          title: rect('#auth-gate-title', 'login'),
          emiLogo: rect('.auth-brand-logo', 'brand'),
          microsoftLogo: rect('.auth-microsoft-brand', 'login'),
          loginButton: rect('.auth-primary-button', 'login'),
          guidance: rect('.auth-login-guidance', 'login'),
          remember: rect('.auth-gate-extra', 'login'),
          loadingIndicator: rect('.auth-loading-indicator', 'login'),
          ellipse68: rect('[data-figma-node-id="1:181"]', 'brand')
        }
      },
      horizontalOverflowPixels: Math.max(0, root.scrollWidth - window.innerWidth),
      verticalOverflowPixels: Math.max(0, root.scrollHeight - window.innerHeight),
      blankPage: !main || main.getBoundingClientRect().height === 0,
      targetNotFoundPresent: document.body.textContent?.includes('대상을 찾을 수 없습니다.') ?? false,
      figmaBackgroundContractMatches: Boolean(
        main
        && layout
        && brandPanel
        && getComputedStyle(main).backgroundColor === 'rgb(218, 33, 39)'
        && getComputedStyle(layout).backgroundColor === 'rgb(218, 33, 39)'
        && getComputedStyle(brandPanel).backgroundColor === 'rgb(218, 33, 39)'
        && getComputedStyle(layout, '::before').backgroundColor === 'rgba(255, 255, 255, 0.1)'
      ),
      figmaConnectionContractMatches: Boolean(
        brandPanelBounds
        && loginPanelBounds
        && loginPanel
        && Math.abs(brandPanelBounds.right - loginPanelBounds.x) <= 0.1
        && getComputedStyle(loginPanel).backgroundColor === 'rgb(255, 255, 255)'
        && parseFloat(getComputedStyle(loginPanel).borderTopLeftRadius) > 0
        && getComputedStyle(loginPanel).boxShadow !== 'none'
      )
    };
  });
}
