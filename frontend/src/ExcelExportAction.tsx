import { useRef, useState } from 'react';
import { ApiError, type ExcelExportDownload } from './api';
import { actionErrorMessage, type ActionFeedbackTone } from './useActionFeedback';

export function ExcelExportAction({
  exportFile,
  scopeLabel,
  label = 'Excel 내보내기',
  disabled = false,
  disabledReason,
  unprocessableEntityHint = '조건을 좁혀 다시 시도해 주세요.',
  onUnprocessableEntity,
  onBusyChange
}: {
  exportFile: () => Promise<ExcelExportDownload>;
  scopeLabel: string;
  label?: string;
  disabled?: boolean;
  disabledReason?: string;
  unprocessableEntityHint?: string;
  onUnprocessableEntity?: () => void;
  onBusyChange?: (isBusy: boolean) => void;
}) {
  const [isExporting, setIsExporting] = useState(false);
  const [message, setMessage] = useState('');
  const [tone, setTone] = useState<ActionFeedbackTone>('neutral');
  const busyRef = useRef(false);

  async function runExport() {
    if (busyRef.current || disabled) {
      return;
    }

    busyRef.current = true;
    setIsExporting(true);
    onBusyChange?.(true);
    setTone('loading');
    setMessage('Excel 파일을 생성하는 중입니다.');

    let file: ExcelExportDownload;
    try {
      file = await exportFile();
    } catch (error: unknown) {
      setTone('error');
      if (error instanceof ApiError && error.status === 422) {
        onUnprocessableEntity?.();
        setMessage(error.message.includes(unprocessableEntityHint)
          ? error.message
          : `${error.message} ${unprocessableEntityHint}`);
      } else if (error instanceof ApiError && error.status === 429) {
        setMessage(`${error.message} 잠시 후 다시 시도해 주세요.`);
      } else {
        setMessage(actionErrorMessage(error, 'Excel 파일을 생성할 수 없습니다.'));
      }
      busyRef.current = false;
      setIsExporting(false);
      onBusyChange?.(false);
      return;
    }

    let url: string | null = null;
    try {
      url = URL.createObjectURL(file.blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = file.fileName;
      document.body.append(anchor);
      anchor.click();
      anchor.remove();
      setTone('success');
      setMessage(file.rowCount === 0
        ? '조건에 맞는 데이터가 없어 0건 파일을 생성했습니다'
        : 'Excel 파일 생성을 완료했습니다');
    } catch {
      setTone('partial');
      setMessage('Excel 파일 생성은 완료됐지만 다운로드를 시작하지 못했습니다. 다시 시도해 주세요.');
    } finally {
      if (url) URL.revokeObjectURL(url);
      busyRef.current = false;
      setIsExporting(false);
      onBusyChange?.(false);
    }
  }

  return (
    <div className="excel-export-action">
      <button
        type="button"
        className="excel-export-button"
        disabled={isExporting || disabled}
        title={disabled ? disabledReason : undefined}
        onClick={() => void runExport()}
      >
        <span aria-hidden="true">↗</span>
        {isExporting ? '생성 중' : label}
      </button>
      <small className="excel-export-scope">{scopeLabel}</small>
      {message ? (
        <span
          className="action-feedback excel-export-feedback"
          data-tone={tone}
          role={tone === 'error' ? 'alert' : 'status'}
          aria-live={tone === 'error' ? 'assertive' : 'polite'}
        >
          {message}
        </span>
      ) : null}
    </div>
  );
}
