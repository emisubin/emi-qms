import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { resetBusinessUnitRequestContext, selectBusinessUnit, setRuntimeMutationAllowed } from '../src/api';
import { applyOsanProjectExcel, downloadOsanProjectTemplate, previewOsanProjectExcel } from '../src/osanProjectExcel';

beforeEach(() => { resetBusinessUnitRequestContext(true); selectBusinessUnit('OSAN'); setRuntimeMutationAllowed(true); });
afterEach(() => { vi.restoreAllMocks(); resetBusinessUnitRequestContext(true); setRuntimeMutationAllowed(false); });
describe('오산 엑셀 API 전송', () => {
  it('keeps original workbook, preview hash, retry ID and business context on the actual shared API client', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async () => new Response('{}', { headers: { 'Content-Type': 'application/json' } }));
    const file = new File([new Uint8Array([80, 75, 3, 4])], '오산 프로젝트.xlsx');
    await previewOsanProjectExcel('dev-user', file);
    const rows = [{ rowNumber: 4, title: 'edited', projectCode: '0001', customerName: 'Customer', poNumber: null, workOrderNumber: null, deliveryDate: '2026-12-31', productName: 'Product', quantity: 2, errors: [] }];
    await applyOsanProjectExcel('dev-user', file, 'verified-hash', 'same-operation', undefined, rows, [4]);
    for (const [, options] of fetchMock.mock.calls) {
      const headers = new Headers(options?.headers);
      expect(headers.get('X-Qms-Business-Unit')).toBe('OSAN');
      expect(headers.get('X-Dev-User')).toBe('dev-user');
      expect(headers.has('Content-Type')).toBe(false);
      const body = options?.body as FormData;
      expect((body.get('file') as File).name).toBe(file.name);
      expect((body.get('file') as File).size).toBe(4);
    }
    expect(String(fetchMock.mock.calls[0][0])).toContain('/api/osan/projects/import/preview');
    expect(String(fetchMock.mock.calls[1][0])).toContain('/api/osan/projects/import/apply');
    const applied = fetchMock.mock.calls[1][1]?.body as FormData;
    expect(applied.get('expectedFileSha256')).toBe('verified-hash');
    expect(applied.get('operationId')).toBe('same-operation');
    expect(JSON.parse(applied.get('rows') as string)).toEqual(rows);
    expect(JSON.parse(applied.get('confirmedDuplicateRowNumbers') as string)).toEqual([4]);
  });
  it('blocks registration in read-only mode while template download remains a read request', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(new Uint8Array([80, 75]), { status: 200 }));
    setRuntimeMutationAllowed(false);
    await expect(applyOsanProjectExcel('dev-user', new File(['x'], 'a.xlsx'), 'hash', 'op')).rejects.toMatchObject({ status: 423 });
    expect(fetchMock).not.toHaveBeenCalled();
    expect((await downloadOsanProjectTemplate('dev-user')).size).toBe(2);
    expect(String(fetchMock.mock.calls[0][0])).toContain('/api/osan/projects/import/template');
  });
});
