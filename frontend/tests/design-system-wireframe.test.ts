/// <reference types="node" />

import { readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import mainSource from '../src/main.tsx?raw';

const stylesSource = readFileSync(path.resolve(process.cwd(), 'src/styles.css'), 'utf8');
const wireframeSource = readFileSync(path.resolve(process.cwd(), 'src/design-system/wireframe.css'), 'utf8');

describe('DESIGN-000 black and white wireframe contract', () => {
  it('loads the wireframe layer after the design tokens', () => {
    expect(mainSource.indexOf("import './design-system/tokens.css'"))
      .toBeLessThan(mainSource.indexOf("import './design-system/wireframe.css'"));
  });

  it('keeps input-flow headers readable inside narrow action panels', () => {
    expect(wireframeSource).toContain('container-type: inline-size');
    expect(wireframeSource).toContain('@container (max-width: 620px)');
    expect(stylesSource).toContain('.settlement-form > .ds-input-flow { grid-column: 1 / -1; }');
    expect(stylesSource).toContain('.logistics-action-panel .ds-input-flow__header');
  });

  it('pairs dark wireframe surfaces with an explicit light foreground', () => {
    expect(wireframeSource).toContain(".logistics-stage-switch button[aria-current='step']");
    expect(wireframeSource).toContain('.production-control-template-step > b');
    expect(wireframeSource).toContain('background-color: var(--wire-black) !important;');
    expect(wireframeSource).toContain('color: #ffffff !important;');
  });
});
