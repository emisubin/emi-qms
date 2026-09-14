/** Bounded decoder inputs: native central pixels retain small printed QR modules;
 * the full frame remains a fallback for a large or off-centre label. */
export function qrScanRegions(videoWidth: number, videoHeight: number) {
  const side = Math.max(1, Math.round(Math.min(videoWidth, videoHeight) * 0.65));
  const cropSize = Math.min(960, side);
  const scale = Math.min(1, 960 / Math.max(videoWidth, videoHeight));
  return [
    {
      x: Math.floor((videoWidth - side) / 2), y: Math.floor((videoHeight - side) / 2),
      sourceWidth: side, sourceHeight: side, width: cropSize, height: cropSize,
    },
    {
      x: 0, y: 0, sourceWidth: videoWidth, sourceHeight: videoHeight,
      width: Math.max(1, Math.round(videoWidth * scale)), height: Math.max(1, Math.round(videoHeight * scale)),
    },
  ];
}
