/** Bounded decoder inputs: native central pixels retain small printed QR modules;
 * the full frame remains a fallback for a large or off-centre label. */
export function qrScanRegions(videoWidth: number, videoHeight: number) {
  const side = Math.max(1, Math.round(Math.min(videoWidth, videoHeight) * 0.65));
  const cropSize = Math.min(960, side);
  const scale = Math.min(1, 960 / Math.max(videoWidth, videoHeight));
  const regions = [
    {
      x: Math.floor((videoWidth - side) / 2), y: Math.floor((videoHeight - side) / 2),
      sourceWidth: side, sourceHeight: side, width: cropSize, height: cropSize,
    },
    {
      x: 0, y: 0, sourceWidth: videoWidth, sourceHeight: videoHeight,
      width: Math.max(1, Math.round(videoWidth * scale)), height: Math.max(1, Math.round(videoHeight * scale)),
    },
  ];
  // Nearest-neighbour enlargement gives the decoder more samples per module.
  // It supplements native sampling; it does not replace it with a blurred image.
  if (side <= 960) regions.push({ ...regions[0], width: cropSize * 2, height: cropSize * 2 });
  const tile = Math.min(960, Math.min(videoWidth, videoHeight));
  const insetX = Math.floor((videoWidth - Math.min(videoWidth, videoHeight)) / 2);
  const insetY = Math.floor((videoHeight - Math.min(videoWidth, videoHeight)) / 2);
  for (const x of [insetX, videoWidth - insetX - tile]) for (const y of [insetY, videoHeight - insetY - tile]) {
    if (!regions.some(r => r.x === x && r.y === y && r.sourceWidth === tile && r.width === tile))
      regions.push({ x, y, sourceWidth: tile, sourceHeight: tile, width: tile, height: tile });
  }
  return regions;
}
