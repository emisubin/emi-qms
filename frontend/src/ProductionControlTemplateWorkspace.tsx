import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  createProductionControlDraft,
  getProductionControlTemplateCatalog,
  saveProductionControlManufacturingDraft,
  saveProductionControlPlanDraft,
  transitionProductionControlVersion
} from './api';
import type {
  ProductionControlConnection,
  ProductionControlManufacturingItem,
  ProductionControlPlanItem,
  ProductionControlTemplateCatalog
} from './productionControlTemplates';

type Domain = 'manufacturing' | 'planning';
type State = { kind: 'loading' } | { kind: 'ready'; data: ProductionControlTemplateCatalog } | { kind: 'error'; message: string };

export function ProductionControlTemplateWorkspace({ developmentUserKey, domain }: { developmentUserKey: string | undefined; domain: Domain }) {
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [selectedProductTypeId, setSelectedProductTypeId] = useState('');
  const [selectedVersionId, setSelectedVersionId] = useState('');
  const [manufacturingRows, setManufacturingRows] = useState<ProductionControlManufacturingItem[]>([]);
  const [planRows, setPlanRows] = useState<ProductionControlPlanItem[]>([]);
  const [expandedPlanIndex, setExpandedPlanIndex] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState('');

  const applyCatalog = useCallback((data: ProductionControlTemplateCatalog, preferredVersionId?: string) => {
    setState({ kind: 'ready', data });
    const selectedItem = data.items.find((item) => item.productTypeId === selectedProductTypeId) ?? data.items[0];
    setSelectedProductTypeId(selectedItem?.productTypeId ?? '');
    const versions = domain === 'manufacturing' ? selectedItem?.manufacturingVersions : selectedItem?.planVersions;
    const selectedVersion = versions?.find((version) => version.versionId === preferredVersionId)
      ?? versions?.find((version) => version.lifecycleStatus === 'Draft')
      ?? versions?.find((version) => version.lifecycleStatus === 'Active')
      ?? versions?.[0];
    setSelectedVersionId(selectedVersion?.versionId ?? '');
    if (domain === 'manufacturing') {
      setManufacturingRows((selectedVersion?.items as ProductionControlManufacturingItem[] | undefined)?.map((item) => ({ ...item })) ?? []);
      setExpandedPlanIndex(null);
    } else {
      setPlanRows((selectedVersion?.items as ProductionControlPlanItem[] | undefined)?.map(copyPlanItem) ?? []);
      setExpandedPlanIndex(selectedVersion?.lifecycleStatus === 'Draft' ? 0 : null);
    }
  }, [domain, selectedProductTypeId]);

  const load = useCallback(async () => {
    setState({ kind: 'loading' });
    try {
      applyCatalog(await getProductionControlTemplateCatalog(developmentUserKey));
    } catch (error) {
      setState({ kind: 'error', message: error instanceof Error ? error.message : '생산계획 양식을 불러오지 못했습니다.' });
    }
  }, [applyCatalog, developmentUserKey]);

  useEffect(() => { queueMicrotask(() => void load()); }, [developmentUserKey]); // eslint-disable-line react-hooks/exhaustive-deps

  const catalog = state.kind === 'ready' ? state.data : null;
  const selectedItem = catalog?.items.find((item) => item.productTypeId === selectedProductTypeId) ?? null;
  const versions = domain === 'manufacturing' ? selectedItem?.manufacturingVersions ?? [] : selectedItem?.planVersions ?? [];
  const selectedVersion = versions.find((version) => version.versionId === selectedVersionId) ?? null;
  const activeManufacturing = selectedItem?.manufacturingVersions.find((version) => version.lifecycleStatus === 'Active');
  const canManage = domain === 'manufacturing' ? catalog?.canManageManufacturing : catalog?.canManageProductionPlanning;
  const isDraft = selectedVersion?.lifecycleStatus === 'Draft';

  useEffect(() => {
    if (!selectedItem) return;
    const nextVersions = domain === 'manufacturing' ? selectedItem.manufacturingVersions : selectedItem.planVersions;
    const next = nextVersions.find((version) => version.lifecycleStatus === 'Draft')
      ?? nextVersions.find((version) => version.lifecycleStatus === 'Active')
      ?? nextVersions[0];
    chooseVersion(next?.versionId ?? '', next);
  }, [domain, selectedProductTypeId]); // eslint-disable-line react-hooks/exhaustive-deps

  function chooseVersion(versionId: string, explicitVersion = versions.find((version) => version.versionId === versionId)) {
    setSelectedVersionId(versionId);
    if (domain === 'manufacturing') {
      setManufacturingRows((explicitVersion?.items as ProductionControlManufacturingItem[] | undefined)?.map((item) => ({ ...item })) ?? []);
      setExpandedPlanIndex(null);
    } else {
      setPlanRows((explicitVersion?.items as ProductionControlPlanItem[] | undefined)?.map(copyPlanItem) ?? []);
      setExpandedPlanIndex(explicitVersion?.lifecycleStatus === 'Draft' ? 0 : null);
    }
    setFeedback('');
  }

  async function run(action: () => Promise<ProductionControlTemplateCatalog>, success: string, preferredVersionId?: string) {
    setBusy(true);
    setFeedback('');
    try {
      applyCatalog(await action(), preferredVersionId);
      setFeedback(success);
    } catch (error) {
      setFeedback(error instanceof Error ? error.message : '요청을 완료하지 못했습니다.');
    } finally {
      setBusy(false);
    }
  }

  async function createDraft() {
    if (!selectedItem) return;
    const active = versions.find((version) => version.lifecycleStatus === 'Active');
    await run(
      () => createProductionControlDraft(developmentUserKey, domain, selectedItem.productTypeId, active?.rowVersion ?? null),
      '사용 중인 버전을 복제해 초안을 만들었습니다.'
    );
  }

  async function saveDraft() {
    if (!selectedItem || !selectedVersion || !isDraft) return;
    if (domain === 'manufacturing') {
      await run(
        () => saveProductionControlManufacturingDraft(
          developmentUserKey,
          selectedItem.productTypeId,
          selectedVersion.versionId,
          selectedVersion.rowVersion,
          resequenceManufacturing(manufacturingRows)
        ),
        '제조 양식 초안을 저장했습니다.',
        selectedVersion.versionId
      );
    } else {
      await run(
        () => saveProductionControlPlanDraft(
          developmentUserKey,
          selectedItem.productTypeId,
          selectedVersion.versionId,
          selectedVersion.rowVersion,
          resequencePlan(planRows)
        ),
        '생산계획 양식과 실적 연결을 저장했습니다.',
        selectedVersion.versionId
      );
    }
  }

  async function transition(action: 'activate' | 'archive') {
    if (!selectedItem || !selectedVersion || !isDraft) return;
    if (action === 'activate' && !window.confirm('이 버전을 사용하도록 활성화할까요? 이후 새 프로젝트부터 적용됩니다.')) return;
    await run(
      () => transitionProductionControlVersion(
        developmentUserKey,
        domain,
        selectedItem.productTypeId,
        selectedVersion.versionId,
        selectedVersion.rowVersion,
        action
      ),
      action === 'activate' ? '새 버전을 활성화했습니다. 이후 생성되는 프로젝트부터 적용됩니다.' : '초안을 보관했습니다.'
    );
  }

  const manufacturingOptions = useMemo(
    () => activeManufacturing?.items ?? [],
    [activeManufacturing]
  );

  if (state.kind === 'loading') return <section className="production-control-template-loading"><p role="status">연결형 생산계획 양식을 준비하는 중입니다.</p></section>;
  if (state.kind === 'error') return <section className="production-control-template-loading"><p role="alert">{state.message}</p><button type="button" onClick={() => void load()}>다시 시도</button></section>;
  if (!catalog) return null;

  return (
    <section className="production-control-template-workspace" aria-label={domain === 'manufacturing' ? 'Item별 제조 양식' : '생산계획 연결 양식'}>
      <header className="production-control-template-intro">
        <div>
          <p className="eyebrow">{domain === 'manufacturing' ? '제조 양식' : '생산계획 양식'}</p>
          <h3>{domain === 'manufacturing' ? 'Item별 제조 항목' : '생산계획·실적 연결'}</h3>
          <p>{domain === 'manufacturing'
            ? 'Item을 고른 뒤 제조 단계의 이름과 구분을 관리합니다.'
            : 'Item을 고른 뒤 계획 항목을 실제 부서 실적과 연결합니다.'}</p>
        </div>
        <span className="form-manager-badge">{canManage ? '편집 가능' : '조회 전용'}</span>
      </header>

      <div className="production-control-template-toolbar">
        <div className="production-control-template-step">
          <b>1</b>
          <label>적용 Item
          <select value={selectedProductTypeId} onChange={(event) => setSelectedProductTypeId(event.target.value)}>
            {catalog.items.map((item) => <option key={item.productTypeId} value={item.productTypeId}>{item.productTypeCode} · {item.productTypeName}</option>)}
          </select>
          </label>
        </div>
        <div className="production-control-template-step">
          <b>2</b>
          <span><strong>버전 선택</strong><small>사용 중 버전은 보존하고 편집 시 초안으로 복제합니다.</small></span>
        </div>
      </div>

      <div className="production-control-version-bar">
        <div className="production-control-version-list">
          {versions.map((version) => (
            <button type="button" key={version.versionId} className={version.versionId === selectedVersionId ? 'is-active' : ''} onClick={() => chooseVersion(version.versionId)}>
              v{version.versionNumber} · {versionStatusLabel(version.lifecycleStatus)}
            </button>
          ))}
        </div>
        {canManage ? (
          <div className="production-control-version-actions">
            {!isDraft ? <button type="button" className="secondary-button" disabled={busy} onClick={() => void createDraft()}>편집</button> : null}
            {isDraft ? <button type="button" className="primary-button" disabled={busy} onClick={() => void saveDraft()}>저장</button> : null}
            {isDraft ? <button type="button" disabled={busy} onClick={() => void transition('archive')}>초안 보관</button> : null}
            {isDraft ? <button type="button" className="primary-button" disabled={busy} onClick={() => void transition('activate')}>활성화</button> : null}
          </div>
        ) : null}
      </div>

      {feedback ? <p className="production-control-template-feedback" role="status">{feedback}</p> : null}
      {!selectedVersion ? <p className="empty-text">버전이 없습니다. 편집용 초안을 만들어 주세요.</p> : null}
      {selectedVersion && !isDraft ? <p className="form-version-lock">사용 중이거나 보관된 버전은 조회만 가능합니다. 편집용 초안을 만들면 안전하게 복제됩니다.</p> : null}

      {selectedVersion && domain === 'manufacturing' ? (
        <section className="production-control-editor">
          <header><div><strong>제조 항목</strong><small>조립 단계는 일괄 단계 완료 기능과 연결됩니다.</small></div>{isDraft ? <button type="button" onClick={() => setManufacturingRows((current) => [...current, newManufacturingItem(current.length + 1)])}>항목 추가</button> : null}</header>
          <div className="production-control-row-list" role="table" aria-label="제조 항목 목록">
            <div className="production-control-row-head" role="row">
              <span role="columnheader">No.</span>
              <span role="columnheader">제조 항목</span>
              <span role="columnheader">구분</span>
              <span role="columnheader">관리</span>
            </div>
            {manufacturingRows.map((item, index) => (
              <article className="production-control-row" role="row" key={item.definitionKey ?? `new-manufacturing-${index}`}>
                <b role="cell">{index + 1}</b>
                <label role="cell"><span className="sr-only">제조 항목</span><input aria-label={`${index + 1}번 제조 항목`} disabled={!isDraft} value={item.label} onChange={(event) => setManufacturingRows((current) => replaceAt(current, index, { ...item, label: event.target.value }))} /></label>
                <label role="cell"><span className="sr-only">구분</span><select aria-label={`${index + 1}번 구분`} disabled={!isDraft} value={item.stepRole} onChange={(event) => setManufacturingRows((current) => replaceAt(current, index, { ...item, stepRole: event.target.value as 'General' | 'Assembly' }))}><option value="General">일반</option><option value="Assembly">조립</option></select></label>
                {isDraft ? <button type="button" onClick={() => setManufacturingRows((current) => resequenceManufacturing(current.filter((_, rowIndex) => rowIndex !== index)))}>삭제</button> : null}
                {!isDraft ? <span aria-hidden="true">—</span> : null}
              </article>
            ))}
          </div>
        </section>
      ) : null}

      {selectedVersion && domain === 'planning' ? (
        <section className="production-control-editor">
          <header><div><strong>생산계획 항목과 실적 연결</strong><small>한 계획 항목에 여러 부서 실적을 연결할 수 있으며 모두 완료되면 실적일이 확정됩니다.</small></div>{isDraft ? <button type="button" onClick={() => setPlanRows((current) => [...current, newPlanItem(current.length + 1)])}>항목 추가</button> : null}</header>
          {!activeManufacturing ? <p className="production-control-warning">먼저 제조 양식을 활성화해야 제조 단계와 LQC를 연결할 수 있습니다.</p> : null}
          <div className="production-control-plan-list">
            {planRows.map((item, index) => {
              const expanded = expandedPlanIndex === index;
              return (
                <article className="production-control-template-plan-row" data-expanded={expanded || undefined} key={item.definitionKey ?? `new-plan-${index}`}>
                  <button type="button" className="production-control-plan-summary" aria-expanded={expanded} onClick={() => setExpandedPlanIndex(expanded ? null : index)}>
                    <b>{index + 1}</b>
                    <span><strong>{item.label || '항목명 없음'}</strong><small>{item.isRequired ? '필수 계획 항목' : '선택 계획 항목'}</small></span>
                    <span><strong>실적 연결 {item.connections.length}개</strong><small>{connectionSummary(item, catalog.sources)}</small></span>
                    <i>{expanded ? '접기' : isDraft ? '편집' : '보기'}</i>
                  </button>
                  {expanded ? (
                    <div className="production-control-plan-editor">
                      <section className="production-control-plan-basics" aria-label={`${item.label} 기본 정보`}>
                        <header><strong>계획 항목 정보</strong><small>일정표에 표시할 이름과 필수 여부입니다.</small></header>
                        <label>계획 항목명<input aria-label={`${index + 1}번 계획 항목명`} disabled={!isDraft} value={item.label} onChange={(event) => setPlanRows((current) => replaceAt(current, index, { ...item, label: event.target.value }))} /></label>
                        <label className="checkbox-row"><input type="checkbox" disabled={!isDraft} checked={item.isRequired} onChange={(event) => setPlanRows((current) => replaceAt(current, index, { ...item, isRequired: event.target.checked }))} />필수 계획 항목</label>
                        {isDraft ? <button type="button" className="production-control-delete" onClick={() => { setPlanRows((current) => resequencePlan(current.filter((_, rowIndex) => rowIndex !== index))); setExpandedPlanIndex(null); }}>항목 삭제</button> : null}
                      </section>
                      <section className="production-control-plan-connections" aria-label={`${item.label} 실적 데이터 연결`}>
                        <header><strong>실적 데이터 연결</strong><small>선택한 실적이 모두 완료되면 이 계획 항목의 실적일이 자동 확정됩니다.</small></header>
                        <div className="production-control-connections">
                          {catalog.sources.map((source) => source.requiresManufacturingDefinition ? (
                            <fieldset key={source.code}>
                              <legend><strong>{source.departmentLabel}</strong><span>{source.label}</span></legend>
                              <div>
                                {manufacturingOptions.map((step) => {
                                  if (!step.definitionKey) return null;
                                  const checked = hasConnection(item.connections, source.code, step.definitionKey);
                                  return <label key={step.definitionKey}><input type="checkbox" disabled={!isDraft} checked={checked} onChange={(event) => setPlanRows((current) => replaceAt(current, index, { ...item, connections: toggleConnection(item.connections, source.code, step.definitionKey, event.target.checked) }))} />{step.label}</label>;
                                })}
                              </div>
                            </fieldset>
                          ) : (
                            <label className="production-control-source" key={source.code}>
                              <input type="checkbox" disabled={!isDraft} checked={hasConnection(item.connections, source.code, null)} onChange={(event) => setPlanRows((current) => replaceAt(current, index, { ...item, connections: toggleConnection(item.connections, source.code, null, event.target.checked) }))} />
                              <span><strong>{source.departmentLabel}</strong>{source.label}</span>
                            </label>
                          ))}
                        </div>
                      </section>
                    </div>
                  ) : null}
                </article>
              );
            })}
          </div>
        </section>
      ) : null}

      <footer className="production-control-snapshot-note">
        <strong>적용 원칙</strong>
        <span>활성화 이후 새로 만드는 프로젝트만 이 버전을 복제합니다. 기존 프로젝트는 생성 당시 양식을 계속 사용합니다.</span>
      </footer>
    </section>
  );
}

function versionStatusLabel(status: 'Draft' | 'Active' | 'Archived') {
  return status === 'Draft' ? '초안' : status === 'Active' ? '사용 중' : '보관';
}

function copyPlanItem(item: ProductionControlPlanItem): ProductionControlPlanItem {
  return { ...item, connections: item.connections.map((connection) => ({ ...connection })) };
}

function replaceAt<T>(items: T[], index: number, value: T) {
  return items.map((item, itemIndex) => itemIndex === index ? value : item);
}

function newManufacturingItem(displayOrder: number): ProductionControlManufacturingItem {
  return { definitionKey: null, displayOrder, label: '새 제조 항목', stepRole: 'General' };
}

function newPlanItem(displayOrder: number): ProductionControlPlanItem {
  return { definitionKey: null, displayOrder, label: '새 생산계획 항목', isRequired: true, connections: [] };
}

function resequenceManufacturing(items: ProductionControlManufacturingItem[]) {
  return items.map((item, index) => ({ ...item, displayOrder: index + 1 }));
}

function resequencePlan(items: ProductionControlPlanItem[]) {
  return items.map((item, index) => ({ ...item, displayOrder: index + 1 }));
}

function connectionSummary(item: ProductionControlPlanItem, sources: Array<{ code: string; departmentLabel: string }>) {
  const departments = [...new Set(item.connections
    .map((connection) => sources.find((source) => source.code === connection.sourceCode)?.departmentLabel)
    .filter((department): department is string => Boolean(department)))];
  return departments.length > 0 ? departments.join(' · ') : '연결된 실적 없음';
}

function hasConnection(connections: ProductionControlConnection[], sourceCode: string, definitionKey: string | null) {
  return connections.some((connection) => connection.sourceCode === sourceCode && connection.sourceDefinitionKey === definitionKey);
}

function toggleConnection(connections: ProductionControlConnection[], sourceCode: string, sourceDefinitionKey: string | null, checked: boolean) {
  if (checked) {
    return hasConnection(connections, sourceCode, sourceDefinitionKey)
      ? connections
      : [...connections, { sourceCode, sourceDefinitionKey }];
  }
  return connections.filter((connection) => connection.sourceCode !== sourceCode || connection.sourceDefinitionKey !== sourceDefinitionKey);
}
