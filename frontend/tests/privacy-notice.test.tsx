import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PrivacyNoticePage } from '../src/PrivacyNoticePage';

function stubReducedMotion(matches: boolean) {
  vi.stubGlobal('matchMedia', vi.fn(() => ({
    matches,
    media: '(prefers-reduced-motion: reduce)',
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn()
  })));
}

describe('PrivacyNoticePage', () => {
  const scrollIntoView = vi.fn();

  beforeEach(() => {
    window.history.pushState(null, '', '/privacy-notice');
    scrollIntoView.mockClear();
    Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
      configurable: true,
      value: scrollIntoView
    });
    stubReducedMotion(false);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('shows the approved retention policy and current notification channels', () => {
    render(<PrivacyNoticePage onBack={vi.fn()} />);

    expect(screen.getAllByText('사내 규정에 따름')).toHaveLength(3);
    expect(screen.getByText(/Teams Activity와 메일로 전달될 수 있습니다/)).toBeInTheDocument();
    expect(screen.getByText(/PWA 푸시 기기 구독 정보/)).toBeInTheDocument();
    expect(screen.getByText(/암호화 키는 기기 알림 전달에만 사용/)).toBeInTheDocument();
    expect(screen.queryByText(/향후 모바일 푸시 알림/)).not.toBeInTheDocument();
  });

  it('smoothly scrolls to a selected section and exposes the destination', () => {
    render(<PrivacyNoticePage onBack={vi.fn()} />);

    const tab = screen.getByRole('link', { name: '외부 서비스' });
    const destination = screen.getByRole('heading', { name: '외부 서비스와 알림' }).closest('section');
    fireEvent.click(tab);

    expect(scrollIntoView).toHaveBeenCalledWith({ behavior: 'smooth', block: 'start' });
    expect(window.location.hash).toBe('#privacy-services');
    expect(tab).not.toHaveAttribute('aria-current');
    expect(destination).toHaveFocus();
  });

  it('respects reduced-motion preferences while keeping section navigation', () => {
    stubReducedMotion(true);
    render(<PrivacyNoticePage onBack={vi.fn()} />);

    fireEvent.click(screen.getByRole('link', { name: '문의와 권리' }));

    expect(scrollIntoView).toHaveBeenCalledWith({ behavior: 'auto', block: 'start' });
    expect(window.location.hash).toBe('#privacy-rights');
  });
});
