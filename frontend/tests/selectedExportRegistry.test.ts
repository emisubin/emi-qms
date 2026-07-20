import { describe, expect, it } from 'vitest';
import { selectedExportPageRegistry } from '../src/selectedExportRegistry';

describe('selected export page registry', () => {
  it('covers the 12 business and 9 admin list pages without duplicate routes or screens', () => {
    expect(selectedExportPageRegistry).toHaveLength(21);
    expect(selectedExportPageRegistry.filter((page) => page.area === 'business')).toHaveLength(12);
    expect(selectedExportPageRegistry.filter((page) => page.area === 'admin')).toHaveLength(9);
    expect(new Set(selectedExportPageRegistry.map((page) => page.route)).size).toBe(21);
    expect(new Set(selectedExportPageRegistry.map((page) => page.screen)).size).toBe(21);
  });

  it('uses a stable row identity on every page', () => {
    expect(selectedExportPageRegistry.every((page) => page.selectionKey.endsWith('Id'))).toBe(true);
  });
});
