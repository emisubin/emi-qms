/// <reference types="node" />

import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import mainSource from '../src/main.tsx?raw';

const tokensSource = readFileSync(path.resolve(process.cwd(), 'src/design-system/tokens.css'), 'utf8');
const sharedShellSource = readFileSync(path.resolve(process.cwd(), 'src/shared-shell.css'), 'utf8');
const legacyStylesheetPath = path.resolve(process.cwd(), 'src/design-system/wireframe.css');

describe('current design-system stylesheet contract', () => {
  it('does not load or retain the retired global theme', () => {
    expect(mainSource).not.toContain('wireframe.css');
    expect(mainSource).not.toContain('graphite');
    expect(existsSync(legacyStylesheetPath)).toBe(false);
    expect(tokensSource).not.toContain('--wire-');
    expect(sharedShellSource).not.toContain('--wire-');
  });

  it('keeps workflow components on the semantic token layer', () => {
    expect(tokensSource).toContain('.ds-input-flow {');
    expect(tokensSource).toContain('container-type: inline-size');
    expect(tokensSource).toContain('@container (max-width: 620px)');
    expect(tokensSource).toContain('background: var(--ds-color-primary-soft)');
  });

  it('keeps shared product identity and company actions in the shell layer', () => {
    expect(sharedShellSource).toContain('.company-information-footer {');
    expect(sharedShellSource).toContain('.company-information-footer__privacy-link');
    expect(sharedShellSource).toContain('.app-shell .app-brand-home-button');
    expect(sharedShellSource).toContain('min-width:44px!important');
  });
});
