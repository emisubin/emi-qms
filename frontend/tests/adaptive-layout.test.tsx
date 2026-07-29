import { act, fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AdaptiveLayoutProvider, useAdaptiveLayout } from '../src/adaptive-layout';

const mobileQuery = '(max-width: 860px)';
const touchQuery = '(any-pointer: coarse), (pointer: coarse), (hover: none)';

afterEach(() => {
  vi.restoreAllMocks();
});

describe('AdaptiveLayoutProvider', () => {
  it('uses viewport for composition and capability signals for touch optimization', () => {
    const media = installMatchMedia({ [mobileQuery]: false, [touchQuery]: true });

    render(<AdaptiveLayoutProvider><LayoutProbe /></AdaptiveLayoutProvider>);

    expect(screen.getByTestId('layout-mode')).toHaveTextContent('desktop');
    expect(screen.getByTestId('touch-mode')).toHaveTextContent('touch');

    act(() => media.set(mobileQuery, true));
    expect(screen.getByTestId('layout-mode')).toHaveTextContent('mobile');

    act(() => media.set(touchQuery, false));
    expect(screen.getByTestId('touch-mode')).toHaveTextContent('pointer');
  });

  it('preserves stateful child input while the composition mode changes', () => {
    const media = installMatchMedia({ [mobileQuery]: false, [touchQuery]: false });

    render(<AdaptiveLayoutProvider><StatefulProbe /></AdaptiveLayoutProvider>);
    fireEvent.change(screen.getByRole('textbox', { name: '작업 메모' }), { target: { value: '입력 유지' } });

    act(() => media.set(mobileQuery, true));

    expect(screen.getByRole('textbox', { name: '작업 메모' })).toHaveValue('입력 유지');
    expect(screen.getByTestId('layout-mode')).toHaveTextContent('mobile');
  });
});

function LayoutProbe() {
  const layout = useAdaptiveLayout();
  return (
    <>
      <span data-testid="layout-mode">{layout.mode}</span>
      <span data-testid="touch-mode">{layout.touchOptimized ? 'touch' : 'pointer'}</span>
    </>
  );
}
function StatefulProbe() {
  const [value, setValue] = useState('');
  return (
    <>
      <LayoutProbe />
      <label>작업 메모<input value={value} onChange={(event) => setValue(event.target.value)} /></label>
    </>
  );
}

function installMatchMedia(initial: Record<string, boolean>) {
  const states = new Map(Object.entries(initial));
  const listeners = new Map<string, Set<(event: MediaQueryListEvent) => void>>();

  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: vi.fn((query: string) => ({
      get matches() { return states.get(query) ?? false; },
      media: query,
      onchange: null,
      addEventListener: (_type: string, listener: (event: MediaQueryListEvent) => void) => {
        const queryListeners = listeners.get(query) ?? new Set();
        queryListeners.add(listener);
        listeners.set(query, queryListeners);
      },
      removeEventListener: (_type: string, listener: (event: MediaQueryListEvent) => void) => listeners.get(query)?.delete(listener),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn()
    }))
  });

  return {
    set(query: string, matches: boolean) {
      states.set(query, matches);
      listeners.get(query)?.forEach((listener) => listener({ matches, media: query } as MediaQueryListEvent));
    }
  };
}
