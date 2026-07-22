import { describe, expect, it } from 'vitest';
import mainSource from '../src/main.tsx?raw';

describe('DESIGN-000 black and white wireframe contract', () => {
  it('loads the wireframe layer after the design tokens', () => {
    expect(mainSource.indexOf("import './design-system/tokens.css'"))
      .toBeLessThan(mainSource.indexOf("import './design-system/wireframe.css'"));
  });
});
