import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

export type LayoutMode = 'mobile' | 'desktop';

export type AdaptiveLayout = {
  mode: LayoutMode;
  isMobile: boolean;
  touchOptimized: boolean;
};

const mobileQuery = '(max-width: 860px)';
const touchQuery = '(any-pointer: coarse), (pointer: coarse), (hover: none)';

const desktopFallback: AdaptiveLayout = {
  mode: 'desktop',
  isMobile: false,
  touchOptimized: false
};

const AdaptiveLayoutContext = createContext<AdaptiveLayout>(desktopFallback);

function currentSignals(): AdaptiveLayout {
  const isMobile = typeof window !== 'undefined' && window.matchMedia?.(mobileQuery).matches === true;
  const touchOptimized = typeof window !== 'undefined' && window.matchMedia?.(touchQuery).matches === true;

  return {
    mode: isMobile ? 'mobile' : 'desktop',
    isMobile,
    touchOptimized
  };
}

export function AdaptiveLayoutProvider({ children }: { children: ReactNode }) {
  const [layout, setLayout] = useState<AdaptiveLayout>(currentSignals);

  useEffect(() => {
    if (typeof window === 'undefined' || !window.matchMedia) {
      return undefined;
    }

    const mobileMedia = window.matchMedia(mobileQuery);
    const touchMedia = window.matchMedia(touchQuery);
    const update = () => setLayout(currentSignals());

    mobileMedia.addEventListener('change', update);
    touchMedia.addEventListener('change', update);
    update();

    return () => {
      mobileMedia.removeEventListener('change', update);
      touchMedia.removeEventListener('change', update);
    };
  }, []);

  const value = useMemo(() => layout, [layout]);

  return <AdaptiveLayoutContext.Provider value={value}>{children}</AdaptiveLayoutContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAdaptiveLayout() {
  return useContext(AdaptiveLayoutContext);
}
