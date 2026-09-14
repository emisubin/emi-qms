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

import { qrScanRegions } from '../src/osanQrFrame';
// Independent encoder fixture in a camera-like frame. This tests lost sampling
// detail, not physical iPhone focus or a captured camera image.
it('preserves small label modules lost by full-frame downscaling', () => {
  const width = 1920, height = 1080, scale = 2;
  const pixels = new Uint8ClampedArray(width * height * 4).fill(255);
  const left = Math.floor((width - fixture.rows.length * scale) / 2);
  const top = Math.floor((height - fixture.rows.length * scale) / 2);
  fixture.rows.forEach((row, y) => [...row].forEach((value, x) => {
    if (value !== '1') return;
    for (let dy = 0; dy < scale; dy++) for (let dx = 0; dx < scale; dx++) {
      const offset = ((top + y * scale + dy) * width + left + x * scale + dx) * 4;
      pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = 0;
    }
  }));
  // Area averaging models the old canvas 1920 -> 960 reduction.
  const old = new Uint8ClampedArray(960 * 540 * 4);
  for (let y = 0; y < 540; y++) for (let x = 0; x < 960; x++) for (let c = 0; c < 4; c++) {
    const base = (y * 2 * width + x * 2) * 4 + c;
    old[(y * 960 + x) * 4 + c] = (pixels[base] + pixels[base + 4] + pixels[base + width * 4] + pixels[base + width * 4 + 4]) / 4;
  }
  expect(decode(old, 960, 540)).toBeNull();
  const region = qrScanRegions(width, height)[0];
  expect(region.width).toBe(region.sourceWidth);
  const crop = new Uint8ClampedArray(region.width * region.height * 4);
  for (let y = 0; y < region.height; y++) {
    const start = ((y + region.y) * width + region.x) * 4;
    crop.set(pixels.subarray(start, start + region.width * 4), y * region.width * 4);
  }
  expect(decode(crop, region.width, region.height)?.data).toBe(fixture.text);
});
it('bounds portrait and landscape decoding and retains full-frame coverage', () => {
  for (const [width, height] of [[1920, 1080], [1080, 1920], [3840, 2160], [640, 480]]) {
    const regions = qrScanRegions(width, height);
    for (const r of regions) {
      expect(Math.max(r.width, r.height)).toBeLessThanOrEqual(1920);
      expect(r.x).toBeGreaterThanOrEqual(0); expect(r.y).toBeGreaterThanOrEqual(0);
      expect(r.x + r.sourceWidth).toBeLessThanOrEqual(width);
      expect(r.y + r.sourceHeight).toBeLessThanOrEqual(height);
    }
    expect(regions[1]).toMatchObject({ x: 0, y: 0, sourceWidth: width, sourceHeight: height });
  }
});

it('reads a small QR outside the centre guide through a native fallback tile', () => {
  const width = 1920, height = 1080, left = 499, top = 189;
  const pixels = new Uint8ClampedArray(width * height * 4).fill(255);
  fixture.rows.forEach((row, y) => [...row].forEach((v, x) => {
    if (v !== '1') return;
    for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) {
      const offset = ((top + y * 2 + dy) * width + left + x * 2 + dx) * 4;
      pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = 0;
    }
  }));
  const region = qrScanRegions(width, height).slice(2).find(r => r.width === r.sourceWidth && r.x <= left && r.y <= top && r.x + r.width >= left + fixture.rows.length * 2 && r.y + r.height >= top + fixture.rows.length * 2)!;
  expect(region).toBeDefined();
  const crop = new Uint8ClampedArray(region.width * region.height * 4);
  for (let y = 0; y < region.height; y++) {
    const start = ((y + region.y) * width + region.x) * 4;
    crop.set(pixels.subarray(start, start + region.width * 4), y * region.width * 4);
  }
  expect(decode(crop, region.width, region.height)?.data).toBe(fixture.text);
});
