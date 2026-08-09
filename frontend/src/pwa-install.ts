import { createContext, useContext } from 'react';

export type PwaInstallExperienceValue = {
  available: boolean;
  entryLabel: string;
  openGuide: () => void;
};

export const PwaInstallExperienceContext = createContext<PwaInstallExperienceValue>({
  available: false,
  entryLabel: 'EMI PMS 설치 안내',
  openGuide: () => undefined
});

export function usePwaInstallExperience() {
  return useContext(PwaInstallExperienceContext);
}
