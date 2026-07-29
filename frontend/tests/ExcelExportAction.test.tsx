import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../src/api';
import { ExcelExportAction } from '../src/ExcelExportAction';

describe('ExcelExportAction', () => {
  beforeEach(() => {
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:export') });
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('blocks duplicate requests and reports generation completion', async () => {
    let resolveExport: ((value: { blob: Blob; fileName: string; rowCount: number }) => void) | undefined;
    const exportFile = vi.fn(() => new Promise<{ blob: Blob; fileName: string; rowCount: number }>((resolve) => {
      resolveExport = resolve;
    }));
    render(<ExcelExportAction exportFile={exportFile} scopeLabel="현재 필터" />);

    const button = screen.getByRole('button', { name: /Excel 내보내기/ });
    fireEvent.click(button);
    fireEvent.click(button);
    expect(exportFile).toHaveBeenCalledTimes(1);
    expect(button).toBeDisabled();

    resolveExport?.({ blob: new Blob(['xlsx']), fileName: 'export.xlsx', rowCount: 3 });
    expect(await screen.findByText('Excel 파일 생성을 완료했습니다')).toBeInTheDocument();
    await waitFor(() => expect(button).not.toBeDisabled());
    expect(URL.createObjectURL).toHaveBeenCalledTimes(1);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:export');
  });

  it('reports a valid zero-row file explicitly', async () => {
    const exportFile = vi.fn().mockResolvedValue({ blob: new Blob(['xlsx']), fileName: 'empty.xlsx', rowCount: 0 });
    render(<ExcelExportAction exportFile={exportFile} scopeLabel="전체" />);

    fireEvent.click(screen.getByRole('button', { name: /Excel 내보내기/ }));

    expect(await screen.findByText('조건에 맞는 데이터가 없어 0건 파일을 생성했습니다')).toBeInTheDocument();
  });

  it('reports partial success and always revokes the object URL when the browser download trigger fails', async () => {
    vi.mocked(HTMLAnchorElement.prototype.click).mockImplementation(() => {
      throw new Error('browser download blocked');
    });
    const exportFile = vi.fn().mockResolvedValue({ blob: new Blob(['xlsx']), fileName: 'export.xlsx', rowCount: 3 });
    render(<ExcelExportAction exportFile={exportFile} scopeLabel="현재 필터" />);

    fireEvent.click(screen.getByRole('button', { name: /Excel 내보내기/ }));

    const feedback = await screen.findByText('Excel 파일 생성은 완료됐지만 다운로드를 시작하지 못했습니다. 다시 시도해 주세요.');
    expect(feedback).toHaveAttribute('data-tone', 'partial');
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:export');
  });

  it.each([
    [422, '조건을 좁혀 다시 시도해 주세요'],
    [429, '잠시 후 다시 시도해 주세요']
  ])('shows an actionable message for status %s', async (status, message) => {
    const exportFile = vi.fn().mockRejectedValue(new ApiError(status, 'Excel 파일을 생성할 수 없습니다.'));
    render(<ExcelExportAction exportFile={exportFile} scopeLabel="현재 필터" />);

    fireEvent.click(screen.getByRole('button', { name: /Excel 내보내기/ }));

    expect(await screen.findByText(new RegExp(message))).toBeInTheDocument();
  });

  it('supports a disabled selection state, custom 422 recovery, and busy notifications', async () => {
    const onBusyChange = vi.fn();
    const onUnprocessableEntity = vi.fn();
    const exportFile = vi.fn().mockRejectedValue(new ApiError(422, '선택한 프로젝트를 내보낼 수 없습니다.'));
    const { rerender } = render(
      <ExcelExportAction
        exportFile={exportFile}
        scopeLabel="0건 선택"
        disabled
        disabledReason="프로젝트를 선택해 주세요."
      />
    );

    const button = screen.getByRole('button', { name: /Excel 내보내기/ });
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('title', '프로젝트를 선택해 주세요.');

    rerender(
      <ExcelExportAction
        exportFile={exportFile}
        scopeLabel="2건 선택"
        unprocessableEntityHint="목록을 새로고침한 뒤 다시 선택해 주세요."
        onUnprocessableEntity={onUnprocessableEntity}
        onBusyChange={onBusyChange}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /Excel 내보내기/ }));

    expect(await screen.findByText(/목록을 새로고침한 뒤 다시 선택해 주세요/)).toBeInTheDocument();
    expect(onUnprocessableEntity).toHaveBeenCalledTimes(1);
    expect(onBusyChange).toHaveBeenNthCalledWith(1, true);
    expect(onBusyChange).toHaveBeenLastCalledWith(false);
  });
});
