import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { OsanProjectExcelDialog } from '../src/OsanProjectExcelDialog';
import * as api from '../src/osanProjectExcel';
import { ApiError } from '../src/api';

vi.mock('../src/osanProjectExcel', () => ({ downloadOsanProjectTemplate: vi.fn(), previewOsanProjectExcel: vi.fn(), applyOsanProjectExcel: vi.fn() }));
const preview: api.OsanProjectExcelPreview = {
  fileSha256: 'file-hash', totalRowCount: 1, totalQuantity: 2, errorCount: 0, errors: [],
  rows: [{ rowNumber: 2, title: '프로젝트', projectCode: 'AbC  001', customerName: '거래처', poNumber: '001-PO', workOrderNumber: null, deliveryDate: '2026-12-31', productName: '제품', quantity: 2, errors: [] }]
};
const file = () => new File(['synthetic workbook'], 'projects.xlsx');
function selectFile(value = file()) { fireEvent.change(screen.getByLabelText('작성한 엑셀 파일'), { target: { files: [value] } }); }
async function showPreview() { selectFile(); fireEvent.click(screen.getByRole('button', { name: '내용 미리보기' })); await screen.findByRole('button', { name: '1개 프로젝트 등록' }); }
beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(api.previewOsanProjectExcel).mockResolvedValue(preview);
  vi.mocked(api.applyOsanProjectExcel).mockResolvedValue({ operationId: 'op', replayed: false, createdCount: 1, projectIds: ['project'] });
});

describe('오산 프로젝트 엑셀 업로드', () => {
  it('keeps text identifiers, previews before apply, and reports the created count', async () => {
    const applied = vi.fn();
    render(<OsanProjectExcelDialog developmentUserKey="dev-user" onClose={vi.fn()} onApplied={applied} />);
    expect(screen.queryByRole('button', { name: /프로젝트 등록/ })).not.toBeInTheDocument();
    await showPreview();
    expect(screen.getByText('AbC 001')).toHaveTextContent('AbC 001');
    expect(screen.getByText('001-PO')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '1개 프로젝트 등록' }));
    await waitFor(() => expect(applied).toHaveBeenCalledWith(1));
    expect(api.applyOsanProjectExcel).toHaveBeenCalledWith('dev-user', expect.any(File), 'file-hash', expect.stringMatching(/^[0-9a-f-]{36}$/), expect.any(AbortSignal));
  });

  it('blocks all registration when any row or file has errors and resets preview on reselection', async () => {
    vi.mocked(api.previewOsanProjectExcel).mockResolvedValue({ ...preview, errorCount: 1, rows: [{ ...preview.rows[0], errors: ['이미 등록된 코드입니다.'] }] });
    render(<OsanProjectExcelDialog developmentUserKey="dev-user" onClose={vi.fn()} onApplied={vi.fn()} />);
    await showPreview();
    expect(screen.getByText('이미 등록된 코드입니다.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '1개 프로젝트 등록' })).toBeDisabled();
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
    expect(api.applyOsanProjectExcel).toHaveBeenCalledTimes(1);
    expect(screen.getByLabelText('작성한 엑셀 파일')).toBeDisabled();
    expect(screen.getByRole('button', { name: '닫기' })).toBeDisabled();
    fireEvent.keyDown(screen.getByRole('heading', { name: '프로젝트 엑셀 업로드' }), { key: 'Escape' });
    expect(close).not.toHaveBeenCalled();
    await act(async () => rejectApply(new ApiError(0, '연결을 확인해 주세요.')));
    expect(await screen.findByRole('alert')).toHaveTextContent('연결을 확인');
    fireEvent.click(screen.getByRole('button', { name: '1개 프로젝트 등록' }));
    await waitFor(() => expect(api.applyOsanProjectExcel).toHaveBeenCalledTimes(2));
    const calls = vi.mocked(api.applyOsanProjectExcel).mock.calls;
    expect(calls[1][3]).toBe(calls[0][3]);
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
