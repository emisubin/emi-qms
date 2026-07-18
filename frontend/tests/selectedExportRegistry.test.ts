import { describe, expect, it } from 'vitest';
import { selectedExportPageRegistry } from '../src/selectedExportRegistry';

describe('selected export page registry', () => {
  it('covers the 12 business and 8 admin list pages without duplicate routes or screens', () => {
    expect(selectedExportPageRegistry).toHaveLength(20);
    expect(selectedExportPageRegistry.filter((page) => page.area === 'business')).toHaveLength(12);
    expect(selectedExportPageRegistry.filter((page) => page.area === 'admin')).toHaveLength(8);
    expect(new Set(selectedExportPageRegistry.map((page) => page.route)).size).toBe(20);
    expect(new Set(selectedExportPageRegistry.map((page) => page.screen)).size).toBe(20);
  });

  it('uses a stable row identity on every page', () => {
    expect(selectedExportPageRegistry.every((page) => page.selectionKey.endsWith('Id'))).toBe(true);
  });
});
