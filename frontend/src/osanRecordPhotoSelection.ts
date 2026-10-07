import { validateOsanPhotos } from './osanProgress';

/** Capture the selection before clearing it so the same file can be selected again. */
export function selectOsanRecordPhotos(input: HTMLInputElement, current: readonly File[], append = false) {
  const selected = Array.from(input.files ?? []);
  input.value = '';
  const files = [...(append ? current : []), ...selected];
  return { files, error: validateOsanPhotos(files) };
}
