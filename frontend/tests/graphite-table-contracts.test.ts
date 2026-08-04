/// <reference types="node" />

import { readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import appSource from '../src/App.tsx?raw';

const wireframeSource = readFileSync(path.resolve(process.cwd(), 'src/design-system/wireframe.css'), 'utf8');

function ruleBody(selector: string): string {
  const start = wireframeSource.indexOf(`${selector} {`);
  expect(start, `selector missing: ${selector}`).toBeGreaterThan(-1);
  return wireframeSource.slice(start, wireframeSource.indexOf('}', start));
}

describe('Graphite desktop table density contracts', () => {
  it('keeps input flows from capturing nested sticky table headers', () => {
    const inputFlow = ruleBody('.ds-input-flow');
    expect(inputFlow).toContain('overflow: clip');
    expect(inputFlow).not.toMatch(/^\s*overflow:\s*hidden;/m);
  });

  it('gives the production planning/release grid a 38px ruled header and 48px rows', () => {
    const head = ruleBody('.app-shell .production-project-head');
    expect(head).toContain('min-height: 38px');
    expect(head).toContain('border-bottom: 1px solid var(--wire-ink) !important');

    const row = ruleBody('.app-shell .production-project-row');
    expect(row).toContain('min-height: 48px');
    expect(row).toContain('padding-inline: 12px');
  });

  it('locks My Work and Notifications tables to full-width fixed layout with dense cells', () => {
    const layout = ruleBody('.app-shell .table-wrapper > .workflow-desktop-table,\n.app-shell .table-wrapper > .notification-desktop-table');
    expect(layout).toContain('width: 100%');
    expect(layout).toContain('table-layout: fixed');

    expect(ruleBody(".app-shell :where(.workflow-desktop-table, .notification-desktop-table) th")).toContain('padding: 10px 12px');
    const cells = ruleBody(".app-shell :where(.workflow-desktop-table, .notification-desktop-table) td");
    expect(cells).toContain('padding: 6px 12px');
    expect(cells).toContain('vertical-align: middle');
  });

  it('releases the legacy detail-column min/max for header and data cells and sets the 26% contract', () => {
    const release = ruleBody('.app-shell :where(.workflow-desktop-table, .notification-desktop-table) .workflow-detail-column');
    expect(release).toContain('min-width: 0');
    expect(release).toContain('max-width: none');

    expect(ruleBody('.app-shell :where(.workflow-desktop-table, .notification-desktop-table) th.workflow-detail-column'))
      .toContain('width: 26%');
  });

  it('keeps page-specific column widths for both tables', () => {
    expect(wireframeSource).toContain('.app-shell .workflow-desktop-table th:nth-child(2)');
    expect(wireframeSource).toContain('.app-shell .workflow-desktop-table th:nth-child(5)');
    expect(wireframeSource).toContain('.app-shell .notification-desktop-table th:nth-child(3)');
  });

  it('binds the contract classes to the actual My Work and Notifications tables', () => {
    expect(appSource).toContain('<table className="workflow-desktop-table">');
    expect(appSource).toContain('<table className="notification-desktop-table">');
    expect(appSource).toContain('className="workflow-detail-column"');
  });
});

describe('Graphite no-decorative-left-rail contract', () => {
  it('declares no asymmetric left accent anywhere in the Graphite layer', () => {
    expect(wireframeSource).not.toMatch(/border-left(?:-width)?: (?:[2-9]|\d{2,})px/);
    expect(wireframeSource).not.toMatch(/box-shadow:[^;]*inset \d+px 0/);
  });

  it('keeps the runtime-mode banners on uniform 1px semantic borders', () => {
    expect(ruleBody('.app-shell .review-safe-banner')).not.toContain('border-left');
    expect(ruleBody('.app-shell .review-safe-banner--error')).not.toContain('border-left');
    expect(ruleBody('.app-shell .review-safe-banner--checking')).not.toContain('border-left');
  });

  it('collapses every legacy accent rail to a uniform 1px width', () => {
    for (const selector of [
      '.procurement-error-panel',
      '.project-material-item-summary > span',
      '.quality-inspection-hero',
      '.quality-start-card',
      '.iqc-progress-strip',
      '.iqc-final-report blockquote',
      '.project-selection-tray',
      '.assignee-card',
      '.logistics-priority-strip > p'
    ]) {
      expect(wireframeSource, `rail neutralization missing: ${selector}`).toContain(selector);
    }
    expect(wireframeSource).toContain('.app-shell .sales-kpi-cards article:first-child');
  });
});
