import { expect, it } from 'vitest';
import decode from 'jsqr';
import fixture from './fixtures/osan-panel-qr.json';
import { parseOsanPanelQr } from '../src/osanQrScan';

// Independent QR encoder fixture, synthetic identifiers; exercise the shipped decoder.
it('decodes a printed panel QR without a browser-native BarcodeDetector', () => {
  const scale = 5, quiet = 4, size = (fixture.rows.length + quiet * 2) * scale;
  const pixels = new Uint8ClampedArray(size * size * 4).fill(255);
  fixture.rows.forEach((row, y) => [...row].forEach((value, x) => {
    if (value !== '1') return;
    for (let dy = 0; dy < scale; dy++) for (let dx = 0; dx < scale; dx++) {
      const offset = (((y + quiet) * scale + dy) * size + (x + quiet) * scale + dx) * 4;
      pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = 0;
    }
  }));
  const result = decode(pixels, size, size, { inversionAttempts: 'dontInvert' });
  expect(result?.data).toBe(fixture.text);
  expect(parseOsanPanelQr(result!.data)).toEqual({ projectId: '11111111-1111-1111-1111-111111111111', targetId: '22222222-2222-2222-2222-222222222222' });
});
