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
    fireEvent.click(screen.getByRole('button', { name: '저장하고 최종확인' }));
    expect(await screen.findByRole('button', { name: visibleAction })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: hiddenAction })).not.toBeInTheDocument();
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

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}
