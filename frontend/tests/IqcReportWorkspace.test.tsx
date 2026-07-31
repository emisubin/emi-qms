import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { setRuntimeMutationAllowed } from '../src/api';
import { IqcReportWorkspace } from '../src/IqcReportWorkspace';
import type { IqcReport, SaveIqcItemResponse } from '../src/iqc-report';

const attemptId = '91000000-0000-0000-0000-000000000001';
const reportId = '92000000-0000-0000-0000-000000000001';
const itemId = '93000000-0000-0000-0000-000000000001';

describe('IqcReportWorkspace', () => {
  beforeEach(() => {
    setRuntimeMutationAllowed(true);
  });

  afterEach(() => {
    setRuntimeMutationAllowed(false);
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it.each([
    ['Pass', '합격 · 성적서 확정', '부적합 · 입고 차단'],
    ['Fail', '부적합 · 입고 차단', '합격 · 성적서 확정']
  ] as const)('shows only the %s-derived final action', async (checkResult, visibleAction, hiddenAction) => {
    let current = report([]);
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/quality/iqc/${attemptId}/report`) return json(current);
      if (url.pathname === `/api/quality/iqc/reports/${reportId}/responses`) {
        const body = JSON.parse(String(init?.body)) as { responses: SaveIqcItemResponse[] };
        current = report(body.responses);
        return json(current);
      }
      return json({ title: 'not found' }, 404);
    }));

    render(
      <IqcReportWorkspace
        attemptId={attemptId}
        developmentUserKey="dev-quality"
        canInspect
        onClose={vi.fn()}
        onChanged={vi.fn()}
      />
    );

    fireEvent.click(await screen.findByRole('button', { name: checkResult === 'Pass' ? '적합' : '부적합' }));
    fireEvent.click(screen.getByRole('button', { name: '검사항목·사진 저장 후 최종확인' }));
    expect(await screen.findByRole('button', { name: visibleAction })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: hiddenAction })).not.toBeInTheDocument();
  });

  it('uploads and finalizes a signed scan for category-based enclosure IQC', async () => {
    let current = scanReport();
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/quality/iqc/${attemptId}/report`) return json(current);
      if (url.pathname === `/api/quality/iqc/${attemptId}/reports` && init?.method === 'POST') {
        current = { ...current, reportId, reportStatus: 'Draft', reportVersion: 1 };
        return json(current);
      }
      if (url.pathname === `/api/quality/iqc/scan-reports/${reportId}/attachments` && init?.method === 'POST') {
        current = {
          ...current,
          reportVersion: 2,
          scanAttachments: [{
            attachmentId: '96000000-0000-0000-0000-000000000001',
            originalFileName: 'signed-iqc.png',
            normalizedMime: 'image/png',
            byteSize: 8,
            createdAtUtc: '2026-07-30T00:00:00Z'
          }]
        };
        return json(current);
      }
      if (url.pathname === `/api/quality/iqc/scan-reports/${reportId}/finalize` && init?.method === 'POST') {
        current = {
          ...current,
          reportStatus: 'Finalized',
          reportVersion: 3,
          attemptStatus: 'Passed',
          receiptVersion: 3,
          result: 'Passed',
          reason: '서명 검사서 확인 완료',
          canEdit: false,
          finalizedAtUtc: '2026-07-30T01:00:00Z',
          finalizedBy: '품질 담당자'
        };
        return json(current);
      }
      return json({ title: 'not found' }, 404);
    }));

    render(
      <IqcReportWorkspace
        attemptId={attemptId}
        developmentUserKey="dev-quality"
        canInspect
        onClose={vi.fn()}
        onChanged={vi.fn()}
      />
    );

    fireEvent.click(await screen.findByRole('button', { name: '검사 시작' }));
    const file = new File([new Uint8Array([137, 80, 78, 71, 13, 10, 26, 10])], 'signed-iqc.png', { type: 'image/png' });
    fireEvent.change(await screen.findByLabelText('검사서 파일 추가'), { target: { files: [file] } });
    expect(await screen.findByRole('button', { name: 'signed-iqc.png' })).toBeInTheDocument();
    fireEvent.change(screen.getByRole('textbox', { name: '판정 사유' }), { target: { value: '서명 검사서 확인 완료' } });
    fireEvent.click(screen.getByRole('button', { name: '합격 확정' }));
    expect(await screen.findByText('IQC 합격')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '삭제' })).not.toBeInTheDocument();
  });
});

function report(responses: SaveIqcItemResponse[]): IqcReport {
  return {
    attemptId,
    receiptId: '94000000-0000-0000-0000-000000000001',
    projectId: '95000000-0000-0000-0000-000000000001',
    projectCode: 'IQC-DECISION',
    projectTitle: 'IQC 판정 단일화',
    orderItem: '부스바',
    quantity: 7,
    unit: 'EA',
    attemptNumber: 1,
    receiptVersion: 2,
    attemptStatus: 'Requested',
    decisionMode: 'Detailed',
    reportId,
    reportStatus: 'Draft',
    reportVersion: 1,
    result: null,
    reason: null,
    pdfStatus: null,
    pdfErrorCode: null,
    templateVersion: 1,
    canEdit: true,
    reinspectionSource: null,
    items: [{
      itemId,
      itemCode: 'ITEM_SPEC',
      displayOrder: 1,
      label: '품목 규격',
      guidance: '발주 규격과 일치하는지 확인합니다.',
      responseType: 'Check',
      isRequired: true,
      requiresPhoto: false,
      maxTextLength: null
    }],
    responses,
    photos: [],
    finalizedAtUtc: null,
    finalizedBy: null
  };
}

function scanReport(): IqcReport {
  return {
    ...report([]),
    orderItem: '외함',
    decisionMode: 'ScanBased',
    reportId: null,
    reportStatus: null,
    reportVersion: null,
    items: [],
    responses: [],
    photos: [],
    scanAttachments: [],
    scanHistory: []
  };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}
