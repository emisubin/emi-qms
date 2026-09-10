export const qrLabelCss = `.osan-qr-label{box-sizing:border-box;width:var(--label-size);height:var(--label-size);padding:1mm;display:flex;flex-direction:column;align-items:center;justify-content:center;flex-shrink:0;background:#fff;color:#000;break-inside:avoid;page-break-inside:avoid;font-family:Arial,'Malgun Gothic',sans-serif;outline:1px solid #ddd}.osan-qr-label img{display:block;width:var(--qr-size);height:var(--qr-size);flex:0 0 var(--qr-size);object-fit:contain;filter:none}.osan-qr-label-caption{width:100%;text-align:center;line-height:var(--caption-line);font-size:var(--caption-size);height:calc(var(--caption-line) * 3)}.osan-qr-label-caption p{padding:0;margin:0;height:var(--caption-line);white-space:nowrap;line-height:var(--caption-line)}.osan-qr-label-caption p:first-child{font-weight:700}@media print{.osan-qr-label{outline:none}}`;

export function populateQrPrintDocument(doc: Document, labels: Element[], size: 30 | 50) {
  const style = doc.createElement('style');
  style.textContent = qrLabelCss + '@page{size:A4;margin:10mm}body{margin:0}.sheet{display:grid;grid-template-columns:repeat(' + (size === 30 ? 5 : 3) + ',' + size + 'mm);gap:3mm;align-content:start;break-after:page;page-break-after:always}.sheet:last-child{break-after:auto;page-break-after:auto}';
  doc.head.append(style);
  const capacity = size === 30 ? 40 : 15;
  for (let start = 0; start < labels.length; start += capacity) {
    const sheet = doc.createElement('div'); sheet.className = 'sheet';
    labels.slice(start, start + capacity).forEach(label => sheet.append(label.cloneNode(true)));
    doc.body.append(sheet);
  }
}
