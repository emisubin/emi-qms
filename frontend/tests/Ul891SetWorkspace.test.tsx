import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { setRuntimeMutationAllowed } from '../src/api';
import { Ul891SetWorkspace } from '../src/Ul891SetWorkspace';
import type { Ul891SetStructure } from '../src/ul891Sets';

describe('Ul891SetWorkspace', () => {
  beforeEach(() => setRuntimeMutationAllowed(true));
  afterEach(() => { setRuntimeMutationAllowed(false); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

  it('groups physical panels under their set specification and opens the exact panel', async () => {
    const onOpenPanel = vi.fn();
    const onEdit = vi.fn();
    render(<Ul891SetWorkspace developmentUserKey="dev-design" projectId="project-1" mode="design" presentation="summary" initialStructure={structure()} onEdit={onEdit} onOpenPanel={onOpenPanel} />);

    expect(await screen.findByText('MCC 메인 세트')).toBeInTheDocument();
    expect(screen.getByText('현재 설계 2개 위치 · 실물 2세트')).toBeInTheDocument();
    expect(screen.getByRole('table', { name: '저장된 세트 공통 설계정보' })).toBeInTheDocument();
    expect(screen.queryByText('규격')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('세트 사양명')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '수정' }));
    expect(onEdit).toHaveBeenCalledOnce();
    const panelButton = screen.getByRole('button', { name: /1번 위치 MAIN A/ });
    fireEvent.click(panelButton);
    expect(onOpenPanel).toHaveBeenCalledWith('panel-a-1');
  });

  it('saves the current design directly without a version or publish step', async () => {
    const requests: string[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname;
      requests.push(`${init?.method ?? 'GET'} ${path}`);
      if (init?.method === 'PUT') {
        return json({ operationId: '00000000-0000-0000-0000-000000000000', projectId: 'project-1', action: 'CurrentDesignUpdated', replayed: false });
      }
      return json(structure());
    }));

    render(<Ul891SetWorkspace developmentUserKey="dev-design" projectId="project-1" mode="design" presentation="edit" initialStructure={structure()} onOpenPanel={vi.fn()} />);

    expect(screen.getByRole('button', { name: '저장' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '사양 확정' })).not.toBeInTheDocument();
    expect(screen.queryByText(/v1/)).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('2번 위치 패널명'), { target: { value: 'MAIN A' } });
    fireEvent.click(screen.getByRole('button', { name: '저장' }));
    expect(await screen.findByText('저장되었습니다. 필요할 때 같은 화면에서 다시 수정할 수 있습니다.')).toBeInTheDocument();
    expect(requests).toContain('PUT /api/projects/project-1/set-specs/spec-1/design');
  });

  it('shows project by shipment-month billing totals without hiding the set order', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input)).pathname;
      return path.endsWith('/monthly-billing') ? json(monthlyBilling()) : json(structure());
    }));
    render(<Ul891SetWorkspace developmentUserKey="dev-sales" projectId="project-1" mode="sales" onOpenPanel={vi.fn()} />);

    expect(await screen.findByText('세트 주문 구성')).toBeInTheDocument();
    expect(screen.getByText('월별 부분출하 발행요청')).toBeInTheDocument();
    expect(screen.getByText('2026-07')).toBeInTheDocument();
    expect(screen.getByText(/12,000,000 KRW/)).toBeInTheDocument();
    expect(screen.getByText(/P01, P02/)).toBeInTheDocument();
  });

  it('adds a new set specification using only its panel count', async () => {
    const requests: Array<{ path: string; body: Record<string, unknown> | null }> = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname;
      requests.push({ path, body: init?.body ? JSON.parse(String(init.body)) as Record<string, unknown> : null });
      return path.endsWith('/monthly-billing') ? json(monthlyBilling()) : json(structure());
    }));
    render(<Ul891SetWorkspace developmentUserKey="dev-sales" projectId="project-1" mode="sales" onOpenPanel={vi.fn()} />);

    expect(await screen.findByText('새 세트 사양 추가')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('세트 사양명'), { target: { value: 'AUX 세트' } });
    fireEvent.change(screen.getByLabelText('주문 수량'), { target: { value: '2' } });
    fireEvent.change(screen.getByLabelText('세트당 패널 수'), { target: { value: '3' } });
    fireEvent.change(screen.getAllByLabelText('변경 사유')[0], { target: { value: '고객 추가 주문' } });
    fireEvent.click(screen.getByRole('button', { name: '새 사양 추가' }));

    await screen.findByText('완료 · 새 세트 사양과 개별 패널을 추가했습니다.');
    const posted = requests.find((request) => request.path.endsWith('/set-specs') && request.body);
    expect(posted?.body).toMatchObject({ expectedSpecCount: 1, name: 'AUX 세트', quantity: 2, panelCount: 3, reason: '고객 추가 주문' });
    expect(posted?.body).not.toHaveProperty('components');
  });
});

function structure() {
  const components = [
    { componentId: 'component-a', componentCode: 'A', panelName: 'MAIN A', panelSpecification: '800x2000', widthMm: 800, heightMm: 2000, depthMm: 600, sortOrder: 1 },
    { componentId: 'component-b', componentCode: 'B', panelName: 'MAIN B', panelSpecification: '700x2000', widthMm: 700, heightMm: 2000, depthMm: 600, sortOrder: 2 }
  ];
  const currentDesign = components.map((component) => ({ slotId: `slot-${component.componentCode.toLowerCase()}`, positionNumber: component.sortOrder, panelName: component.panelName, panelSpecification: component.panelSpecification, widthMm: component.widthMm, heightMm: component.heightMm, depthMm: component.depthMm, rowVersion: 1 }));
  const panel = (suffix: string, code: string) => ({ panelId: `panel-${suffix}`, sequenceNumber: suffix.endsWith('1') ? 1 : 2, displayCode: suffix.endsWith('1') ? 'P01' : 'P02', componentCode: code, designSlotId: `slot-${code.toLowerCase()}`, positionNumber: code === 'A' ? 1 : 2, panelName: code === 'A' ? 'MAIN A' : 'MAIN B', panelSpecification: code === 'A' ? '800x2000' : '700x2000', panelStatus: 'Active', workflowStage: 'BeforeManufacturing', packingUnitLabel: null, departureDate: null, delivered: false });
  return {
    projectId: 'project-1', structureMode: 'Ul891Set', isLegacyFlat: false, canEditOrder: true, canEditDesign: true,
    specs: [{
      specId: 'spec-1', specNo: 1, name: 'MCC 메인 세트', rowVersion: 2, activeInstanceCount: 2,
      currentDesign,
      versions: [{ versionId: 'version-1', versionNumber: 1, status: 'Published', revisionReason: '초도', publishedAtUtc: '2026-07-01T00:00:00Z', components }],
      instances: [
        { instanceId: 'instance-1', instanceNumber: 1, specVersionId: 'version-1', specVersionNumber: 1, status: 'Active', rowVersion: 1, hasStarted: false, hasDeliveredPanel: false, panels: [panel('a-1', 'A'), panel('b-2', 'B')] },
        { instanceId: 'instance-2', instanceNumber: 2, specVersionId: 'version-1', specVersionNumber: 1, status: 'Active', rowVersion: 1, hasStarted: false, hasDeliveredPanel: false, panels: [] }
      ]
    }],
    orderedProcurementItems: [], recoveryCases: []
  } as Ul891SetStructure;
}

function monthlyBilling() {
  return {
    projectId: 'project-1', structureMode: 'Ul891Set', salesAmount: 12000000, currencyCode: 'KRW', confirmedAmount: 4000000,
    currentRequestedAmount: 4000000, remainingAmount: 8000000, canReadAmounts: true, canMutate: true, unbilledMonths: [],
    ledgers: [{
      ledgerId: 'ledger-1', billingMonth: '2026-07-01', kind: 'Shipment', status: 'InvoiceConfirmed', rowVersion: 3,
      currentShipmentEvidence: [], availableRecoveryCases: [],
      revisions: [{ revisionId: 'revision-1', revisionNumber: 1, amount: 4000000, note: '7월 출하', isAdjustment: false, adjustmentReason: null, createdAtUtc: '2026-07-20T00:00:00Z', invoiceConfirmedDate: '2026-07-21', invoiceNumber: 'INV-01', recoveryCaseIds: [], panels: [{ panelId: 'panel-a-1', displayCode: 'P01', setLabel: 'MCC-1-A', packingUnitLabel: 'PKG-1', departureDate: '2026-07-20' }, { panelId: 'panel-b-2', displayCode: 'P02', setLabel: 'MCC-1-B', packingUnitLabel: 'PKG-1', departureDate: '2026-07-20' }] }]
    }]
  };
}

function json(value: unknown) { return new Response(JSON.stringify(value), { status: 200, headers: { 'Content-Type': 'application/json' } }); }
