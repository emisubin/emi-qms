import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  addUl891SetSpec,
  applyUl891Version,
  cancelUl891Instances,
  confirmUl891MonthlyBilling,
  createUl891MonthlyBillingRevision,
  createUl891Version,
  getUl891MonthlyBilling,
  getUl891SetStructure,
  increaseUl891Instances,
  openUl891MonthlyBilling,
  publishUl891Version,
  recoverUl891Case,
  updateUl891Draft
} from './api';
import type {
  MonthlyBilling,
  MonthlyBillingLedger,
  Ul891SetComponent,
  Ul891SetSpec,
  Ul891SetStructure,
  Ul891SetVersion
} from './ul891Sets';

type Props = {
  developmentUserKey: string;
  projectId: string;
  mode: 'sales' | 'design';
  onOpenPanel: (panelId: string) => void;
  presentation?: 'summary' | 'edit';
  initialStructure?: Ul891SetStructure;
  onEdit?: () => void;
};

export function Ul891SetWorkspace({
  developmentUserKey,
  projectId,
  mode,
  onOpenPanel,
  presentation = 'summary',
  initialStructure,
  onEdit
}: Props) {
  const [structure, setStructure] = useState<Ul891SetStructure | null>(initialStructure ?? null);
  const [billing, setBilling] = useState<MonthlyBilling | null>(null);
  const [loading, setLoading] = useState(!initialStructure);
  const [message, setMessage] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const next = await getUl891SetStructure(developmentUserKey, projectId);
      setStructure(next);
      setBilling(mode === 'sales' && next.structureMode === 'Ul891Set'
        ? await getUl891MonthlyBilling(developmentUserKey, projectId)
        : null);
    } catch (error) {
      setMessage(errorMessage(error));
    } finally {
      setLoading(false);
    }
  }, [developmentUserKey, mode, projectId]);

  useEffect(() => {
    if (initialStructure?.projectId === projectId) {
      setStructure(initialStructure);
      setLoading(false);
      return;
    }
    void load();
  }, [initialStructure, load, projectId]);

  if (loading) return <section className="subsection ul891-workspace"><p className="muted-text">UL891 세트 구성을 불러오는 중입니다.</p></section>;
  if (!structure && message) return <section className="subsection ul891-workspace"><p className="error-text" role="alert">{message}</p></section>;
  if (!structure || structure.structureMode !== 'Ul891Set') {
    return structure?.isLegacyFlat ? (
      <section className="subsection ul891-workspace legacy"><strong>기존 UL891 면수형 프로젝트</strong><p>기존 패널 입력 구조를 유지합니다. 신규 프로젝트부터 세트 사양 구조가 적용됩니다.</p></section>
    ) : null;
  }

  const activePanels = structure.specs.flatMap((spec) => spec.instances.filter((item) => item.status === 'Active').flatMap((item) => item.panels.filter((panel) => panel.panelStatus === 'Active')));
  return (
    <section className="subsection ul891-workspace" aria-label={mode === 'sales' ? 'UL891 세트 주문 및 월별 발행요청' : 'UL891 세트 설계'}>
      <header className="ul891-workspace-hero">
        <div>
          <p className="eyebrow">UL891 · SET BASED</p>
          <h3>{mode === 'sales' ? '세트 주문 구성' : '세트 사양 · 개별 패널'}</h3>
          <p>{mode === 'sales' ? '세트 사양별 주문 수량을 관리하고, 실제 제조·검사·출하는 개별 패널로 추적합니다.' : '같은 세트의 공통 사양은 한 번 입력하고 각 실물 패널은 고유 ID와 이력을 유지합니다.'}</p>
        </div>
        <div className="ul891-workspace-hero-actions">
          <div className="ul891-hero-metrics">
            <span><small>세트 사양</small><strong>{structure.specs.length}</strong></span>
            <span><small>활성 세트</small><strong>{structure.specs.reduce((sum, spec) => sum + spec.activeInstanceCount, 0)}</strong></span>
            <span><small>개별 패널</small><strong>{activePanels.length}</strong></span>
          </div>
          {mode === 'design' && presentation === 'summary' && structure.canEditDesign && onEdit ? (
            <button type="button" className="primary-button" onClick={onEdit}>수정</button>
          ) : null}
        </div>
      </header>

      {mode === 'sales' ? (
        <SalesSetControls
          developmentUserKey={developmentUserKey}
          projectId={projectId}
          structure={structure}
          billing={billing}
          onChanged={load}
          onMessage={setMessage}
          onOpenPanel={onOpenPanel}
        />
      ) : (
        <DesignSetControls
          developmentUserKey={developmentUserKey}
          projectId={projectId}
          structure={structure}
          editable={presentation === 'edit'}
          onChanged={load}
          onMessage={setMessage}
          onOpenPanel={onOpenPanel}
        />
      )}
      {mode === 'sales' && message ? <p className={message.startsWith('완료') ? 'success-text' : 'error-text'} role="status">{message}</p> : null}
    </section>
  );
}

function SalesSetControls({ developmentUserKey, projectId, structure, billing, onChanged, onMessage, onOpenPanel }: {
  developmentUserKey: string; projectId: string; structure: Ul891SetStructure; billing: MonthlyBilling | null;
  onChanged: () => Promise<void>; onMessage: (value: string) => void; onOpenPanel: (panelId: string) => void;
}) {
  const [increase, setIncrease] = useState<Record<string, { quantity: string; reason: string }>>({});
  const [selectedInstances, setSelectedInstances] = useState<string[]>([]);
  const [selectedProcurement, setSelectedProcurement] = useState<string[]>([]);
  const [cancelReason, setCancelReason] = useState('');
  const [exceptionAcknowledged, setExceptionAcknowledged] = useState(false);
  const [newSpec, setNewSpec] = useState({ name: '', quantity: '1', componentCodes: 'A, B, C, D, E, F, G', reason: '' });
  const [saving, setSaving] = useState(false);

  async function addSpec() {
    const componentCodes = newSpec.componentCodes.split(',').map((value) => value.trim().toUpperCase()).filter(Boolean);
    if (!newSpec.name.trim() || !Number.isInteger(Number(newSpec.quantity)) || Number(newSpec.quantity) < 1
      || componentCodes.length === 0 || new Set(componentCodes).size !== componentCodes.length || !newSpec.reason.trim()) {
      onMessage('새 세트 사양명, 주문 수량, 쉼표로 구분한 중복 없는 구성 code와 변경 사유를 입력해 주세요.'); return;
    }
    await run(async () => {
      await addUl891SetSpec(developmentUserKey, projectId, {
        expectedSpecCount: structure.specs.length,
        name: newSpec.name.trim(),
        quantity: Number(newSpec.quantity),
        componentCodes,
        reason: newSpec.reason.trim()
      });
      setNewSpec({ name: '', quantity: '1', componentCodes: 'A, B, C, D, E, F, G', reason: '' });
      onMessage('완료 · 새 세트 사양과 개별 패널을 추가했습니다.');
    });
  }

  async function addInstances(spec: Ul891SetSpec) {
    const form = increase[spec.specId] ?? { quantity: '1', reason: '' };
    if (!Number.isInteger(Number(form.quantity)) || Number(form.quantity) < 1 || !form.reason.trim()) {
      onMessage('추가할 세트 수량과 변경 사유를 입력해 주세요.'); return;
    }
    await run(async () => {
      await increaseUl891Instances(developmentUserKey, projectId, spec.specId, { expectedActiveInstanceCount: spec.activeInstanceCount, quantity: Number(form.quantity), reason: form.reason.trim() });
      setIncrease((current) => ({ ...current, [spec.specId]: { quantity: '1', reason: '' } }));
      onMessage('완료 · 세트 주문 수량을 추가했습니다.');
    });
  }

  async function cancelSelected() {
    if (selectedInstances.length === 0 || !cancelReason.trim()) { onMessage('취소할 세트와 취소 사유를 입력해 주세요.'); return; }
    if (structure.orderedProcurementItems.length > 0 && selectedProcurement.length === 0) { onMessage('발주일이 입력된 관련 구매품목을 선택해 주세요.'); return; }
    await run(async () => {
      await cancelUl891Instances(developmentUserKey, projectId, { instanceIds: selectedInstances, procurementItemIds: selectedProcurement, reason: cancelReason.trim(), exceptionAcknowledged });
      setSelectedInstances([]); setSelectedProcurement([]); setCancelReason(''); setExceptionAcknowledged(false);
      onMessage('완료 · 선택한 세트를 취소하고 발주품 회수 추적을 생성했습니다.');
    });
  }

  async function run(action: () => Promise<void>) {
    setSaving(true); onMessage('');
    try { await action(); await onChanged(); } catch (error) { onMessage(errorMessage(error)); } finally { setSaving(false); }
  }

  return (
    <>
      {structure.canEditOrder ? <section className="ul891-action-panel ul891-add-spec-panel">
        <div><p className="eyebrow">NEW SET SPEC</p><h4>새 세트 사양 추가</h4><p>기존 패널 ID는 유지하고 새 사양·세트·패널 번호를 뒤에 이어서 만듭니다.</p></div>
        <div className="ul891-add-spec-form">
          <label>세트 사양명<input value={newSpec.name} onChange={(event) => setNewSpec((current) => ({ ...current, name: event.target.value }))} /></label>
          <label>주문 수량<input type="number" min="1" value={newSpec.quantity} onChange={(event) => setNewSpec((current) => ({ ...current, quantity: event.target.value }))} /></label>
          <label className="wide">구성 code<input value={newSpec.componentCodes} onChange={(event) => setNewSpec((current) => ({ ...current, componentCodes: event.target.value }))} /></label>
          <label className="wide">변경 사유<input value={newSpec.reason} onChange={(event) => setNewSpec((current) => ({ ...current, reason: event.target.value }))} /></label>
          <button type="button" className="primary-button" disabled={saving} onClick={() => void addSpec()}>새 사양 추가</button>
        </div>
      </section> : null}
      <div className="ul891-spec-grid">
        {structure.specs.map((spec) => {
          const form = increase[spec.specId] ?? { quantity: '1', reason: '' };
          return (
            <article className="ul891-spec-card" key={spec.specId}>
              <div className="ul891-spec-heading"><span>SET {spec.specNo}</span><div><h4>{spec.name}</h4><p>주문 {spec.activeInstanceCount}세트 · 패널 {spec.instances.filter((item) => item.status === 'Active').reduce((sum, item) => sum + item.panels.filter((panel) => panel.panelStatus === 'Active').length, 0)}개</p></div><strong>{versionLabel(spec.versions)}</strong></div>
              <div className="ul891-instance-list">
                {spec.instances.map((instance) => (
                  <article key={instance.instanceId} data-status={instance.status}>
                    <label><input type="checkbox" disabled={instance.status !== 'Active' || instance.hasDeliveredPanel} checked={selectedInstances.includes(instance.instanceId)} onChange={() => setSelectedInstances(toggle(selectedInstances, instance.instanceId))} /><span>{spec.name} · {instance.instanceNumber}번 세트</span></label>
                    <small>v{instance.specVersionNumber} · {instance.hasDeliveredPanel ? '출하 포함' : instance.hasStarted ? '착수' : '미착수'} · {instance.status === 'Cancelled' ? '취소' : '활성'}</small>
                    <PanelPills panels={instance.panels} onOpenPanel={onOpenPanel} />
                  </article>
                ))}
              </div>
              {structure.canEditOrder ? <div className="ul891-inline-form"><label>추가 세트<input type="number" min="1" value={form.quantity} onChange={(event) => setIncrease((current) => ({ ...current, [spec.specId]: { ...form, quantity: event.target.value } }))} /></label><label>변경 사유<input value={form.reason} onChange={(event) => setIncrease((current) => ({ ...current, [spec.specId]: { ...form, reason: event.target.value } }))} /></label><button type="button" disabled={saving} onClick={() => void addInstances(spec)}>수량 추가</button></div> : null}
            </article>
          );
        })}
      </div>

      {structure.canEditOrder ? <section className="ul891-action-panel">
        <div><p className="eyebrow">QUANTITY DECREASE</p><h4>선택 세트 취소</h4><p>납품된 세트는 취소할 수 없고, 착수 세트는 영향 확인이 필요합니다. 패널 ID는 취소 후에도 재사용하지 않습니다.</p></div>
        {structure.orderedProcurementItems.length > 0 ? <fieldset><legend>회수 추적할 발주품목</legend>{structure.orderedProcurementItems.map((item) => <label key={item.procurementItemId}><input type="checkbox" checked={selectedProcurement.includes(item.procurementItemId)} onChange={() => setSelectedProcurement(toggle(selectedProcurement, item.procurementItemId))} />{item.orderItem} · 발주 {item.orderDate}</label>)}</fieldset> : <p className="muted-text">발주일이 입력된 품목이 없어 회수 사례를 만들지 않습니다.</p>}
        <label>취소 사유<textarea value={cancelReason} onChange={(event) => setCancelReason(event.target.value)} /></label>
        <label className="checkbox-row"><input type="checkbox" checked={exceptionAcknowledged} onChange={(event) => setExceptionAcknowledged(event.target.checked)} />착수된 세트의 영향과 예외 취소를 확인했습니다.</label>
        <button type="button" className="danger-button" disabled={saving || selectedInstances.length === 0} onClick={() => void cancelSelected()}>선택 {selectedInstances.length}세트 취소</button>
      </section> : null}

      {billing ? <MonthlyBillingPanel developmentUserKey={developmentUserKey} projectId={projectId} billing={billing} recoveryCases={structure.recoveryCases} onChanged={onChanged} onMessage={onMessage} /> : null}
    </>
  );
}

function DesignSetControls({ developmentUserKey, projectId, structure, editable, onChanged, onMessage, onOpenPanel }: {
  developmentUserKey: string; projectId: string; structure: Ul891SetStructure;
  editable: boolean;
  onChanged: () => Promise<void>; onMessage: (value: string) => void; onOpenPanel: (panelId: string) => void;
}) {
  return <div className="ul891-spec-grid">{structure.specs.map((spec) => <DesignSpecCard key={spec.specId} developmentUserKey={developmentUserKey} projectId={projectId} spec={spec} canEdit={editable && structure.canEditDesign} onChanged={onChanged} onMessage={onMessage} onOpenPanel={onOpenPanel} />)}</div>;
}

function DesignSpecCard({ developmentUserKey, projectId, spec, canEdit, onChanged, onMessage, onOpenPanel }: {
  developmentUserKey: string; projectId: string; spec: Ul891SetSpec; canEdit: boolean; onChanged: () => Promise<void>; onMessage: (value: string) => void; onOpenPanel: (panelId: string) => void;
}) {
  const draft = spec.versions.find((version) => version.status === 'Draft');
  const published = spec.versions.find((version) => version.status === 'Published');
  const source = draft ?? published ?? spec.versions[0];
  const [name, setName] = useState(spec.name);
  const [reason, setReason] = useState(source?.revisionReason ?? '설계 사양 입력');
  const [components, setComponents] = useState<Ul891SetComponent[]>(source?.components ?? []);
  const [selectedInstances, setSelectedInstances] = useState<string[]>([]);
  const [targetVersion, setTargetVersion] = useState(published?.versionId ?? '');
  const [savingAction, setSavingAction] = useState<'temporary' | 'save' | 'new-version' | 'apply' | null>(null);
  const [feedback, setFeedback] = useState<{ tone: 'loading' | 'success' | 'error'; message: string } | null>(null);

  useEffect(() => { setName(spec.name); setReason(source?.revisionReason ?? '설계 사양 입력'); setComponents(source?.components ?? []); setTargetVersion(published?.versionId ?? ''); }, [published?.versionId, source?.components, source?.revisionReason, source?.versionId, spec.name]);
  async function run(
    action: () => Promise<unknown>,
    actionName: NonNullable<typeof savingAction>,
    loadingMessage: string,
    successMessage: string
  ) {
    setSavingAction(actionName);
    setFeedback({ tone: 'loading', message: loadingMessage });
    onMessage('');
    try {
      await action();
      setFeedback({ tone: 'success', message: successMessage });
      await onChanged();
    } catch (error) {
      const message = errorMessage(error);
      setFeedback({ tone: 'error', message });
      onMessage(message);
    } finally {
      setSavingAction(null);
    }
  }
  async function save() {
    if (!draft || !reason.trim() || components.some((item) => !item.componentCode.trim())) {
      setFeedback({ tone: 'error', message: '임시저장할 수정본, 변경 사유와 구성 code를 확인해 주세요.' });
      return;
    }
    await run(
      () => updateUl891Draft(developmentUserKey, projectId, spec.specId, draft.versionId, { expectedSpecVersion: spec.rowVersion, specName: name.trim(), revisionReason: reason.trim(), components: components.map((item) => ({ componentCode: item.componentCode.trim().toUpperCase(), panelName: clean(item.panelName), panelSpecification: clean(item.panelSpecification), widthMm: item.widthMm, heightMm: item.heightMm, depthMm: item.depthMm })) }),
      'temporary',
      '임시저장 중입니다.',
      '임시저장되었습니다. 계속 수정하거나 최종 저장할 수 있습니다.'
    );
  }
  async function publish() {
    if (!draft || !reason.trim() || components.some((item) => !item.componentCode.trim())) {
      setFeedback({ tone: 'error', message: '저장할 수정본, 변경 사유와 구성 code를 확인해 주세요.' });
      return;
    }
    await run(
      async () => {
        await updateUl891Draft(developmentUserKey, projectId, spec.specId, draft.versionId, {
          expectedSpecVersion: spec.rowVersion,
          specName: name.trim(),
          revisionReason: reason.trim(),
          components: components.map((item) => ({
            componentCode: item.componentCode.trim().toUpperCase(),
            panelName: clean(item.panelName),
            panelSpecification: clean(item.panelSpecification),
            widthMm: item.widthMm,
            heightMm: item.heightMm,
            depthMm: item.depthMm
          }))
        });
        await publishUl891Version(developmentUserKey, projectId, spec.specId, draft.versionId, reason.trim());
      },
      'save',
      '저장 중입니다.',
      '저장되었습니다. 이 사양이 적용된 패널은 제조를 시작할 수 있습니다.'
    );
  }
  async function createVersion() {
    if (!reason.trim()) {
      setFeedback({ tone: 'error', message: '새 수정본을 만드는 사유를 입력해 주세요.' });
      return;
    }
    await run(
      () => createUl891Version(developmentUserKey, projectId, spec.specId, reason.trim()),
      'new-version',
      '새 수정본을 만드는 중입니다.',
      '새 수정본을 만들었습니다. 내용을 변경한 뒤 임시저장 또는 저장해 주세요.'
    );
  }
  async function applyVersion() {
    if (!targetVersion || selectedInstances.length === 0 || !reason.trim()) {
      setFeedback({ tone: 'error', message: '적용할 저장 버전, 세트와 사유를 입력해 주세요.' });
      return;
    }
    await run(
      () => applyUl891Version(developmentUserKey, projectId, spec.specId, { expectedActiveInstanceCount: spec.activeInstanceCount, versionId: targetVersion, instanceIds: selectedInstances, reason: reason.trim() }),
      'apply',
      '선택 세트에 적용 중입니다.',
      '선택한 세트에 저장된 사양을 적용했습니다.'
    );
    setSelectedInstances([]);
  }

  return <article className="ul891-spec-card design">
    <div className="ul891-spec-heading"><span>SET {spec.specNo}</span><div><h4>{spec.name}</h4><p>공통 사양 {components.length}개 · 실물 {spec.activeInstanceCount}세트</p></div><strong>{versionLabel(spec.versions)}</strong></div>
    <div className="ul891-version-rail" aria-label="사양 저장 이력">{spec.versions.map((version) => <span key={version.versionId} data-status={version.status}>v{version.versionNumber} {versionStatusLabel(version.status)}</span>)}</div>
    {!canEdit || !draft ? <DesignComponentSummary components={source?.components ?? []} /> : null}
    {canEdit && draft ? <div className="ul891-design-form">
      <label>세트 사양명<input value={name} onChange={(event) => setName(event.target.value)} /></label>
      <label>변경 사유<input value={reason} onChange={(event) => setReason(event.target.value)} /></label>
      <div className="ul891-component-table"><div className="head"><span>Code</span><span>패널명</span><span>W × H × D (mm)</span></div>{components.map((component, index) => <div className="row" key={`${component.componentId}-${index}`}><input aria-label={`구성 ${index + 1} code`} value={component.componentCode} onChange={(event) => setComponents(updateComponent(components, index, { componentCode: event.target.value }))} /><input aria-label={`${component.componentCode} 패널명`} value={component.panelName ?? ''} onChange={(event) => setComponents(updateComponent(components, index, { panelName: event.target.value }))} /><span>{(['widthMm', 'heightMm', 'depthMm'] as const).map((field) => <input key={field} type="number" min="0" value={component[field] ?? ''} onChange={(event) => setComponents(updateComponent(components, index, { [field]: event.target.value ? Number(event.target.value) : null }))} />)}</span></div>)}</div>
      <div className="button-row"><button type="button" disabled={savingAction !== null} onClick={() => setComponents([...components, blankComponent(components.length)])}>구성 추가</button><button type="button" disabled={savingAction !== null} onClick={() => void save()}>{savingAction === 'temporary' ? '임시저장 중' : '임시저장'}</button><button type="button" className="primary-button" disabled={savingAction !== null} onClick={() => void publish()}>{savingAction === 'save' ? '저장 중' : '저장'}</button></div>
    </div> : null}
    {canEdit && !draft && published ? <div className="ul891-inline-form"><label>새 수정본 사유<input value={reason} onChange={(event) => setReason(event.target.value)} /></label><button type="button" disabled={savingAction !== null} onClick={() => void createVersion()}>{savingAction === 'new-version' ? '만드는 중' : '새 수정본 만들기'}</button></div> : null}
    <div className="ul891-instance-list">{spec.instances.map((instance) => <article key={instance.instanceId} data-status={instance.status}><label>{canEdit && instance.status === 'Active' && !instance.hasDeliveredPanel ? <input type="checkbox" checked={selectedInstances.includes(instance.instanceId)} onChange={() => setSelectedInstances(toggle(selectedInstances, instance.instanceId))} /> : null}<span>{spec.name} · {instance.instanceNumber}번 세트</span></label><small>적용 사양 v{instance.specVersionNumber} · {instance.hasStarted ? '착수됨' : '미착수'}</small><PanelPills panels={instance.panels} onOpenPanel={onOpenPanel} /></article>)}</div>
    {canEdit && spec.versions.some((version) => version.status === 'Published') ? <div className="ul891-inline-form"><label>저장된 버전<select value={targetVersion} onChange={(event) => setTargetVersion(event.target.value)}>{spec.versions.filter((version) => version.status === 'Published').map((version) => <option key={version.versionId} value={version.versionId}>v{version.versionNumber}</option>)}</select></label><label>적용 사유<input value={reason} onChange={(event) => setReason(event.target.value)} /></label><button type="button" disabled={savingAction !== null || selectedInstances.length === 0} onClick={() => void applyVersion()}>{savingAction === 'apply' ? '적용 중' : '선택 세트에 적용'}</button></div> : null}
    {feedback ? <p className={`ul891-action-feedback ${feedback.tone === 'error' ? 'error-text' : feedback.tone === 'success' ? 'success-text' : 'muted-text'}`} role="status" aria-live="polite">{feedback.message}</p> : null}
  </article>;
}

function MonthlyBillingPanel({ developmentUserKey, projectId, billing, recoveryCases, onChanged, onMessage }: { developmentUserKey: string; projectId: string; billing: MonthlyBilling; recoveryCases: Ul891SetStructure['recoveryCases']; onChanged: () => Promise<void>; onMessage: (value: string) => void }) {
  const defaultMonth = new Date().toISOString().slice(0, 7);
  const [month, setMonth] = useState(defaultMonth);
  const [amount, setAmount] = useState('');
  const [note, setNote] = useState('월별 부분출하 세금계산서 발행 요청');
  const [adjustmentReason, setAdjustmentReason] = useState('');
  const [recoveryIds, setRecoveryIds] = useState<string[]>([]);
  const [confirmation, setConfirmation] = useState<Record<string, { date: string; number: string }>>({});
  const [saving, setSaving] = useState(false);
  const eligibleRecoveries = useMemo(() => [...recoveryCases, ...billing.ledgers.flatMap((item) => item.availableRecoveryCases)].filter((item, index, all) => all.findIndex((candidate) => candidate.recoveryCaseId === item.recoveryCaseId) === index), [billing, recoveryCases]);
  async function createRequest() {
    if (!/^\d{4}-\d{2}$/.test(month) || !amount || Number(amount) < 0) { onMessage('출하 월과 발행요청 금액을 입력해 주세요.'); return; }
    setSaving(true); onMessage('');
    try {
      const billingMonth = `${month}-01`;
      let current = billing;
      let ledger = current.ledgers.find((item) => item.billingMonth === billingMonth);
      if (!ledger) { await openUl891MonthlyBilling(developmentUserKey, projectId, billingMonth, recoveryIds); current = await getUl891MonthlyBilling(developmentUserKey, projectId); ledger = current.ledgers.find((item) => item.billingMonth === billingMonth); }
      if (!ledger) throw new Error('월별 원장을 생성하지 못했습니다.');
      await createUl891MonthlyBillingRevision(developmentUserKey, projectId, ledger.ledgerId, { expectedLedgerVersion: ledger.rowVersion, amount: Number(amount), note: clean(note), recoveryCaseIds: recoveryIds, adjustmentReason: ledger.status === 'InvoiceConfirmed' || ledger.status === 'AdjustmentRequired' ? clean(adjustmentReason) : null });
      setAmount(''); setRecoveryIds([]); onMessage('완료 · 해당 월 출하근거 snapshot과 발행요청 금액을 저장했습니다.'); await onChanged();
    } catch (error) { onMessage(errorMessage(error)); } finally { setSaving(false); }
  }
  async function confirm(ledger: MonthlyBillingLedger) {
    const form = confirmation[ledger.ledgerId] ?? { date: '', number: '' };
    if (!form.date || !form.number.trim()) { onMessage('회계 발행 확인일과 세금계산서 번호를 입력해 주세요.'); return; }
    setSaving(true); try { await confirmUl891MonthlyBilling(developmentUserKey, projectId, ledger.ledgerId, { expectedLedgerVersion: ledger.rowVersion, invoiceConfirmedDate: form.date, invoiceNumber: form.number.trim(), note: '회계 발행 확인' }); onMessage('완료 · 회계 발행 확인을 기록했습니다.'); await onChanged(); } catch (error) { onMessage(errorMessage(error)); } finally { setSaving(false); }
  }
  async function recover(item: typeof eligibleRecoveries[number]) { setSaving(true); try { await recoverUl891Case(developmentUserKey, projectId, item.recoveryCaseId, item.rowVersion, '영업 회수 확인'); onMessage('완료 · 발주 후 취소 품목의 회수를 확인했습니다.'); await onChanged(); } catch (error) { onMessage(errorMessage(error)); } finally { setSaving(false); } }
  return <section className="ul891-billing-panel">
    <header><div><p className="eyebrow">PROJECT × SHIPMENT MONTH</p><h4>월별 부분출하 발행요청</h4><p>매월 1일~말일 출하분을 하나의 원장으로 묶고, 늦게 출하된 패널은 revision으로 추가합니다.</p></div><div className="ul891-billing-totals"><span>판매액<strong>{money(billing.salesAmount, billing.currencyCode)}</strong></span><span>요청 합계<strong>{money(billing.currentRequestedAmount, billing.currencyCode)}</strong></span><span>잔액<strong>{money(billing.remainingAmount, billing.currencyCode)}</strong></span></div></header>
    {billing.canMutate ? <div className="ul891-billing-form"><label>출하 월<input type="month" value={month} onChange={(event) => setMonth(event.target.value)} /></label><label>발행요청 금액<input type="number" min="0" value={amount} onChange={(event) => setAmount(event.target.value)} /></label><label>요청 메모<input value={note} onChange={(event) => setNote(event.target.value)} /></label><label>조정 사유<input value={adjustmentReason} placeholder="회계 확인 후 추가 출하 시 필수" onChange={(event) => setAdjustmentReason(event.target.value)} /></label>{eligibleRecoveries.length > 0 ? <fieldset><legend>함께 청구할 발주 후 취소 품목</legend>{eligibleRecoveries.filter((item) => item.status === 'BillingRequired').map((item) => <label key={item.recoveryCaseId}><input type="checkbox" checked={recoveryIds.includes(item.recoveryCaseId)} onChange={() => setRecoveryIds(toggle(recoveryIds, item.recoveryCaseId))} />{item.procurementItemName} · {item.setInstanceNumber}번 세트</label>)}</fieldset> : null}<button type="button" className="primary-button" disabled={saving} onClick={() => void createRequest()}>월 발행요청 저장</button></div> : null}
    <div className="ul891-ledger-list">{billing.ledgers.length === 0 ? <p className="empty-text">아직 생성된 월별 발행요청이 없습니다.</p> : billing.ledgers.map((ledger) => { const latest = ledger.revisions[0]; const form = confirmation[ledger.ledgerId] ?? { date: '', number: '' }; return <article key={ledger.ledgerId} data-status={ledger.status}><div className="ul891-ledger-heading"><strong>{ledger.billingMonth.slice(0, 7)}</strong><span>{billingStatusLabel(ledger.status)}</span><small>출하 {ledger.currentShipmentEvidence.length}패널 · revision {ledger.revisions.length}건</small></div>{latest ? <dl><div><dt>최신 요청</dt><dd>v{latest.revisionNumber} · {money(latest.amount, billing.currencyCode)}</dd></div><div><dt>출하근거</dt><dd>{latest.panels.map((panel) => panel.displayCode).join(', ') || '회수 전용'}</dd></div><div><dt>회계 확인</dt><dd>{latest.invoiceConfirmedDate ? `${latest.invoiceConfirmedDate} · ${latest.invoiceNumber}` : '대기'}</dd></div></dl> : null}{billing.canMutate && latest && !latest.invoiceConfirmedDate ? <div className="ul891-inline-form"><label>확인일<input type="date" value={form.date} onChange={(event) => setConfirmation((current) => ({ ...current, [ledger.ledgerId]: { ...form, date: event.target.value } }))} /></label><label>세금계산서 번호<input value={form.number} onChange={(event) => setConfirmation((current) => ({ ...current, [ledger.ledgerId]: { ...form, number: event.target.value } }))} /></label><button type="button" disabled={saving} onClick={() => void confirm(ledger)}>회계 발행 확인</button></div> : null}</article>; })}</div>
    {eligibleRecoveries.some((item) => item.status === 'InvoiceConfirmed') ? <div className="ul891-recovery-list"><h5>고객 청구·회수 확인 대기</h5>{eligibleRecoveries.filter((item) => item.status === 'InvoiceConfirmed').map((item) => <button type="button" key={item.recoveryCaseId} disabled={saving} onClick={() => void recover(item)}>{item.procurementItemName} · {item.setInstanceNumber}번 세트 회수 확인</button>)}</div> : null}
  </section>;
}

function DesignComponentSummary({ components }: { components: Ul891SetComponent[] }) {
  if (components.length === 0) {
    return <p className="empty-text">저장된 구성 패널 정보가 없습니다.</p>;
  }
  return (
    <div className="ul891-component-summary" role="table" aria-label="저장된 세트 공통 설계정보">
      <div className="head" role="row">
        <span role="columnheader">Code</span>
        <span role="columnheader">패널명</span>
        <span role="columnheader">W × H × D (mm)</span>
      </div>
      {components.map((component) => (
        <div className="row" role="row" key={component.componentId}>
          <strong role="cell">{component.componentCode}</strong>
          <span role="cell">{component.panelName ?? '미입력'}</span>
          <span role="cell">{formatDimensions(component)}</span>
        </div>
      ))}
    </div>
  );
}

function PanelPills({ panels, onOpenPanel }: { panels: Ul891SetSpec['instances'][number]['panels']; onOpenPanel: (panelId: string) => void }) { return <div className="ul891-panel-pills">{panels.map((panel) => <button type="button" key={panel.panelId} data-status={panel.panelStatus} aria-label={`${panel.componentCode} ${panel.panelName ?? panel.displayCode} 패널 상세`} onClick={() => onOpenPanel(panel.panelId)}><strong>{panel.componentCode}</strong><span>{panel.panelName ?? panel.displayCode}</span><small>{panel.delivered ? '출하' : panel.workflowStage}</small></button>)}</div>; }
function toggle(values: string[], value: string) { return values.includes(value) ? values.filter((item) => item !== value) : [...values, value]; }
function versionLabel(versions: Ul891SetVersion[]) { const draft = versions.find((item) => item.status === 'Draft'); const published = versions.find((item) => item.status === 'Published'); return draft ? `v${draft.versionNumber} 임시저장` : published ? `v${published.versionNumber} 저장 완료` : '저장된 사양 없음'; }
function versionStatusLabel(status: Ul891SetVersion['status']) { return { Draft: '임시저장', Published: '저장 완료', Superseded: '이전 버전' }[status]; }
function formatDimensions(component: Ul891SetComponent) {
  if (component.widthMm === null && component.heightMm === null && component.depthMm === null) return '-';
  return `${component.widthMm ?? '-'} × ${component.heightMm ?? '-'} × ${component.depthMm ?? '-'}`;
}
function updateComponent(items: Ul891SetComponent[], index: number, patch: Partial<Ul891SetComponent>) { return items.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item); }
function blankComponent(index: number): Ul891SetComponent { return { componentId: `new-${index}`, componentCode: '', panelName: null, panelSpecification: null, widthMm: null, heightMm: null, depthMm: null, sortOrder: index + 1 }; }
function clean(value: string | null | undefined) { const normalized = value?.trim(); return normalized ? normalized : null; }
function money(value: number | null, currency: string | null) { return value === null ? '-' : `${new Intl.NumberFormat('ko-KR').format(value)} ${currency ?? ''}`.trim(); }
function billingStatusLabel(value: MonthlyBillingLedger['status']) { return { Open: '작성 중', Requested: '발행요청', InvoiceConfirmed: '회계 확인', AdjustmentRequired: '조정 필요' }[value]; }
function errorMessage(error: unknown) { return error instanceof Error ? error.message : '요청을 처리할 수 없습니다.'; }
