import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { OsanProjectExcelDialog } from '../src/OsanProjectExcelDialog';
import * as api from '../src/osanProjectExcel';
import { ApiError } from '../src/api';

vi.mock('../src/osanProjectExcel', () => ({ downloadOsanProjectTemplate: vi.fn(), previewOsanProjectExcel: vi.fn(), applyOsanProjectExcel: vi.fn() }));
const preview: api.OsanProjectExcelPreview = {
  supportsRowEditing: true,
  fileSha256: 'file-hash', totalRowCount: 1, totalQuantity: 2, errorCount: 0, errors: [],
  rows: [{ rowNumber: 2, title: '프로젝트', projectCode: 'AbC  001', customerName: '고객사', poNumber: '001-PO', workOrderNumber: null, deliveryDate: '2026-12-31', productName: '제품', quantity: 2, errors: [] }]
};
const file = () => new File(['synthetic workbook'], 'projects.xlsx');
function selectFile(value = file()) { fireEvent.change(screen.getByLabelText('작성한 엑셀 파일'), { target: { files: [value] } }); }
async function showPreview() { selectFile(); fireEvent.click(screen.getByRole('button', { name: '내용 미리보기' })); await screen.findByRole('button', { name: '1개 프로젝트 등록' }); }
beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(api.previewOsanProjectExcel).mockResolvedValue(preview);
  vi.mocked(api.applyOsanProjectExcel).mockResolvedValue({ operationId: 'op', replayed: false, createdCount: 1, projectIds: ['project'], createdRowNumbers: [2] });
});

describe('오산 프로젝트 엑셀 업로드', () => {
  it('keeps text identifiers, previews before apply, and reports the created count', async () => {
    const applied = vi.fn();
    render(<OsanProjectExcelDialog developmentUserKey="dev-user" onClose={vi.fn()} onApplied={applied} />);
    expect(screen.queryByRole('button', { name: /프로젝트 등록/ })).not.toBeInTheDocument();
    await showPreview();
    expect(screen.getAllByRole('columnheader').map(header => header.textContent))
      .toEqual(['행', '장비명', '프로젝트 코드', 'part 분류', '수량', '고객사', 'PO No', 'W/O No', '납기일', '확인 결과']);
    expect(screen.getByLabelText('2행 프로젝트 코드')).toHaveTextContent('AbC 001');
    expect(screen.getByLabelText('2행 PO No')).toHaveTextContent('001-PO');
    fireEvent.click(screen.getByRole('button', { name: '1개 프로젝트 등록' }));
    await waitFor(() => expect(applied).toHaveBeenCalledWith(1));
    expect(api.applyOsanProjectExcel).toHaveBeenCalledWith('dev-user', expect.any(File), 'file-hash', expect.stringMatching(/^[0-9a-f-]{36}$/), expect.any(AbortSignal), preview.rows, []);
  });

  it('blocks rows with validation errors and resets preview on reselection', async () => {
    vi.mocked(api.previewOsanProjectExcel).mockResolvedValue({ ...preview, errorCount: 1, rows: [{ ...preview.rows[0], title: '', errors: ['장비명을 입력해 주세요.'] }] });
    render(<OsanProjectExcelDialog developmentUserKey="dev-user" onClose={vi.fn()} onApplied={vi.fn()} />);
    selectFile(); fireEvent.click(screen.getByRole('button', { name: '내용 미리보기' }));
    expect(await screen.findByRole('button', { name: '0개 프로젝트 등록' })).toBeDisabled();
    selectFile(new File(['new file'], 'new.xlsx'));
    expect(screen.queryByRole('button', { name: '1개 프로젝트 등록' })).not.toBeInTheDocument();
    expect(api.applyOsanProjectExcel).not.toHaveBeenCalled();
  });

  it('rejects wrong extension, empty and oversized files before calling the server', () => {
    render(<OsanProjectExcelDialog developmentUserKey="dev-user" onClose={vi.fn()} onApplied={vi.fn()} />);
    for (const invalid of [new File(['x'], 'projects.xlsm'), new File([], 'empty.xlsx'), new File([new Uint8Array(5 * 1024 * 1024 + 1)], 'large.xlsx')]) {
      selectFile(invalid);
      expect(screen.getByRole('alert')).toHaveTextContent('5MiB 이하');
      expect(screen.getByRole('button', { name: '내용 미리보기' })).toBeDisabled();
    }
    expect(api.previewOsanProjectExcel).not.toHaveBeenCalled();
  });

  it('prevents duplicate submits and keeps the same operation ID when retrying an uncertain result', async () => {
    let rejectApply!: (error: unknown) => void;
    vi.mocked(api.applyOsanProjectExcel).mockImplementationOnce(() => new Promise((_resolve, reject) => { rejectApply = reject; }));
    const close = vi.fn();
    render(<OsanProjectExcelDialog developmentUserKey="dev-user" onClose={close} onApplied={vi.fn()} />);
    await showPreview();
    const button = screen.getByRole('button', { name: '1개 프로젝트 등록' });
    fireEvent.click(button); fireEvent.click(button);
    await waitFor(() => expect(api.applyOsanProjectExcel).toHaveBeenCalledTimes(1));
    expect(screen.getByLabelText('작성한 엑셀 파일')).toBeDisabled();
    expect(screen.getByRole('button', { name: '닫기' })).toBeDisabled();
    fireEvent.keyDown(screen.getByRole('heading', { name: '프로젝트 엑셀 업로드' }), { key: 'Escape' });
    expect(close).not.toHaveBeenCalled();
    await act(async () => rejectApply(new ApiError(0, '연결을 확인해 주세요.')));
    expect(await screen.findByRole('alert')).toHaveTextContent('등록 결과를 확인하지 못했습니다');
    expect(screen.getByLabelText('2행 장비명')).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: '같은 요청으로 결과 확인' }));
    await waitFor(() => expect(api.applyOsanProjectExcel).toHaveBeenCalledTimes(2));
    const calls = vi.mocked(api.applyOsanProjectExcel).mock.calls;
    expect(calls[1][3]).toBe(calls[0][3]);
    expect(calls[1][5]).toEqual(calls[0][5]);
  });

  it('registers valid rows, keeps the missing row editable, and never resubmits saved rows', async () => {
    const missing = { ...preview.rows[0], rowNumber: 3, title: '', projectCode: 'SECOND', errors: ['장비명 필요'] };
    vi.mocked(api.previewOsanProjectExcel).mockImplementation(async (_key, _file, _signal, rows) => ({
      ...preview, rows: rows ? rows.map(row => ({ ...row, errors: row.title ? [] : ['장비명 필요'] })) : [preview.rows[0], missing], totalRowCount: rows?.length ?? 2
    }));
    vi.mocked(api.applyOsanProjectExcel).mockImplementation(async (_key, _file, _hash, operationId, _signal, rows) => ({ operationId, replayed: false, createdCount: rows!.length, projectIds: ['id'], createdRowNumbers: rows!.map(row => row.rowNumber) }));
    const applied = vi.fn();
    render(<OsanProjectExcelDialog developmentUserKey="dev-user" onClose={vi.fn()} onApplied={applied} />);
    await showPreview(); fireEvent.click(screen.getByRole('button', { name: '1개 프로젝트 등록' }));
    await waitFor(() => expect(applied).toHaveBeenCalledWith(1));
    expect(screen.getByLabelText('2행 장비명')).toBeDisabled();
    expect(screen.getByLabelText('3행 장비명')).toBeEnabled();
    fireEvent.click(screen.getByLabelText('3행 장비명'));
    fireEvent.change(screen.getByLabelText('3행 장비명'), { target: { value: '추가 프로젝트' } });
    fireEvent.click(screen.getByRole('button', { name: '1개 프로젝트 등록' }));
    await waitFor(() => expect(applied).toHaveBeenCalledTimes(2));
    const calls = vi.mocked(api.applyOsanProjectExcel).mock.calls;
    expect(calls[0][5]?.map(row => row.rowNumber)).toEqual([2]);
    expect(calls[1][5]?.map(row => row.rowNumber)).toEqual([3]);
    expect(calls[1][5]?.[0].title).toBe('추가 프로젝트');
    expect(calls[1][3]).not.toBe(calls[0][3]);
  });

  it.each(['identical', 'code'] as const)('requires explicit confirmation for %s duplicates and invalidates it after editing', async duplicateKind => {
    vi.mocked(api.previewOsanProjectExcel).mockResolvedValue({ ...preview, rows: [{ ...preview.rows[0], duplicateKind }] });
    render(<OsanProjectExcelDialog developmentUserKey="dev-user" onClose={vi.fn()} onApplied={vi.fn()} />);
    await showPreview(); fireEvent.click(screen.getByRole('button', { name: '1개 프로젝트 등록' }));
    await screen.findByRole('region', { name: '중복 프로젝트 확인' });
    expect(api.applyOsanProjectExcel).not.toHaveBeenCalled();
    fireEvent.click(screen.getByLabelText('2행 장비명'));
    fireEvent.change(screen.getByLabelText('2행 장비명'), { target: { value: '수정' } });
    expect(screen.queryByRole('region', { name: '중복 프로젝트 확인' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '1개 프로젝트 등록' }));
    fireEvent.click(await screen.findByRole('button', { name: '중복 포함 1개 등록' }));
    await waitFor(() => expect(api.applyOsanProjectExcel).toHaveBeenCalledTimes(1));
    expect(vi.mocked(api.applyOsanProjectExcel).mock.calls[0][6]).toEqual([2]);
  });

  it('keeps all rows when file-level validation rejects the request', async () => {
    vi.mocked(api.previewOsanProjectExcel).mockResolvedValue({ ...preview, errors: ['파일 오류'] });
    render(<OsanProjectExcelDialog developmentUserKey="dev-user" onClose={vi.fn()} onApplied={vi.fn()} />);
    await showPreview(); fireEvent.click(screen.getByRole('button', { name: '1개 프로젝트 등록' }));
    await waitFor(() => expect(screen.getByText('파일 오류를 확인해 주세요.')).toBeInTheDocument());
    expect(api.applyOsanProjectExcel).not.toHaveBeenCalled();
    expect(screen.getByLabelText('2행 장비명')).toBeEnabled();
  });

  it('blocks old servers that would ignore edited rows', async () => {
    vi.mocked(api.previewOsanProjectExcel).mockResolvedValue({ ...preview, supportsRowEditing: undefined });
    render(<OsanProjectExcelDialog developmentUserKey="dev-user" onClose={vi.fn()} onApplied={vi.fn()} />);
    await showPreview();
    expect(screen.getByRole('button', { name: '1개 프로젝트 등록' })).toBeDisabled();
    expect(screen.getByRole('alert')).toHaveTextContent('서버 갱신');
    expect(api.applyOsanProjectExcel).not.toHaveBeenCalled();
  });

  it('keeps the unresolved request locked when replay is forbidden after an uncertain commit', async () => {
    vi.mocked(api.applyOsanProjectExcel).mockRejectedValueOnce(new ApiError(0, '응답 유실')).mockRejectedValueOnce(new ApiError(403, '권한 없음'));
    render(<OsanProjectExcelDialog developmentUserKey="dev-user" onClose={vi.fn()} onApplied={vi.fn()} />);
    await showPreview(); fireEvent.click(screen.getByRole('button', { name: '1개 프로젝트 등록' }));
    fireEvent.click(await screen.findByRole('button', { name: '같은 요청으로 결과 확인' }));
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('조회 권한'));
    expect(screen.getByLabelText('2행 장비명')).toBeDisabled();
    expect(screen.getByRole('button', { name: '닫기' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: '같은 요청으로 결과 확인' }));
    await waitFor(() => expect(api.applyOsanProjectExcel).toHaveBeenCalledTimes(3));
    const calls = vi.mocked(api.applyOsanProjectExcel).mock.calls;
    expect(calls[2][3]).toBe(calls[0][3]);
    expect(calls[2][5]).toEqual(calls[0][5]);
  });

  it('ignores a preview arriving after the dialog is unmounted', async () => {
    let resolvePreview!: (value: api.OsanProjectExcelPreview) => void;
    vi.mocked(api.previewOsanProjectExcel).mockImplementationOnce(() => new Promise(resolve => { resolvePreview = resolve; }));
    const applied = vi.fn();
    const view = render(<OsanProjectExcelDialog developmentUserKey="dev-user" onClose={vi.fn()} onApplied={applied} />);
    selectFile(); fireEvent.click(screen.getByRole('button', { name: '내용 미리보기' }));
    const signal = vi.mocked(api.previewOsanProjectExcel).mock.calls[0][2]!;
    view.unmount(); expect(signal.aborted).toBe(true);
    await act(async () => resolvePreview(preview));
    expect(applied).not.toHaveBeenCalled();
  });
});
