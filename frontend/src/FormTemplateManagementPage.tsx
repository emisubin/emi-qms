import { useCallback, useEffect, useState } from 'react';
import {
  activateFormTemplateVersion,
  assignFormTemplateManager,
  cancelFormTemplateDraft,
  createFormTemplateDraft,
  exportFormTemplateVersionsExcel,
  getFormTemplateCatalog,
  getFormTemplateManagers,
  getFormTemplateVersions,
  revokeFormTemplateManager,
  saveFormTemplateItems
} from './api';
import { useAdaptiveLayout } from './adaptive-layout';
import type { FormTemplateCatalogItem, FormTemplateItem, FormTemplateManagers, FormTemplateVersion, FormTemplateVersions } from './formTemplates';
import { SelectedExportTray, SelectionCheckbox } from './SelectedExcelExport';
import { useActionFeedback } from './useActionFeedback';

type LoadState = { kind: 'loading' } | { kind: 'ready'; items: FormTemplateCatalogItem[] } | { kind: 'error'; message: string };

export function FormTemplateManagementPage({ developmentUserKey, isSystemAdministrator }: { developmentUserKey: string | undefined; isSystemAdministrator: boolean }) {
  const { isMobile } = useAdaptiveLayout();
  const [state, setState] = useState<LoadState>({ kind: 'loading' });
  const [selectedKey, setSelectedKey] = useState('');
  const [versions, setVersions] = useState<FormTemplateVersions | null>(null);
  const [selectedVersionId, setSelectedVersionId] = useState('');
  const [draftItems, setDraftItems] = useState<FormTemplateItem[]>([]);
  const [selectedVersionIds, setSelectedVersionIds] = useState<Set<string>>(new Set());
  const [exportBusy, setExportBusy] = useState(false);
  const [managerPanelOpen, setManagerPanelOpen] = useState(false);
  const [managers, setManagers] = useState<FormTemplateManagers | null>(null);
  const [candidateUserId, setCandidateUserId] = useState('');
  const actions = useActionFeedback();

  const selectedTemplate = state.kind === 'ready' ? state.items.find((item) => `${item.family}:${item.templateKey}` === selectedKey) ?? null : null;
  const selectedVersion = versions?.versions.find((version) => version.versionId === selectedVersionId) ?? null;

  const loadVersions = useCallback(async (template: FormTemplateCatalogItem, preferredId?: string) => {
    const data = await getFormTemplateVersions(developmentUserKey, template.family, template.templateKey);
    setVersions(data);
    const next = data.versions.find((version) => version.versionId === preferredId)
      ?? data.versions.find((version) => version.lifecycleStatus === 'Draft')
      ?? data.versions.find((version) => version.lifecycleStatus === 'Active')
      ?? data.versions[0];
    setSelectedVersionId(next?.versionId ?? '');
    setDraftItems(next?.items.map(copyItem) ?? []);
    setSelectedVersionIds(new Set());
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
        await loadVersions(first);
      }
    } catch (error) {
      setState({ kind: 'error', message: error instanceof Error ? error.message : '양식 목록을 불러오지 못했습니다.' });
    }
  }, [developmentUserKey, loadVersions, selectedKey]);

  useEffect(() => { queueMicrotask(() => void load()); }, [developmentUserKey]); // eslint-disable-line react-hooks/exhaustive-deps

  async function chooseTemplate(template: FormTemplateCatalogItem) {
    setSelectedKey(`${template.family}:${template.templateKey}`);
    actions.reset();
    await loadVersions(template);
  }

  async function createDraft() {
    if (!selectedTemplate || !versions) return;
    const active = versions.versions.find((version) => version.lifecycleStatus === 'Active');
    if (!active) return;
    await actions.run('template:create', async () => {
      const result = await createFormTemplateDraft(developmentUserKey, selectedTemplate.family, selectedTemplate.templateKey, active.rowVersion);
      setVersions(result);
      const draft = result.versions.find((version) => version.lifecycleStatus === 'Draft');
      setSelectedVersionId(draft?.versionId ?? '');
      setDraftItems(draft?.items.map(copyItem) ?? []);
    }, { loadingMessage: '새 초안을 만드는 중입니다.', successMessage: '활성 양식을 복제해 새 초안을 만들었습니다.', errorFallback: '새 초안을 만들지 못했습니다.' });
  }

  async function beginEditing() {
    if (!versions || selectedVersion?.lifecycleStatus === 'Draft') return;
    const existingDraft = versions.versions
      .filter((version) => version.lifecycleStatus === 'Draft')
      .sort((left, right) => right.versionNumber - left.versionNumber)[0];
    if (existingDraft) {
      setSelectedVersionId(existingDraft.versionId);
      setDraftItems(existingDraft.items.map(copyItem));
      actions.setFeedback('template:edit', { tone: 'neutral', message: '저장 중인 초안을 열었습니다.' });
      return;
    }
    await createDraft();
  }

  async function saveDraft() {
    if (!selectedTemplate || !selectedVersion || selectedVersion.lifecycleStatus !== 'Draft') return;
    await actions.run('template:save', async () => {
      const result = await saveFormTemplateItems(developmentUserKey, selectedTemplate.family, selectedTemplate.templateKey, selectedVersion.versionId, selectedVersion.rowVersion, draftItems);
      setVersions(result);
      const updated = result.versions.find((version) => version.versionId === selectedVersion.versionId);
      setDraftItems(updated?.items.map(copyItem) ?? []);
    }, { loadingMessage: '양식 항목을 저장하는 중입니다.', successMessage: '초안 항목을 저장했습니다.', errorFallback: '양식 항목을 저장하지 못했습니다.' });
  }

  async function activateDraft() {
    if (!selectedTemplate || !selectedVersion || selectedVersion.lifecycleStatus !== 'Draft') return;
    if (!window.confirm('이 초안을 활성화할까요? 기존 활성 버전은 삭제되지 않고 보관됩니다.')) return;
    await actions.run('template:activate', async () => {
      const result = await activateFormTemplateVersion(developmentUserKey, selectedTemplate.family, selectedTemplate.templateKey, selectedVersion.versionId, selectedVersion.rowVersion);
      setVersions(result);
      const active = result.versions.find((version) => version.lifecycleStatus === 'Active');
      setSelectedVersionId(active?.versionId ?? '');
      setDraftItems(active?.items.map(copyItem) ?? []);
    }, { loadingMessage: '새 양식을 활성화하는 중입니다.', successMessage: '새 버전을 활성화했습니다. 이후 새 업무부터 적용됩니다.', errorFallback: '양식을 활성화하지 못했습니다.' });
  }

  async function archiveDraft() {
    if (!selectedTemplate || !selectedVersion || selectedVersion.lifecycleStatus !== 'Draft') return;
    await actions.run('template:archive', async () => {
      const result = await cancelFormTemplateDraft(developmentUserKey, selectedTemplate.family, selectedTemplate.templateKey, selectedVersion.versionId, selectedVersion.rowVersion);
      setVersions(result);
      const active = result.versions.find((version) => version.lifecycleStatus === 'Active');
      setSelectedVersionId(active?.versionId ?? '');
      setDraftItems(active?.items.map(copyItem) ?? []);
    }, { loadingMessage: '초안을 보관하는 중입니다.', successMessage: '초안을 보관했습니다.', errorFallback: '초안을 보관하지 못했습니다.' });
  }

  async function openManagers() {
    setManagerPanelOpen(true);
    if (!managers) setManagers(await getFormTemplateManagers(developmentUserKey));
  }

  async function assignManager() {
    const candidate = managers?.candidates.find((item) => item.userId === candidateUserId);
    if (!candidate) return;
    const domain = candidate.departmentCode === 'quality' ? 'Quality' : 'Manufacturing';
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

  const visibleVersionIds = versions?.versions.map((version) => version.versionId) ?? [];
  const allSelected = visibleVersionIds.length > 0 && visibleVersionIds.every((id) => selectedVersionIds.has(id));
  const latestFeedback = actions.latestFeedback;
  const isEditing = selectedVersion?.lifecycleStatus === 'Draft';
  const templateActionBusy = actions.hasBusyPrefix('template:');

  if (state.kind === 'loading') return <section className="page-surface form-template-page"><p role="status">양식 관리 화면을 준비하는 중입니다.</p></section>;
  if (state.kind === 'error') return <section className="page-surface form-template-page"><p role="alert">{state.message}</p><button type="button" onClick={() => void load()}>다시 시도</button></section>;

  return (
    <section className="page-surface form-template-page" data-mobile-experience={isMobile || undefined} aria-labelledby="form-template-title">
      <header className="form-template-header">
        <div><p className="eyebrow">NO-CODE FORM CONTROL</p><h2 id="form-template-title">양식 관리</h2><p>사용 중인 버전은 보존하고 새 초안을 활성화해 이후 업무에 적용합니다.</p></div>
        {isSystemAdministrator ? <button type="button" className="secondary-button" onClick={() => void openManagers()}>부서장 지정</button> : <span className="form-manager-badge">부서 양식 관리자</span>}
      </header>

      {latestFeedback ? <p className="form-template-feedback" data-tone={latestFeedback.tone} role={latestFeedback.tone === 'error' ? 'alert' : 'status'}>{latestFeedback.message}</p> : null}

      <div className="form-template-workspace">
        <nav className="form-template-catalog" aria-label="양식 종류">
          <header><strong>양식 종류</strong><small>{state.items.length}개</small></header>
          {state.items.map((template) => <button key={`${template.family}:${template.templateKey}`} type="button" className={selectedKey === `${template.family}:${template.templateKey}` ? 'is-active' : ''} onClick={() => void chooseTemplate(template)}><span><b>{template.displayName}</b><small>{template.domain === 'Quality' ? '품질' : '제조'} · Active v{template.activeVersionNumber ?? '-'}</small></span>{template.draftCount > 0 ? <i>{template.draftCount}</i> : null}</button>)}
        </nav>

        <section className="form-template-versions" aria-label="양식 버전">
          <header><div><strong>{versions?.displayName ?? '버전'}</strong><small>사용 중 버전은 편집할 때 안전한 초안으로 복제됩니다.</small></div></header>
          <SelectedExportTray
            developmentUserKey={developmentUserKey}
            screen="form-templates"
            visibleIds={visibleVersionIds}
            selectedIds={selectedVersionIds}
            allSelected={allSelected}
            busy={exportBusy}
            filters={{ family: versions?.family, templateKey: versions?.templateKey }}
            exportFile={() => exportFormTemplateVersionsExcel(developmentUserKey, versions?.family ?? '', versions?.templateKey ?? '', [...selectedVersionIds])}
            onBusyChange={setExportBusy}
            onToggleAll={(checked) => setSelectedVersionIds(checked ? new Set(visibleVersionIds) : new Set())}
            onClear={() => setSelectedVersionIds(new Set())}
          />
          <div className="form-version-list">{versions?.versions.map((version) => <button key={version.versionId} type="button" className={selectedVersionId === version.versionId ? 'is-active' : ''} onClick={() => { setSelectedVersionId(version.versionId); setDraftItems(version.items.map(copyItem)); }}><SelectionCheckbox checked={selectedVersionIds.has(version.versionId)} label={`v${version.versionNumber} 선택`} onChange={(checked) => setSelectedVersionIds((current) => { const next = new Set(current); if (checked) next.add(version.versionId); else next.delete(version.versionId); return next; })} /><span><b>v{version.versionNumber} · {version.displayName}</b><small>{version.items.length}개 항목</small></span><i data-status={version.lifecycleStatus}>{statusLabel(version.lifecycleStatus)}</i></button>)}</div>
        </section>

        <section className="form-template-editor" aria-label="양식 항목 편집">
          {!selectedVersion ? <p>버전을 선택해 주세요.</p> : <>
            <header>
              <div><p className="eyebrow">VERSION {selectedVersion.versionNumber}</p><h3>{selectedVersion.displayName}</h3></div>
              <div className="form-template-editor-controls">
                <span className="form-template-version-status" data-status={selectedVersion.lifecycleStatus}>{statusLabel(selectedVersion.lifecycleStatus)}</span>
                <div className="form-template-primary-actions">
                  <button type="button" className="secondary-button" disabled={isEditing || templateActionBusy} onClick={() => void beginEditing()}>편집</button>
                  <button type="button" className="primary-button" disabled={!isEditing || templateActionBusy} onClick={() => void saveDraft()}>{actions.isBusy('template:save') ? '저장 중' : '저장'}</button>
                </div>
              </div>
            </header>
            {selectedVersion.lifecycleStatus !== 'Draft' ? <p className="form-version-lock">사용 중이거나 보관된 버전은 직접 바뀌지 않습니다. 편집을 누르면 사용 중 버전을 복제한 초안이 열립니다.</p> : null}
            <div className="form-item-list">{draftItems.map((item, index) => <FormItemEditor key={item.itemId} item={item} index={index} editable={selectedVersion.lifecycleStatus === 'Draft'} manufacturing={versions?.family === 'Manufacturing'} onChange={(next) => setDraftItems((current) => current.map((candidate, candidateIndex) => candidateIndex === index ? next : candidate))} onMove={(offset) => setDraftItems((current) => moveItem(current, index, offset))} onRemove={() => setDraftItems((current) => resequence(current.filter((_, candidateIndex) => candidateIndex !== index)))} />)}</div>
            {selectedVersion.lifecycleStatus === 'Draft' ? <div className="form-editor-actions"><button type="button" onClick={() => setDraftItems((current) => [...current, newItem(current.length + 1, versions?.family === 'Manufacturing')])}>항목 추가</button><button type="button" onClick={() => void archiveDraft()}>초안 보관</button><button type="button" className="primary-button" onClick={() => void activateDraft()}>활성화</button></div> : null}
          </>}
        </section>
      </div>

      {managerPanelOpen && isSystemAdministrator ? <section className="form-manager-panel">
        <header><div><p className="eyebrow">DEPARTMENT LEADS</p><h3>부서 양식 관리자 지정</h3></div><button type="button" onClick={() => setManagerPanelOpen(false)}>닫기</button></header>
        <div className="form-manager-assign"><select value={candidateUserId} onChange={(event) => setCandidateUserId(event.target.value)}><option value="">품질·제조 부서 사용자 선택</option>{managers?.candidates.map((candidate) => <option key={candidate.userId} value={candidate.userId}>{candidate.departmentName} · {candidate.displayName}</option>)}</select><button type="button" className="primary-button" disabled={!candidateUserId} onClick={() => void assignManager()}>부서장 지정</button></div>
        <div className="form-manager-list">{managers?.bindings.filter((binding) => !binding.revokedAtUtc).map((binding) => <article key={binding.bindingId}><span><strong>{binding.displayName}</strong><small>{binding.departmentName} · {binding.domain === 'Quality' ? '품질 양식' : '제조 양식'}</small></span><button type="button" onClick={() => void revokeManager(binding.bindingId)}>지정 해제</button></article>)}</div>
      </section> : null}
    </section>
  );
}

function FormItemEditor({ item, index, editable, manufacturing, onChange, onMove, onRemove }: { item: FormTemplateItem; index: number; editable: boolean; manufacturing: boolean; onChange: (item: FormTemplateItem) => void; onMove: (offset: number) => void; onRemove: () => void }) {
  return <article className="form-item-editor"><div className="form-item-order"><b>{index + 1}</b><button type="button" disabled={!editable || index === 0} onClick={() => onMove(-1)}>↑</button><button type="button" disabled={!editable} onClick={() => onMove(1)}>↓</button></div><div className="form-item-fields"><label>항목명<input disabled={!editable} value={item.label} onChange={(event) => onChange({ ...item, label: event.target.value })} /></label>{!manufacturing ? <><label>안내문<input disabled={!editable} value={item.guidance ?? ''} onChange={(event) => onChange({ ...item, guidance: event.target.value || null })} /></label><div className="form-item-options"><label>응답<select disabled={!editable} value={item.responseType} onChange={(event) => onChange({ ...item, responseType: event.target.value as 'Check' | 'Text', requiresPhoto: event.target.value === 'Text' ? false : item.requiresPhoto, maxTextLength: event.target.value === 'Text' ? (item.maxTextLength ?? 1000) : null })}><option value="Check">확인형</option><option value="Text">텍스트</option></select></label><label><input type="checkbox" disabled={!editable} checked={item.isRequired} onChange={(event) => onChange({ ...item, isRequired: event.target.checked })} />필수</label><label><input type="checkbox" disabled={!editable || item.responseType !== 'Check'} checked={item.requiresPhoto} onChange={(event) => onChange({ ...item, requiresPhoto: event.target.checked })} />사진 필수</label></div></> : null}</div>{editable ? <button type="button" className="form-item-remove" onClick={onRemove}>삭제</button> : null}</article>;
}

function statusLabel(status: FormTemplateVersion['lifecycleStatus']) { return status === 'Draft' ? '초안' : status === 'Active' ? '사용 중' : '보관'; }
function copyItem(item: FormTemplateItem): FormTemplateItem { return { ...item }; }
function newItem(order: number, manufacturing: boolean): FormTemplateItem { const id = crypto.randomUUID(); return { itemId: id, itemCode: `ITEM_${id.replaceAll('-', '').slice(0, 8).toUpperCase()}`, displayOrder: order, label: manufacturing ? '새 작업 단계' : '새 검사 항목', guidance: manufacturing ? null : '확인할 내용을 입력해 주세요.', responseType: 'Check', isRequired: true, requiresPhoto: false, maxTextLength: null }; }
function resequence(items: FormTemplateItem[]) { return items.map((item, index) => ({ ...item, displayOrder: index + 1 })); }
function moveItem(items: FormTemplateItem[], index: number, offset: number) { const target = index + offset; if (target < 0 || target >= items.length) return items; const next = [...items]; [next[index], next[target]] = [next[target], next[index]]; return resequence(next); }
