import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ensureProductionControlCurrent,
  getProductionControlTemplateCatalog,
  saveProductionControlManufacturingCurrent,
  saveProductionControlPlanCurrent
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
  const [editing, setEditing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState('');

  const applyCatalog = useCallback((data: ProductionControlTemplateCatalog, preferredVersionId?: string) => {
    setState({ kind: 'ready', data });
    const selectedItem = data.items.find((item) => item.productTypeId === selectedProductTypeId) ?? data.items[0];
    setSelectedProductTypeId(selectedItem?.productTypeId ?? '');
    const versions = domain === 'manufacturing' ? selectedItem?.manufacturingVersions : selectedItem?.planVersions;
    const selectedVersion = versions?.find((version) => version.versionId === preferredVersionId)
      ?? versions?.[0];
    setSelectedVersionId(selectedVersion?.versionId ?? '');
    if (domain === 'manufacturing') {
      setManufacturingRows((selectedVersion?.items as ProductionControlManufacturingItem[] | undefined)?.map((item) => ({ ...item })) ?? []);
      setExpandedPlanIndex(null);
    } else {
      setPlanRows((selectedVersion?.items as ProductionControlPlanItem[] | undefined)?.map(copyPlanItem) ?? []);
      setExpandedPlanIndex(null);
    }
    setEditing(false);
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
  const sources = useMemo(
    () => effectiveSources(catalog?.sources ?? [], selectedItem),
    [catalog?.sources, selectedItem]
  );

  useEffect(() => {
    if (!selectedItem) return;
    const nextVersions = domain === 'manufacturing' ? selectedItem.manufacturingVersions : selectedItem.planVersions;
    const next = nextVersions[0];
    const nextVersionId = next?.versionId ?? '';
    if (selectedVersionId === nextVersionId) return;
    chooseVersion(nextVersionId, next);
  }, [domain, selectedProductTypeId]); // eslint-disable-line react-hooks/exhaustive-deps

  function chooseVersion(versionId: string, explicitVersion = versions.find((version) => version.versionId === versionId)) {
    setSelectedVersionId(versionId);
    if (domain === 'manufacturing') {
      setManufacturingRows((explicitVersion?.items as ProductionControlManufacturingItem[] | undefined)?.map((item) => ({ ...item })) ?? []);
      setExpandedPlanIndex(null);
    } else {
      setPlanRows((explicitVersion?.items as ProductionControlPlanItem[] | undefined)?.map(copyPlanItem) ?? []);
      setExpandedPlanIndex(null);
    }
    setEditing(false);
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

  async function createCurrent() {
    if (!selectedItem) return;
    setBusy(true);
    setFeedback('');
    try {
      const data = await ensureProductionControlCurrent(developmentUserKey, domain, selectedItem.productTypeId, null);
      applyCatalog(data);
      setEditing(true);
      setExpandedPlanIndex(domain === 'planning' ? 0 : null);
      setFeedback('현재 양식을 만들었습니다. 내용을 확인한 뒤 저장해 주세요.');
    } catch (error) {
      setFeedback(error instanceof Error ? error.message : '현재 양식을 만들지 못했습니다.');
    } finally {
      setBusy(false);
    }
  }

  async function saveCurrent() {
    if (!selectedItem || !selectedVersion || !editing) return;
    if (domain === 'manufacturing') {
      const validationMessage = validateManufacturingRows(manufacturingRows);
      if (validationMessage) {
        setFeedback(validationMessage);
        return;
      }
      setBusy(true);
      setFeedback('');
      try {
        const data = await saveProductionControlManufacturingCurrent(
          developmentUserKey,
          selectedItem.productTypeId,
          selectedVersion.versionId,
          selectedVersion.rowVersion,
          resequenceManufacturing(manufacturingRows)
        );
        const savedItem = data.items.find((item) => item.productTypeId === selectedItem.productTypeId);
        const brokenCount = countBrokenManufacturingConnections(savedItem, effectiveSources(data.sources, savedItem));
        applyCatalog(data, selectedVersion.versionId);
        setFeedback(brokenCount > 0
          ? `제조 양식을 저장했습니다. 생산계획에서 연결이 끊긴 항목 ${brokenCount}개를 다시 선택해 주세요. 연결을 수정하기 전에는 새 프로젝트에 적용되지 않습니다.`
          : '제조 양식을 저장했습니다. 이후 생성되는 프로젝트부터 적용됩니다.');
      } catch (error) {
        setFeedback(error instanceof Error ? error.message : '제조 양식을 저장하지 못했습니다.');
      } finally {
        setBusy(false);
      }
    } else {
      const unavailableConnection = planRows
        .flatMap((item) => item.connections)
        .map((connection) => sources.find((source) => source.code === connection.sourceCode))
        .find((source) => source?.isOperational === false);
      if (unavailableConnection) {
        setFeedback(unavailableConnection.operationalMessage ?? `${unavailableConnection.label} 실적 연결은 현재 사용할 수 없습니다.`);
        return;
      }
      await run(
        () => saveProductionControlPlanCurrent(
          developmentUserKey,
          selectedItem.productTypeId,
          selectedVersion.versionId,
          selectedVersion.rowVersion,
          resequencePlan(planRows)
        ),
        '생산계획 양식과 1:1 실적 연결을 저장했습니다. 이후 생성되는 프로젝트부터 적용됩니다.',
        selectedVersion.versionId
      );
    }
  }

  function startEditing() {
    if (!selectedVersion) return;
    chooseVersion(selectedVersion.versionId, selectedVersion);
    setEditing(true);
    setExpandedPlanIndex(domain === 'planning' && selectedVersion.items.length > 0 ? 0 : null);
  }

  function cancelEditing() {
    chooseVersion(selectedVersion?.versionId ?? '', selectedVersion ?? undefined);
    setFeedback('수정 내용을 취소했습니다.');
  }

  const manufacturingOptions = useMemo(
    () => activeManufacturing?.items ?? [],
    [activeManufacturing]
  );
  const brokenManufacturingConnectionCount = useMemo(
    () => countBrokenManufacturingConnections(selectedItem, sources),
    [selectedItem, sources]
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
          <span><strong>현재 양식</strong><small>버전을 추가하지 않고 이 양식 하나를 바로 수정합니다.</small></span>
        </div>
      </div>

      <div className="production-control-version-bar">
        <div className="production-control-current-label">
          <strong>{selectedVersion ? '현재 사용 중인 양식' : '등록된 양식 없음'}</strong>
          <small>{selectedVersion ? '저장하면 새 프로젝트부터 바로 적용됩니다.' : '양식을 만든 뒤 항목을 입력해 주세요.'}</small>
        </div>
        {canManage ? (
          <div className="production-control-version-actions">
            {!selectedVersion ? <button type="button" className="primary-button" disabled={busy} onClick={() => void createCurrent()}>양식 만들기</button> : null}
            {selectedVersion && !editing ? <button type="button" className="secondary-button" disabled={busy} onClick={startEditing}>수정</button> : null}
            {selectedVersion && editing ? <button type="button" disabled={busy} onClick={cancelEditing}>취소</button> : null}
            {selectedVersion && editing ? <button type="button" className="primary-button" disabled={busy} onClick={() => void saveCurrent()}>저장</button> : null}
          </div>
        ) : null}
      </div>

      {feedback ? <p className="production-control-template-feedback" role="status">{feedback}</p> : null}
      {!selectedVersion ? <p className="empty-text">현재 양식이 없습니다. 양식 만들기를 눌러 시작해 주세요.</p> : null}
      {selectedVersion && !editing ? <p className="form-version-lock">현재는 조회 상태입니다. 내용을 바꾸려면 수정 버튼을 눌러 주세요.</p> : null}

      {selectedVersion && domain === 'manufacturing' ? (
        <section className="production-control-editor">
          <header><div><strong>제조 항목</strong><small>등록한 모든 제조 단계를 패널 선택 일괄 완료에 사용할 수 있습니다.</small></div>{editing ? <button type="button" onClick={() => setManufacturingRows((current) => [...current, newManufacturingItem(current.length + 1)])}>항목 추가</button> : null}</header>
          <div className="production-control-row-list" role="table" aria-label="제조 항목 목록">
            <div className="production-control-row-head" role="row">
              <span role="columnheader">No.</span>
              <span role="columnheader">제조 항목</span>
              <span role="columnheader">관리</span>
            </div>
            {manufacturingRows.map((item, index) => (
              <article className="production-control-row" role="row" key={item.definitionKey ?? `new-manufacturing-${index}`}>
                <b role="cell">{index + 1}</b>
                <label role="cell"><span className="sr-only">제조 항목</span><input aria-label={`${index + 1}번 제조 항목`} disabled={!editing} value={item.label} onChange={(event) => setManufacturingRows((current) => replaceAt(current, index, { ...item, label: event.target.value }))} /></label>
                {editing ? <button type="button" onClick={() => setManufacturingRows((current) => resequenceManufacturing(current.filter((_, rowIndex) => rowIndex !== index)))}>삭제</button> : null}
                {!editing ? <span aria-hidden="true">—</span> : null}
              </article>
            ))}
          </div>
        </section>
      ) : null}

      {selectedVersion && domain === 'planning' ? (
        <section className="production-control-editor">
          <header><div><strong>생산계획 항목과 실적 연결</strong><small>계획 항목마다 실적 데이터 하나를 선택하면 해당 실적일이 자동 반영됩니다.</small></div>{editing ? <button type="button" onClick={() => setPlanRows((current) => [...current, newPlanItem(current.length + 1)])}>항목 추가</button> : null}</header>
          {!activeManufacturing ? <p className="production-control-warning">먼저 제조 양식을 저장해야 제조 단계와 LQC를 연결할 수 있습니다.</p> : null}
          {sources.some((source) => source.code === 'LQC_PASSED' && source.isOperational === false) ? (
            <p className="production-control-warning" role="status">
              LQC는 현재 운영 중지 상태입니다. 기존 LQC 연결 이력은 유지되며 새 연결은 제조 단계 완료 등 운영 중인 실적으로 변경해 주세요.
            </p>
          ) : null}
          {brokenManufacturingConnectionCount > 0 ? (
            <p className="production-control-warning" role="alert">
              제조 양식 변경으로 연결이 끊긴 생산계획 항목이 {brokenManufacturingConnectionCount}개 있습니다. 수정에서 현재 제조 단계를 다시 선택해 주세요. 연결을 고치기 전에는 새 프로젝트에 적용되지 않습니다.
            </p>
          ) : null}
          <div className="production-control-plan-list">
            {planRows.map((item, index) => {
              const expanded = expandedPlanIndex === index;
              const staleConnection = staleManufacturingConnection(item.connections[0], sources, manufacturingOptions);
              return (
                <article className="production-control-template-plan-row" data-expanded={expanded || undefined} key={item.definitionKey ?? `new-plan-${index}`}>
                  <button type="button" className="production-control-plan-summary" aria-expanded={expanded} onClick={() => setExpandedPlanIndex(expanded ? null : index)}>
                    <b>{index + 1}</b>
                    <span><strong>{item.label || '항목명 없음'}</strong><small>{item.isRequired ? '필수 계획 항목' : '선택 계획 항목'}</small></span>
                    <span><strong>연결 실적</strong><small>{connectionSummary(item, sources, manufacturingOptions)}</small></span>
                    <i>{expanded ? '접기' : editing ? '편집' : '보기'}</i>
                  </button>
                  {expanded ? (
                    <div className="production-control-plan-editor">
                      <section className="production-control-plan-basics" aria-label={`${item.label} 기본 정보`}>
                        <header><strong>계획 항목 정보</strong><small>일정표에 표시할 이름과 필수 여부입니다.</small></header>
                        <label>계획 항목명<input aria-label={`${index + 1}번 계획 항목명`} disabled={!editing} value={item.label} onChange={(event) => setPlanRows((current) => replaceAt(current, index, { ...item, label: event.target.value }))} /></label>
                        <label className="checkbox-row"><input type="checkbox" disabled={!editing} checked={item.isRequired} onChange={(event) => setPlanRows((current) => replaceAt(current, index, { ...item, isRequired: event.target.checked }))} />필수 계획 항목</label>
                        {editing ? <button type="button" className="production-control-delete" onClick={() => { setPlanRows((current) => resequencePlan(current.filter((_, rowIndex) => rowIndex !== index))); setExpandedPlanIndex(null); }}>항목 삭제</button> : null}
                      </section>
                      <section className="production-control-plan-connections" aria-label={`${item.label} 실적 데이터 연결`}>
                        <header><strong>실적 데이터 1:1 연결</strong><small>이 계획 항목의 실적일로 사용할 데이터 하나를 고릅니다.</small></header>
                        <label className="production-control-connection-select">
                          <span>연결할 실적</span>
                          <select
                            aria-label={`${index + 1}번 연결할 실적`}
                            disabled={!editing}
                            value={connectionValue(item.connections[0])}
                            onChange={(event) => setPlanRows((current) => replaceAt(current, index, {
                              ...item,
                              connections: event.target.value ? [parseConnectionValue(event.target.value)] : []
                            }))}
                          >
                            <option value="">실적 데이터를 선택해 주세요</option>
                            {staleConnection ? (
                              <option value={connectionValue(staleConnection)}>
                                연결 재설정 필요 · 삭제되거나 교체된 제조 단계
                              </option>
                            ) : null}
                            {sources.map((source) => (source.definitionKind ?? (source.requiresManufacturingDefinition ? 'Manufacturing' : 'None')) !== 'None' ? (
                              <optgroup key={source.code} disabled={source.isOperational === false} label={`${source.departmentLabel} · ${source.label}${source.isOperational === false ? ' · 운영 중지' : ''}`}>
                                {sourceDefinitionOptions(source, manufacturingOptions).map((step) => step.definitionKey ? (
                                  <option disabled={source.isOperational === false} key={`${source.code}:${step.definitionKey}`} value={connectionValue({ sourceCode: source.code, sourceDefinitionKey: step.definitionKey })}>
                                    {step.label}
                                  </option>
                                ) : null)}
                              </optgroup>
                            ) : (
                              <option disabled={source.isOperational === false} key={source.code} value={connectionValue({ sourceCode: source.code, sourceDefinitionKey: null })}>
                                {source.departmentLabel} · {source.label}{source.isOperational === false ? ' · 운영 중지' : ''}
                              </option>
                            ))}
                          </select>
                        </label>
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
        <span>저장 이후 새로 만드는 프로젝트만 현재 양식을 복제합니다. 기존 프로젝트는 생성 당시 양식을 계속 사용합니다.</span>
      </footer>
    </section>
  );
}

function copyPlanItem(item: ProductionControlPlanItem): ProductionControlPlanItem {
  const preferred = preferredConnection(item.connections);
  return { ...item, connections: preferred ? [{ ...preferred }] : [] };
}

function replaceAt<T>(items: T[], index: number, value: T) {
  return items.map((item, itemIndex) => itemIndex === index ? value : item);
}

function newManufacturingItem(displayOrder: number): ProductionControlManufacturingItem {
  return { definitionKey: null, displayOrder, label: '새 제조 항목' };
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

function connectionSummary(
  item: ProductionControlPlanItem,
  sources: ProductionControlTemplateCatalog['sources'],
  manufacturingItems: ProductionControlManufacturingItem[]
) {
  const connection = item.connections[0];
  if (!connection) return '연결된 실적 없음';
  const source = sources.find((candidate) => candidate.code === connection.sourceCode);
  const manufacturing = connection.sourceDefinitionKey
    ? manufacturingItems.find((candidate) => candidate.definitionKey === connection.sourceDefinitionKey)
    : null;
  const quality = connection.sourceDefinitionKey
    ? source?.definitions?.find((candidate) => candidate.definitionKey === connection.sourceDefinitionKey)
    : null;
  if (!source) return connection.sourceCode;
  if (staleManufacturingConnection(connection, sources, manufacturingItems)) {
    return `${source.departmentLabel} · ${source.label} · 연결 재설정 필요`;
  }
  const summary = manufacturing || quality
    ? `${source.departmentLabel} · ${source.label} · ${(manufacturing ?? quality)?.label}`
    : `${source.departmentLabel} · ${source.label}`;
  return source.isOperational === false ? `${summary} · 운영 중지` : summary;
}

function sourceDefinitionOptions(
  source: ProductionControlTemplateCatalog['sources'][number],
  manufacturingItems: ProductionControlManufacturingItem[]
) {
  return (source.definitionKind ?? (source.requiresManufacturingDefinition ? 'Manufacturing' : 'None')) === 'Manufacturing'
    ? manufacturingItems
    : source.definitions ?? [];
}

function connectionValue(connection: ProductionControlConnection | undefined) {
  if (!connection) return '';
  return `${connection.sourceCode}:${connection.sourceDefinitionKey ?? ''}`;
}

function parseConnectionValue(value: string): ProductionControlConnection {
  const [sourceCode, sourceDefinitionKey] = value.split(':', 2);
  return { sourceCode, sourceDefinitionKey: sourceDefinitionKey || null };
}

function preferredConnection(connections: ProductionControlConnection[]) {
  const priority = [
    'DELIVERED',
    'DEPARTED',
    'PACKED',
    'FAT_PASSED',
    'CUSTOMER_INSPECTION_PASSED',
    'OQC_PASSED',
    'LQC_PASSED',
    'MANUFACTURING_STEP_COMPLETED',
    'IQC_PASSED',
    'MATERIAL_RECEIPT_CONFIRMED',
    'PURCHASE_ORDERED'
  ];
  return [...connections].sort((left, right) => priority.indexOf(left.sourceCode) - priority.indexOf(right.sourceCode))[0];
}

function validateManufacturingRows(items: ProductionControlManufacturingItem[]) {
  if (items.length < 1 || items.length > 50) {
    return '제조 항목은 1개부터 50개까지 등록해 주세요.';
  }
  const blankIndex = items.findIndex((item) => item.label.trim().length === 0);
  if (blankIndex >= 0) {
    return `${blankIndex + 1}번 제조 항목명을 입력해 주세요.`;
  }
  const longIndex = items.findIndex((item) => item.label.trim().length > 100);
  if (longIndex >= 0) {
    return `${longIndex + 1}번 제조 항목명은 100자 이내로 입력해 주세요.`;
  }
  return null;
}

function countBrokenManufacturingConnections(
  item: ProductionControlTemplateCatalog['items'][number] | null | undefined,
  sources: ProductionControlTemplateCatalog['sources']
) {
  if (!item) return 0;
  const manufacturingItems = item.manufacturingVersions.find((version) => version.lifecycleStatus === 'Active')?.items ?? [];
  const planItems = item.planVersions.find((version) => version.lifecycleStatus === 'Active')?.items ?? [];
  return planItems.filter((planItem) => staleManufacturingConnection(planItem.connections[0], sources, manufacturingItems)).length;
}

function staleManufacturingConnection(
  connection: ProductionControlConnection | undefined,
  sources: ProductionControlTemplateCatalog['sources'],
  manufacturingItems: ProductionControlManufacturingItem[]
) {
  if (!connection?.sourceDefinitionKey) return null;
  const source = sources.find((candidate) => candidate.code === connection.sourceCode);
  const definitionKind = source?.definitionKind ?? (source?.requiresManufacturingDefinition ? 'Manufacturing' : 'None');
  if (definitionKind !== 'Manufacturing') return null;
  return manufacturingItems.some((item) => item.definitionKey === connection.sourceDefinitionKey)
    ? null
    : connection;
}

function effectiveSources(
  sources: ProductionControlTemplateCatalog['sources'],
  item: ProductionControlTemplateCatalog['items'][number] | null | undefined
): ProductionControlTemplateCatalog['sources'] {
  return sources.map((source) => source.code === 'LQC_PASSED'
    ? {
        ...source,
        isOperational: item?.lqcOperational !== false,
        operationalMessage: item?.lqcOperational === false
          ? `${item.productTypeName} Item의 LQC는 운영 중지 상태입니다. 기존 연결 이력은 유지되며 새 연결에는 사용할 수 없습니다.`
          : null
      }
    : source);
}
