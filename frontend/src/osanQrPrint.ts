export const qrLabelCss = `.osan-qr-label{box-sizing:border-box;width:var(--label-size);height:var(--label-size);padding:1mm;display:flex;flex-direction:column;align-items:center;justify-content:center;flex-shrink:0;background:#fff;color:#000;break-inside:avoid;page-break-inside:avoid;font-family:Arial,'Malgun Gothic',sans-serif;outline:1px solid #ddd}.osan-qr-label img{display:block;width:var(--qr-size);height:var(--qr-size);flex:0 0 var(--qr-size);object-fit:contain;filter:none}.osan-qr-label-caption{width:100%;text-align:center;line-height:var(--caption-line);font-size:var(--caption-size);height:calc(var(--caption-line) * 3)}.osan-qr-label-caption p{padding:0;margin:0;height:var(--caption-line);white-space:nowrap;line-height:var(--caption-line)}.osan-qr-label-caption p:first-child{font-weight:700}@media print{.osan-qr-label{outline:none}}`;

export function populateQrPrintDocument(doc: Document, labels: Element[], size: 30 | 50) {
  const style = doc.createElement('style');
  style.textContent = qrLabelCss + '@page{size:' + size + 'mm ' + size + 'mm;margin:0}html,body{margin:0;padding:0}.sheet{width:' + size + 'mm;height:' + size + 'mm;overflow:hidden;break-after:page;page-break-after:always;break-inside:avoid;page-break-inside:avoid}.sheet:last-child{break-after:auto;page-break-after:auto}';
  doc.head.append(style);
  labels.forEach(label => {
    const sheet = doc.createElement('div'); sheet.className = 'sheet';
    sheet.append(label.cloneNode(true)); doc.body.append(sheet);
  });
}
