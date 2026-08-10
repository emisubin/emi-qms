import { useCallback, useEffect, useState } from 'react';
import {
  assignFormTemplateManager,
  createMaterialCategory,
  getFormTemplateCatalog,
  getCurrentFormTemplate,
  getFormTemplateManagers,
  getLqcItemTemplates,
  getMaterialCategories,
  getMaterialCategoryIqcTemplates,
  revokeFormTemplateManager,
  saveCurrentFormTemplate,
  saveLqcItemTemplate,
  saveMaterialCategoryIqcTemplate,
  updateLqcItemOperatingStatus,
  updateMaterialCategory,
  updateMaterialCategoryIqcSetting
} from './api';
import { useAdaptiveLayout } from './adaptive-layout';
import type { FormTemplateCatalogItem, FormTemplateItem, FormTemplateManagers, FormTemplateVersions, LqcItemTemplates, MaterialCategory, MaterialCategoryCatalog, MaterialCategoryIqcTemplates } from './formTemplates';
import { useActionFeedback } from './useActionFeedback';
import { ProductionControlTemplateWorkspace } from './ProductionControlTemplateWorkspace';

type LoadState = { kind: 'loading' } | { kind: 'ready'; items: FormTemplateCatalogItem[] } | { kind: 'error'; message: string };

export function FormTemplateManagementPage({ developmentUserKey, isSystemAdministrator }: { developmentUserKey: string | undefined; isSystemAdministrator: boolean }) {
  const { isMobile } = useAdaptiveLayout();
  const [state, setState] = useState<LoadState>({ kind: 'loading' });
  const [selectedKey, setSelectedKey] = useState('');
  const [versions, setVersions] = useState<FormTemplateVersions | null>(null);
  const [selectedVersionId, setSelectedVersionId] = useState('');
  const [draftItems, setDraftItems] = useState<FormTemplateItem[]>([]);
  const [editing, setEditing] = useState(false);
  const [managerPanelOpen, setManagerPanelOpen] = useState(false);
  const [managers, setManagers] = useState<FormTemplateManagers | null>(null);
  const [candidateUserId, setCandidateUserId] = useState('');
  const [workspaceMode, setWorkspaceMode] = useState<'inspection' | 'production-manufacturing' | 'production-planning' | 'material-category-iqc' | 'material-categories'>('inspection');
  const actions = useActionFeedback();

  const selectedTemplate = state.kind === 'ready' ? state.items.find((item) => `${item.family}:${item.templateKey}` === selectedKey) ?? null : null;
  const selectedVersion = versions?.versions.find((version) => version.versionId === selectedVersionId) ?? null;

  const loadVersions = useCallback(async (template: FormTemplateCatalogItem, preferredId?: string) => {
    const data = await getCurrentFormTemplate(developmentUserKey, template.family, template.templateKey);
    setVersions(data);
    const next = data.versions.find((version) => version.versionId === preferredId)
      ?? data.versions.find((version) => version.lifecycleStatus === 'Active')
      ?? data.versions[0];
    setSelectedVersionId(next?.versionId ?? '');
    setDraftItems(next?.items.map(copyItem) ?? []);
    setEditing(false);
    return true;
  }, [developmentUserKey]);

  const load = useCallback(async () => {
    setState({ kind: 'loading' });
    try {
      const catalog = await getFormTemplateCatalog(developmentUserKey);
      setState({ kind: 'ready', items: catalog.templates });
      const first = catalog.templates.find((item) => `${item.family}:${item.templateKey}` === selectedKey) ?? catalog.templates[0];
      if (first) {
        setSelectedKey(`${first.family}:${first.templateKey}`);
        if (!(first.family === 'PanelQualityStage' && first.templateKey === 'LQC')) {
          await loadVersions(first);
        }
      }
    } catch (error) {
      try {
        await getMaterialCategories(developmentUserKey, true);
        setState({ kind: 'ready', items: [] });
        setWorkspaceMode('material-categories');
      } catch {
        setState({ kind: 'error', message: error instanceof Error ? error.message : '양식 목록을 불러오지 못했습니다.' });
      }
    }
  }, [developmentUserKey, loadVersions, selectedKey]);

  useEffect(() => { queueMicrotask(() => void load()); }, [developmentUserKey]); // eslint-disable-line react-hooks/exhaustive-deps

  async function chooseTemplate(template: FormTemplateCatalogItem) {
    setWorkspaceMode('inspection');
    setSelectedKey(`${template.family}:${template.templateKey}`);
    actions.reset();
    if (template.family === 'PanelQualityStage' && template.templateKey === 'LQC') {
      setVersions(null);
      setSelectedVersionId('');
      setDraftItems([]);
      setEditing(false);
      return;
    }
    await loadVersions(template);
  }

  function beginEditing() {
    if (!selectedVersion) return;
    setDraftItems(selectedVersion.items.map(copyItem));
    setEditing(true);
    actions.setFeedback('template:edit', { tone: 'neutral', message: '현재 양식을 수정하고 있습니다.' });
  }

  async function saveCurrent() {
    if (!selectedTemplate || !selectedVersion || !editing) return;
    await actions.run('template:save', async () => {
      const result = await saveCurrentFormTemplate(developmentUserKey, selectedTemplate.family, selectedTemplate.templateKey, selectedVersion.rowVersion, resequence(draftItems));
      setVersions(result);
      const updated = result.versions.find((version) => version.lifecycleStatus === 'Active') ?? result.versions[0];
      setSelectedVersionId(updated?.versionId ?? '');
      setDraftItems(updated?.items.map(copyItem) ?? []);
      setEditing(false);
    }, { loadingMessage: '현재 양식을 저장하는 중입니다.', successMessage: '현재 양식을 저장했습니다. 이후 시작되는 새 검사부터 적용됩니다.', errorFallback: '현재 양식을 저장하지 못했습니다.' });
  }

  function cancelEditing() {
    setDraftItems(selectedVersion?.items.map(copyItem) ?? []);
    setEditing(false);
    actions.setFeedback('template:cancel', { tone: 'neutral', message: '수정 내용을 취소했습니다.' });
  }

  async function openManagers() {
    setManagerPanelOpen(true);
    if (!managers) setManagers(await getFormTemplateManagers(developmentUserKey));
  }

  async function assignManager() {
    const candidate = managers?.candidates.find((item) => item.userId === candidateUserId);
    if (!candidate) return;
    const domain = candidate.departmentCode === 'quality'
      ? 'Quality'
      : candidate.departmentCode === 'production-planning'
        ? 'ProductionPlanning'
        : 'Manufacturing';
    await actions.run('manager:assign', async () => {
      setManagers(await assignFormTemplateManager(developmentUserKey, candidate.userId, domain));
      setCandidateUserId('');
    }, { loadingMessage: '부서 양식 관리자를 지정하는 중입니다.', successMessage: '부서 양식 관리자를 지정했습니다.', errorFallback: '관리자를 지정하지 못했습니다.' });
  }

  async function revokeManager(bindingId: string) {
    await actions.run(`manager:revoke:${bindingId}`, async () => setManagers(await revokeFormTemplateManager(developmentUserKey, bindingId)), {
      loadingMessage: '관리자 지정을 해제하는 중입니다.', successMessage: '관리자 지정을 해제했습니다.', errorFallback: '관리자 지정을 해제하지 못했습니다.'
    });
  }

  const latestFeedback = actions.latestFeedback;
  const templateActionBusy = actions.hasBusyPrefix('template:');

  if (state.kind === 'loading') return <section className="page-surface form-template-page"><p role="status">양식 관리 화면을 준비하는 중입니다.</p></section>;
  if (state.kind === 'error') return <section className="page-surface form-template-page"><p role="alert">{state.message}</p><button type="button" onClick={() => void load()}>다시 시도</button></section>;

  return (
    <section className="page-surface form-template-page" data-mobile-experience={isMobile || undefined} aria-labelledby="form-template-title">
      <header className="form-template-header">
        <div><p className="eyebrow">코드 수정 없는 양식 관리</p><h2 id="form-template-title">양식 관리</h2><p>양식 종류를 선택해 현재 적용 내용과 항목을 관리합니다.</p></div>
        {isSystemAdministrator ? <button type="button" className="secondary-button" onClick={() => void openManagers()}>부서장 지정</button> : <span className="form-manager-badge">부서 양식 관리자</span>}
      </header>

      {latestFeedback ? <p className="form-template-feedback" data-tone={latestFeedback.tone} role={latestFeedback.tone === 'error' ? 'alert' : 'status'}>{latestFeedback.message}</p> : null}

      <div className={`form-template-workspace${workspaceMode === 'inspection' ? '' : ' has-production-control'}`}>
        <nav className="form-template-catalog" aria-label="양식 종류">
          <header><strong>양식 종류</strong><small>{state.items.length + (state.items.length > 0 ? 4 : 1)}개</small></header>
          {state.items.map((template) => <button key={`${template.family}:${template.templateKey}`} type="button" className={workspaceMode === 'inspection' && selectedKey === `${template.family}:${template.templateKey}` ? 'is-active' : ''} onClick={() => void chooseTemplate(template)}><span><b>{template.displayName}</b><small>{template.family === 'PanelQualityStage' && template.templateKey === 'LQC' ? '품질 · Item별 운영 상태·검사 항목' : template.family === 'IqcReport' ? '품질 · 기존 방식 프로젝트 전용' : '품질 · 현재 양식'}</small></span></button>)}
          {state.items.length > 0 ? <button type="button" className={workspaceMode === 'material-category-iqc' ? 'is-active' : ''} onClick={() => setWorkspaceMode('material-category-iqc')}>
            <span><b>구매품별 IQC 양식</b><small>품질 · 구분별 검사 방식·항목</small></span>
          </button> : null}
          {state.items.length > 0 ? <button type="button" className={workspaceMode === 'production-manufacturing' ? 'is-active' : ''} onClick={() => setWorkspaceMode('production-manufacturing')}>
            <span><b>Item별 제조 양식</b><small>제조 · Item별 현재 양식</small></span>
          </button> : null}
          {state.items.length > 0 ? <button type="button" className={workspaceMode === 'production-planning' ? 'is-active' : ''} onClick={() => setWorkspaceMode('production-planning')}>
            <span><b>생산계획·실적 연결</b><small>생산관리 · Item별 1:1 연결</small></span>
          </button> : null}
          <button type="button" className={workspaceMode === 'material-categories' ? 'is-active' : ''} onClick={() => setWorkspaceMode('material-categories')}>
            <span><b>구매품 구분 관리</b><small>품질 · 이름·사용 상태·순서</small></span>
          </button>
        </nav>

        {workspaceMode === 'inspection' && selectedTemplate?.family === 'PanelQualityStage' && selectedTemplate.templateKey === 'LQC' ? (
          <LqcItemTemplateWorkspace developmentUserKey={developmentUserKey} isSystemAdministrator={isSystemAdministrator} />
        ) : workspaceMode === 'inspection' ? (
        <section className="form-template-editor form-template-current-editor" aria-label="현재 양식 항목 편집">
          {!selectedVersion ? <p>현재 양식을 불러오지 못했습니다.</p> : <>
            <header>
              <div><p className="eyebrow">현재 양식</p><h3>{versions?.displayName ?? selectedVersion.displayName}</h3></div>
              <div className="form-template-editor-controls">
                <div className="form-template-primary-actions">
                  {!editing ? <button type="button" className="secondary-button" disabled={templateActionBusy} onClick={beginEditing}>수정</button> : null}
                  {editing ? <button type="button" disabled={templateActionBusy} onClick={cancelEditing}>취소</button> : null}
                  {editing ? <button type="button" className="primary-button" disabled={templateActionBusy} onClick={() => void saveCurrent()}>{actions.isBusy('template:save') ? '저장 중' : '저장'}</button> : null}
                </div>
              </div>
            </header>
            {!editing ? <p className="form-version-lock">현재는 조회 상태입니다. 내용을 바꾸려면 수정 버튼을 눌러 주세요.</p> : null}
            {editing ? <p className="form-version-editing" role="status"><strong>현재 양식 수정 중</strong><span>저장하면 이후 시작되는 새 검사부터 적용됩니다.</span></p> : null}
            <div className="form-item-list">{draftItems.map((item, index) => <FormItemEditor key={item.definitionKey ?? item.itemId} item={item} index={index} editable={editing} manufacturing={false} onChange={(next) => setDraftItems((current) => current.map((candidate, candidateIndex) => candidateIndex === index ? next : candidate))} onMove={(offset) => setDraftItems((current) => moveItem(current, index, offset))} onRemove={() => setDraftItems((current) => resequence(current.filter((_, candidateIndex) => candidateIndex !== index)))} />)}</div>
            {editing ? <div className="form-editor-actions"><button type="button" onClick={() => setDraftItems((current) => [...current, newItem(current.length + 1, false)])}>항목 추가</button></div> : null}
          </>}
        </section>
        ) : workspaceMode === 'material-category-iqc' ? (
          <MaterialCategoryIqcWorkspace developmentUserKey={developmentUserKey} />
        ) : workspaceMode === 'material-categories' ? (
          <MaterialCategoryWorkspace developmentUserKey={developmentUserKey} />
        ) : (
          <ProductionControlTemplateWorkspace
            developmentUserKey={developmentUserKey}
            domain={workspaceMode === 'production-manufacturing' ? 'manufacturing' : 'planning'}
          />
        )}
      </div>

      {managerPanelOpen && isSystemAdministrator ? <section className="form-manager-panel">
        <header><div><p className="eyebrow">부서장 권한</p><h3>부서 양식 관리자 지정</h3></div><button type="button" onClick={() => setManagerPanelOpen(false)}>닫기</button></header>
        <div className="form-manager-assign"><select value={candidateUserId} onChange={(event) => setCandidateUserId(event.target.value)}><option value="">품질·제조·생산관리 부서 사용자 선택</option>{managers?.candidates.map((candidate) => <option key={candidate.userId} value={candidate.userId}>{candidate.departmentName} · {candidate.displayName}</option>)}</select><button type="button" className="primary-button" disabled={!candidateUserId} onClick={() => void assignManager()}>부서장 지정</button></div>
        <div className="form-manager-list">{managers?.bindings.filter((binding) => !binding.revokedAtUtc).map((binding) => <article key={binding.bindingId}><span><strong>{binding.displayName}</strong><small>{binding.departmentName} · {binding.domain === 'Quality' ? '품질 양식' : binding.domain === 'ProductionPlanning' ? '생산계획 양식' : '제조 양식'}</small></span><button type="button" onClick={() => void revokeManager(binding.bindingId)}>지정 해제</button></article>)}</div>
      </section> : null}
    </section>
  );
}

function LqcItemTemplateWorkspace({
  developmentUserKey,
  isSystemAdministrator
}: {
  developmentUserKey: string | undefined;
  isSystemAdministrator: boolean;
}) {
  const [catalog, setCatalog] = useState<LqcItemTemplates | null>(null);
  const [selectedProductTypeId, setSelectedProductTypeId] = useState('');
  const [draftItems, setDraftItems] = useState<FormTemplateItem[]>([]);
  const [editing, setEditing] = useState(false);
  const [error, setError] = useState('');
  const actions = useActionFeedback();

  const selectedItem = catalog?.items.find((item) => item.productTypeId === selectedProductTypeId)
    ?? catalog?.items[0]
    ?? null;

  const load = useCallback(async () => {
    try {
      const next = await getLqcItemTemplates(developmentUserKey);
      setCatalog(next);
      const selected = next.items.find((item) => item.productTypeId === selectedProductTypeId) ?? next.items[0];
      setSelectedProductTypeId(selected?.productTypeId ?? '');
      setDraftItems(selected?.items.map(copyItem) ?? []);
      setEditing(false);
      setError('');
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : 'Item별 LQC 설정을 불러오지 못했습니다.');
    }
  }, [developmentUserKey, selectedProductTypeId]);

  useEffect(() => { queueMicrotask(() => void load()); }, [developmentUserKey]); // eslint-disable-line react-hooks/exhaustive-deps

  function chooseItem(productTypeId: string) {
    const next = catalog?.items.find((item) => item.productTypeId === productTypeId) ?? null;
    setSelectedProductTypeId(productTypeId);
    setDraftItems(next?.items.map(copyItem) ?? []);
    setEditing(false);
    actions.reset();
  }

  async function changeOperatingStatus(isOperational: boolean) {
    if (!selectedItem || !catalog?.canChangeOperatingStatus) return;
    await actions.run('lqc:status', async () => {
      const next = await updateLqcItemOperatingStatus(
        developmentUserKey,
        selectedItem.productTypeId,
        isOperational,
        selectedItem.settingRowVersion
      );
      setCatalog(next);
    }, {
      loadingMessage: 'LQC 운영 상태를 저장하는 중입니다.',
      successMessage: `${selectedItem.productTypeName} LQC를 ${isOperational ? '운영 중' : '운영 중지'}으로 변경했습니다. 이후 생성 프로젝트부터 적용됩니다.`,
      errorFallback: 'LQC 운영 상태를 저장하지 못했습니다.'
    });
  }

  async function saveTemplate() {
    if (!selectedItem || !editing) return;
    await actions.run('lqc:template', async () => {
      const next = await saveLqcItemTemplate(
        developmentUserKey,
        selectedItem.productTypeId,
        selectedItem.templateRowVersion,
        resequence(draftItems)
      );
      setCatalog(next);
      const updated = next.items.find((item) => item.productTypeId === selectedItem.productTypeId);
      setDraftItems(updated?.items.map(copyItem) ?? []);
      setEditing(false);
    }, {
      loadingMessage: 'Item별 LQC 검사 항목을 저장하는 중입니다.',
      successMessage: `${selectedItem.productTypeName} LQC 검사 항목을 저장했습니다. 이후 생성 프로젝트부터 적용됩니다.`,
      errorFallback: 'LQC 검사 항목을 저장하지 못했습니다.'
    });
  }

  if (error) {
    return <section className="form-template-editor form-template-current-editor"><p role="alert">{error}</p><button type="button" onClick={() => void load()}>다시 시도</button></section>;
  }
  if (!catalog) {
    return <section className="form-template-editor form-template-current-editor"><p role="status">Item별 LQC 설정을 불러오는 중입니다.</p></section>;
  }
  if (!selectedItem) {
    return <section className="form-template-editor form-template-current-editor"><p>관리할 Item이 없습니다.</p></section>;
  }

  const latestFeedback = actions.latestFeedback;
  const busy = actions.hasBusyPrefix('lqc:');

  return (
    <section className="form-template-editor form-template-current-editor" aria-label="Item별 LQC 운영 상태와 검사 항목">
      <header>
        <div><p className="eyebrow">ITEM별 LQC</p><h3>{selectedItem.productTypeName} LQC 검사</h3></div>
        <div className="form-template-editor-controls">
          <label>
            Item
            <select value={selectedItem.productTypeId} disabled={busy || editing} onChange={(event) => chooseItem(event.target.value)}>
              {catalog.items.map((item) => <option key={item.productTypeId} value={item.productTypeId}>{item.productTypeCode} · {item.productTypeName}</option>)}
            </select>
          </label>
        </div>
      </header>

      <div className="form-version-editing" role="status">
        <strong>이후 생성 프로젝트 적용</strong>
        <span>운영 상태와 검사 항목을 바꿔도 이미 만들어진 프로젝트에는 영향을 주지 않습니다.</span>
      </div>

      <div className="form-item-options">
        <label title={!isSystemAdministrator ? '시스템 관리자만 LQC 운영 상태를 변경할 수 있습니다.' : undefined}>
          <input
            type="checkbox"
            role="switch"
            checked={selectedItem.isOperational}
            disabled={busy || editing || !catalog.canChangeOperatingStatus}
            onChange={(event) => void changeOperatingStatus(event.target.checked)}
          />
          {selectedItem.isOperational ? '운영 중' : '운영 중지'}
        </label>
        {!catalog.canChangeOperatingStatus ? <span className="form-manager-badge">운영 상태 변경 · 시스템 관리자 전용</span> : null}
      </div>

      {latestFeedback ? <p className="form-template-feedback" data-tone={latestFeedback.tone} role={latestFeedback.tone === 'error' ? 'alert' : 'status'}>{latestFeedback.message}</p> : null}

      <header>
        <div><p className="eyebrow">현재 양식</p><h3>검사 항목</h3></div>
        <div className="form-template-primary-actions">
          {!editing ? <button type="button" className="secondary-button" disabled={busy} onClick={() => { setDraftItems(selectedItem.items.map(copyItem)); setEditing(true); }}>수정</button> : null}
          {editing ? <button type="button" disabled={busy} onClick={() => { setDraftItems(selectedItem.items.map(copyItem)); setEditing(false); }}>취소</button> : null}
          {editing ? <button type="button" className="primary-button" disabled={busy} onClick={() => void saveTemplate()}>{actions.isBusy('lqc:template') ? '저장 중' : '저장'}</button> : null}
        </div>
      </header>
      {!editing ? <p className="form-version-lock">현재는 조회 상태입니다. 내용을 바꾸려면 수정 버튼을 눌러 주세요.</p> : null}
      {editing ? <p className="form-version-editing" role="status"><strong>검사 항목 수정 중</strong><span>저장한 내용은 이후 생성되는 이 Item 프로젝트부터 적용됩니다.</span></p> : null}
      <div className="form-item-list">{draftItems.map((item, index) => <FormItemEditor key={item.definitionKey ?? item.itemId} item={item} index={index} editable={editing} manufacturing={false} onChange={(next) => setDraftItems((current) => current.map((candidate, candidateIndex) => candidateIndex === index ? next : candidate))} onMove={(offset) => setDraftItems((current) => moveItem(current, index, offset))} onRemove={() => setDraftItems((current) => resequence(current.filter((_, candidateIndex) => candidateIndex !== index)))} />)}</div>
      {editing ? <div className="form-editor-actions"><button type="button" onClick={() => setDraftItems((current) => [...current, newItem(current.length + 1, false)])}>항목 추가</button></div> : null}
    </section>
  );
}

function FormItemEditor({ item, index, editable, manufacturing, onChange, onMove, onRemove }: { item: FormTemplateItem; index: number; editable: boolean; manufacturing: boolean; onChange: (item: FormTemplateItem) => void; onMove: (offset: number) => void; onRemove: () => void }) {
  if (!editable) {
    return (
      <article className="form-item-editor form-item-preview">
        <div className="form-item-order"><b>{index + 1}</b></div>
        <div>
          <strong>{item.label || '항목명 없음'}</strong>
          {!manufacturing ? <p>{item.guidance || '별도 안내 없음'}</p> : null}
          <dl>
            <div><dt>응답</dt><dd>{item.responseType === 'Check' ? '확인형' : '텍스트'}</dd></div>
            <div><dt>필수</dt><dd>{item.isRequired ? '필수' : '선택'}</dd></div>
            {!manufacturing ? <div><dt>사진</dt><dd>{item.requiresPhoto ? '필수' : '선택'}</dd></div> : null}
          </dl>
        </div>
      </article>
    );
  }

  return <article className="form-item-editor"><div className="form-item-order"><b>{index + 1}</b><button type="button" disabled={index === 0} onClick={() => onMove(-1)}>↑</button><button type="button" onClick={() => onMove(1)}>↓</button></div><div className="form-item-fields"><label>항목명<input value={item.label} onChange={(event) => onChange({ ...item, label: event.target.value })} /></label>{!manufacturing ? <><label>안내문<input value={item.guidance ?? ''} onChange={(event) => onChange({ ...item, guidance: event.target.value || null })} /></label><div className="form-item-options"><label>응답<select value={item.responseType} onChange={(event) => onChange({ ...item, responseType: event.target.value as 'Check' | 'Text', requiresPhoto: event.target.value === 'Text' ? false : item.requiresPhoto, maxTextLength: event.target.value === 'Text' ? (item.maxTextLength ?? 1000) : null })}><option value="Check">확인형</option><option value="Text">텍스트</option></select></label><label><input type="checkbox" checked={item.isRequired} onChange={(event) => onChange({ ...item, isRequired: event.target.checked })} />필수</label><label><input type="checkbox" disabled={item.responseType !== 'Check'} checked={item.requiresPhoto} onChange={(event) => onChange({ ...item, requiresPhoto: event.target.checked })} />사진 필수</label></div></> : null}</div><button type="button" className="form-item-remove" onClick={onRemove}>삭제</button></article>;
}

function copyItem(item: FormTemplateItem): FormTemplateItem { return { ...item }; }
function newItem(order: number, manufacturing: boolean): FormTemplateItem { const id = crypto.randomUUID(); return { itemId: id, itemCode: `ITEM_${id.replaceAll('-', '').slice(0, 8).toUpperCase()}`, displayOrder: order, label: manufacturing ? '새 작업 단계' : '새 검사 항목', guidance: manufacturing ? null : '확인할 내용을 입력해 주세요.', responseType: 'Check', isRequired: true, requiresPhoto: false, maxTextLength: null, definitionKey: null }; }
function resequence(items: FormTemplateItem[]) { return items.map((item, index) => ({ ...item, displayOrder: index + 1 })); }
function moveItem(items: FormTemplateItem[], index: number, offset: number) { const target = index + offset; if (target < 0 || target >= items.length) return items; const next = [...items]; [next[index], next[target]] = [next[target], next[index]]; return resequence(next); }

function MaterialCategoryIqcWorkspace({ developmentUserKey }: { developmentUserKey: string | undefined }) {
  const [catalog, setCatalog] = useState<MaterialCategoryIqcTemplates | null>(null);
  const [selectedCategoryId, setSelectedCategoryId] = useState('');
  const [search, setSearch] = useState('');
  const [settingEnabled, setSettingEnabled] = useState(false);
  const [settingMode, setSettingMode] = useState<'ScanBased' | 'Detailed'>('ScanBased');
  const [draftItems, setDraftItems] = useState<FormTemplateItem[]>([]);
  const [editing, setEditing] = useState(false);
  const [error, setError] = useState('');
  const actions = useActionFeedback();

  const selected = catalog?.items.find((item) => item.materialCategoryId === selectedCategoryId)
    ?? catalog?.items[0]
    ?? null;
  const normalizedSearch = search.trim().toLocaleLowerCase('ko-KR');
  const filteredItems = catalog?.items.filter((item) => !normalizedSearch
    || item.materialCategoryName.toLocaleLowerCase('ko-KR').includes(normalizedSearch)
    || item.materialCategoryCode.toLocaleLowerCase('ko-KR').includes(normalizedSearch)) ?? [];
  const selectorItems = selected && !filteredItems.some((item) => item.materialCategoryId === selected.materialCategoryId)
    ? [selected, ...filteredItems]
    : filteredItems;

  function applySelection(nextCatalog: MaterialCategoryIqcTemplates, preferredId?: string) {
    const next = nextCatalog.items.find((item) => item.materialCategoryId === preferredId)
      ?? nextCatalog.items[0]
      ?? null;
    setCatalog(nextCatalog);
    setSelectedCategoryId(next?.materialCategoryId ?? '');
    setSettingEnabled(next?.isEnabled ?? false);
    setSettingMode(next?.decisionMode ?? 'ScanBased');
    setDraftItems(next?.items.map(copyItem) ?? []);
    setEditing(false);
  }

  const load = useCallback(async () => {
    try {
      const next = await getMaterialCategoryIqcTemplates(developmentUserKey);
      applySelection(next, selectedCategoryId);
      setError('');
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : '구매품별 IQC 설정을 불러오지 못했습니다.');
    }
  }, [developmentUserKey, selectedCategoryId]);

  useEffect(() => { queueMicrotask(() => void load()); }, [developmentUserKey]); // eslint-disable-line react-hooks/exhaustive-deps

  function chooseCategory(categoryId: string) {
    const next = catalog?.items.find((item) => item.materialCategoryId === categoryId) ?? null;
    setSelectedCategoryId(categoryId);
    setSettingEnabled(next?.isEnabled ?? false);
    setSettingMode(next?.decisionMode ?? 'ScanBased');
    setDraftItems(next?.items.map(copyItem) ?? []);
    setEditing(false);
    actions.reset();
  }

  async function saveSetting() {
    if (!selected) return;
    await actions.run('category-iqc:setting', async () => {
      const next = await updateMaterialCategoryIqcSetting(
        developmentUserKey,
        selected.materialCategoryId,
        settingEnabled,
        settingMode,
        selected.settingRowVersion
      );
      applySelection(next, selected.materialCategoryId);
    }, {
      loadingMessage: '구매품 IQC 설정을 저장하는 중입니다.',
      successMessage: `${selected.materialCategoryName} IQC 설정을 저장했습니다. 이후 저장되는 구매품부터 적용됩니다.`,
      errorFallback: '구매품 IQC 설정을 저장하지 못했습니다.'
    });
  }

  async function saveTemplate() {
    if (!selected || !editing) return;
    await actions.run('category-iqc:template', async () => {
      const next = await saveMaterialCategoryIqcTemplate(
        developmentUserKey,
        selected.materialCategoryId,
        selected.templateRowVersion,
        resequence(draftItems)
      );
      applySelection(next, selected.materialCategoryId);
    }, {
      loadingMessage: '구매품별 상세 검사 항목을 저장하는 중입니다.',
      successMessage: `${selected.materialCategoryName} 상세 검사 항목을 저장했습니다. 이후 시작되는 새 검사부터 적용됩니다.`,
      errorFallback: '구매품별 상세 검사 항목을 저장하지 못했습니다.'
    });
  }

  if (error) return <section className="form-template-editor form-template-current-editor"><p role="alert">{error}</p><button type="button" onClick={() => void load()}>다시 시도</button></section>;
  if (!catalog) return <section className="form-template-editor form-template-current-editor"><p role="status">구매품별 IQC 설정을 불러오는 중입니다.</p></section>;
  if (!selected) return <section className="form-template-editor form-template-current-editor"><p>관리할 구매품 구분이 없습니다.</p></section>;

  const busy = actions.hasBusyPrefix('category-iqc:');
  const detailedActivationBlocked = settingEnabled && settingMode === 'Detailed' && selected.items.length < 1;
  const latestFeedback = actions.latestFeedback;

  return (
    <section className="form-template-editor form-template-current-editor material-category-iqc-editor" aria-label="구매품 구분별 IQC 설정과 검사 항목">
      <header>
        <div>
          <p className="eyebrow">구매품별 IQC</p>
          <h3>{selected.materialCategoryName} 수입검사</h3>
          {!selected.isCategoryActive ? <span className="form-template-version-status">비활성 구분</span> : null}
        </div>
        <div className="form-template-editor-controls material-category-iqc-selector">
          <label>구분 검색<input type="search" value={search} placeholder="이름 또는 코드" onChange={(event) => setSearch(event.target.value)} /></label>
          <label>구매품 구분<select value={selected.materialCategoryId} disabled={busy || editing} onChange={(event) => chooseCategory(event.target.value)}>{selectorItems.map((item) => <option key={item.materialCategoryId} value={item.materialCategoryId}>{item.materialCategoryName}{item.isCategoryActive ? '' : ' · 비활성'}</option>)}</select></label>
        </div>
      </header>

      <div className="form-version-editing" role="status">
        <strong>저장 시점부터 적용</strong>
        <span>검사 여부와 방식을 바꿔도 이미 저장된 구매품과 시작된 검사에는 영향을 주지 않습니다.</span>
      </div>

      <section className="material-category-iqc-setting" aria-labelledby="material-category-iqc-setting-title">
        <div><p className="eyebrow">검사 설정</p><h4 id="material-category-iqc-setting-title">검사 여부와 방식</h4></div>
        <div className="form-item-options">
          <label><input type="checkbox" role="switch" checked={settingEnabled} disabled={busy || editing} onChange={(event) => setSettingEnabled(event.target.checked)} />{settingEnabled ? '검사 운영' : '검사 없음'}</label>
          <label>검사 방식<select value={settingMode} disabled={busy || editing} onChange={(event) => setSettingMode(event.target.value as 'ScanBased' | 'Detailed')}><option value="ScanBased">스캔형 · 서명 검사서 첨부</option><option value="Detailed">상세형 · 검사 항목 입력</option></select></label>
          <button type="button" className="primary-button" disabled={busy || editing || detailedActivationBlocked} onClick={() => void saveSetting()}>{actions.isBusy('category-iqc:setting') ? '저장 중' : '설정 저장'}</button>
        </div>
        {detailedActivationBlocked ? <p className="form-template-feedback" data-tone="error" role="alert">상세형 검사를 켜려면 아래 검사 항목을 1개 이상 먼저 저장해 주세요.</p> : null}
      </section>

      {latestFeedback ? <p className="form-template-feedback" data-tone={latestFeedback.tone} role={latestFeedback.tone === 'error' ? 'alert' : 'status'}>{latestFeedback.message}</p> : null}

      <header>
        <div><p className="eyebrow">상세형 현재 양식</p><h3>검사 항목 <small>v{selected.templateVersionNumber}</small></h3></div>
        <div className="form-template-primary-actions">
          {!editing ? <button type="button" className="secondary-button" disabled={busy} onClick={() => { setDraftItems(selected.items.map(copyItem)); setEditing(true); }}>수정</button> : null}
          {editing ? <button type="button" disabled={busy} onClick={() => { setDraftItems(selected.items.map(copyItem)); setEditing(false); }}>취소</button> : null}
          {editing ? <button type="button" className="primary-button" disabled={busy || (selected.isEnabled && selected.decisionMode === 'Detailed' && draftItems.length < 1)} onClick={() => void saveTemplate()}>{actions.isBusy('category-iqc:template') ? '저장 중' : '저장'}</button> : null}
        </div>
      </header>
      {!editing ? <p className="form-version-lock">스캔형은 항목 없이 사용할 수 있습니다. 상세형으로 운영하려면 항목을 1개 이상 준비해 주세요.</p> : <p className="form-version-editing" role="status"><strong>상세 검사 항목 수정 중</strong><span>저장한 항목은 이후 처음 열리는 새 상세 검사부터 적용됩니다.</span></p>}
      <div className="form-item-list">{draftItems.length === 0 && !editing ? <p className="form-version-lock">등록된 상세 검사 항목이 없습니다.</p> : draftItems.map((item, index) => <FormItemEditor key={item.definitionKey ?? item.itemId} item={item} index={index} editable={editing} manufacturing={false} onChange={(next) => setDraftItems((current) => current.map((candidate, candidateIndex) => candidateIndex === index ? next : candidate))} onMove={(offset) => setDraftItems((current) => moveItem(current, index, offset))} onRemove={() => setDraftItems((current) => resequence(current.filter((_, candidateIndex) => candidateIndex !== index)))} />)}</div>
      {editing ? <div className="form-editor-actions"><button type="button" onClick={() => setDraftItems((current) => [...current, newItem(current.length + 1, false)])}>항목 추가</button></div> : null}
    </section>
  );
}

function MaterialCategoryWorkspace({ developmentUserKey }: { developmentUserKey: string | undefined }) {
  const [catalog, setCatalog] = useState<MaterialCategoryCatalog | null>(null);
  const [drafts, setDrafts] = useState<MaterialCategory[]>([]);
  const [newName, setNewName] = useState('');
  const [message, setMessage] = useState('');
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    try {
      const next = await getMaterialCategories(developmentUserKey, true);
      setCatalog(next);
      setDrafts(next.items.map((item) => ({ ...item })));
      setMessage('');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : '구매품 구분을 불러오지 못했습니다.');
    }
  }, [developmentUserKey]);

  useEffect(() => { queueMicrotask(() => void load()); }, [load]);

  async function save(item: MaterialCategory) {
    setSaving(true);
    try {
      const next = await updateMaterialCategory(developmentUserKey, item.categoryId, {
        expectedRowVersion: item.rowVersion,
        displayName: item.displayName,
        isActive: item.isActive,
        displayOrder: item.displayOrder
      });
      setCatalog(next);
      setDrafts(next.items.map((value) => ({ ...value })));
      setMessage(`${item.displayName} 구분을 저장했습니다. 이미 저장된 구매품의 구분은 바뀌지 않습니다.`);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : '구분을 저장하지 못했습니다.');
    } finally {
      setSaving(false);
    }
  }

  async function add() {
    if (!newName.trim()) return;
    setSaving(true);
    try {
      const nextOrder = Math.max(0, ...drafts.map((item) => item.displayOrder)) + 10;
      const next = await createMaterialCategory(developmentUserKey, newName.trim(), nextOrder);
      setCatalog(next);
      setDrafts(next.items.map((value) => ({ ...value })));
      setNewName('');
      setMessage('새 구매품 구분을 추가했습니다.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : '구분을 추가하지 못했습니다.');
    } finally {
      setSaving(false);
    }
  }

  if (!catalog) return <section className="form-template-editor"><p role="status">{message || '구매품 구분을 불러오는 중입니다.'}</p></section>;
  return (
    <section className="form-template-editor material-category-editor" aria-labelledby="material-category-title">
      <header><div><p className="eyebrow">구매품 분류</p><h3 id="material-category-title">구매품 구분 관리</h3></div></header>
      <p className="form-version-lock">구매팀이 선택할 구분의 이름·사용 상태·순서를 관리합니다. IQC 방식은 구매품별 IQC 양식에서 변경합니다.</p>
      {message ? <p className="form-template-feedback" role="status">{message}</p> : null}
      <div className="form-item-list">
        {drafts.map((item, index) => (
          <article className="form-item-editor" key={item.categoryId}>
            <div className="form-item-order"><b>{index + 1}</b></div>
            <div className="form-item-fields">
              <label>구분명<input disabled={!catalog.canManage || saving} value={item.displayName} onChange={(event) => setDrafts((current) => current.map((value) => value.categoryId === item.categoryId ? { ...value, displayName: event.target.value } : value))} /></label>
              <div className="form-item-options">
                <span className="form-template-version-status" data-status={item.requiresIqc ? 'Active' : 'Archived'}>{item.requiresIqc ? item.iqcDecisionMode === 'Detailed' ? 'IQC · 상세형' : 'IQC · 스캔형' : 'IQC 없음'}</span>
                <label><input type="checkbox" disabled={!catalog.canManage || saving} checked={item.isActive} onChange={(event) => setDrafts((current) => current.map((value) => value.categoryId === item.categoryId ? { ...value, isActive: event.target.checked } : value))} />구매 입력에 표시</label>
                <label>순서<input type="number" min={1} max={1000} disabled={!catalog.canManage || saving} value={item.displayOrder} onChange={(event) => setDrafts((current) => current.map((value) => value.categoryId === item.categoryId ? { ...value, displayOrder: Number(event.target.value) } : value))} /></label>
              </div>
            </div>
            {catalog.canManage ? <button type="button" className="secondary-button" disabled={saving} onClick={() => void save(item)}>저장</button> : null}
          </article>
        ))}
      </div>
      {catalog.canManage ? <div className="form-editor-actions material-category-add"><input aria-label="새 구매품 구분명" placeholder="새 구분명" value={newName} onChange={(event) => setNewName(event.target.value)} /><button type="button" disabled={saving || !newName.trim()} onClick={() => void add()}>구분 추가</button></div> : null}
    </section>
  );
}
