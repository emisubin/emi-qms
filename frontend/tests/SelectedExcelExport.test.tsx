import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  ApiError,
  exportSelectedRowsExcel,
  getSelectedExportColumns
} from '../src/api';
import { SelectedExportTray } from '../src/SelectedExcelExport';

vi.mock('../src/api', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../src/api')>();
  return {
    ...actual,
    getSelectedExportColumns: vi.fn(),
    exportSelectedRowsExcel: vi.fn()
  };
});

const columns = [
  { key: 'project-code', label: 'PJT Code', required: true },
  { key: 'column-title', label: 'PJT Title', required: false },
  { key: 'column-status', label: '상태', required: false }
];

function renderTray(exportFile?: () => Promise<{ blob: Blob; fileName: string; rowCount: number }>) {
  return render(
    <SelectedExportTray
      developmentUserKey="synthetic-user"
      screen={exportFile ? 'form-templates' : 'projects'}
      visibleIds={['row-1', 'row-2']}
      selectedIds={new Set(['row-1'])}
      allSelected={false}
      busy={false}
      filters={{ status: 'Active' }}
      exportFile={exportFile}
      onBusyChange={vi.fn()}
      onToggleAll={vi.fn()}
      onClear={vi.fn()}
    />
  );
}

describe('SelectedExportTray column picker', () => {
  beforeEach(() => {
    vi.mocked(getSelectedExportColumns).mockResolvedValue(columns);
    vi.mocked(exportSelectedRowsExcel).mockResolvedValue({ blob: new Blob(['xlsx']), fileName: 'selected.xlsx', rowCount: 1 });
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:selected') });
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.clearAllMocks();
    vi.restoreAllMocks();
  });

  it('loads server metadata, locks required columns, and exports a server-ordered subset', async () => {
    renderTray();

    fireEvent.click(screen.getByRole('button', { name: /컬럼 선택/ }));
    const required = await screen.findByRole('checkbox', { name: /PJT Code/ });
    const title = screen.getByRole('checkbox', { name: 'PJT Title' });
    const status = screen.getByRole('checkbox', { name: '상태' });
    expect(required).toBeChecked();
    expect(required).toBeDisabled();
    fireEvent.click(title);
    fireEvent.click(status);
    expect(screen.getAllByText('컬럼 1/3').length).toBeGreaterThan(0);

    fireEvent.click(screen.getByRole('button', { name: '선택 Excel 내보내기' }));

    await waitFor(() => expect(exportSelectedRowsExcel).toHaveBeenCalledWith(
      'synthetic-user',
      'projects',
      ['row-1'],
      { status: 'Active' },
      ['project-code']
    ));
    expect(await screen.findByText('Excel 파일 생성을 완료했습니다')).toBeInTheDocument();
  });

  it('clears stale custom columns after a 422 and reloads metadata on the next open', async () => {
    vi.mocked(exportSelectedRowsExcel).mockRejectedValueOnce(new ApiError(422, '내보낼 컬럼을 확인해 주세요.'));
    renderTray();

    fireEvent.click(screen.getByRole('button', { name: /컬럼 선택/ }));
    fireEvent.click(await screen.findByRole('checkbox', { name: '상태' }));
    fireEvent.click(screen.getByRole('button', { name: '선택 Excel 내보내기' }));

    expect(await screen.findByText(/컬럼 선택이 초기화되었습니다/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /컬럼 선택/ }));
    await waitFor(() => expect(getSelectedExportColumns).toHaveBeenCalledTimes(2));
  });

  it('does not show the common picker for a custom workbook export', () => {
    renderTray(vi.fn().mockResolvedValue({ blob: new Blob(['xlsx']), fileName: 'forms.xlsx', rowCount: 1 }));

    expect(screen.queryByRole('button', { name: /컬럼 선택/ })).not.toBeInTheDocument();
  });
});
