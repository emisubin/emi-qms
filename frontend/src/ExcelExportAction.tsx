import { useState } from 'react';
import { ApiError, type ExcelExportDownload } from './api';

export function ExcelExportAction({
  exportFile,
  scopeLabel,
  label = 'Excel 내보내기'
}: {
  exportFile: () => Promise<ExcelExportDownload>;
  scopeLabel: string;
  label?: string;
}) {
  const [isExporting, setIsExporting] = useState(false);
  const [message, setMessage] = useState('');
  const [tone, setTone] = useState<'success' | 'error'>('success');

  async function runExport() {
    if (isExporting) {
      return;
    }

    setIsExporting(true);
    setMessage('');
    try {
      const file = await exportFile();
      const url = URL.createObjectURL(file.blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = file.fileName;
      document.body.append(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
      setTone('success');
      setMessage(file.rowCount === 0
        ? '조건에 맞는 데이터가 없어 0건 파일을 생성했습니다'
        : 'Excel 파일 생성을 완료했습니다');
    } catch (error: unknown) {
      setTone('error');
      if (error instanceof ApiError && error.status === 422) {
        setMessage(`${error.message} 조건을 좁혀 다시 시도해 주세요.`);
      } else if (error instanceof ApiError && error.status === 429) {
        setMessage(`${error.message} 잠시 후 다시 시도해 주세요.`);
      } else {
        setMessage(error instanceof Error ? error.message : 'Excel 파일을 생성할 수 없습니다.');
      }
    } finally {
      setIsExporting(false);
    }
  }

  return (
    <div className="excel-export-action">
      <button type="button" className="excel-export-button" disabled={isExporting} onClick={() => void runExport()}>
        <span aria-hidden="true">↗</span>
        {isExporting ? '생성 중' : label}
      </button>
      <small className="excel-export-scope">{scopeLabel}</small>
      <span
        className={tone === 'error' ? 'excel-export-feedback error-text' : 'excel-export-feedback success-text'}
        aria-live="polite"
      >
        {message}
      </span>
    </div>
  );
}
