import { useEffect, useMemo, useRef, useState } from 'react';
import { fetchJson } from './api';
import './OsanAdminPage.css';

type Customer = { customerId: string; name: string; version: number };
type User = { userId: string; displayName: string; departmentName: string | null; version: number; customerIds: string[] };
type AssignmentData = { customers: Customer[]; users: User[] };
type Gate = { stageSequence: number; name: string; departmentIds: string[] };
type GateData = { version: number; departments: { departmentId: string; name: string }[]; gates: Gate[] };
type Load<T> = { kind: 'loading' } | { kind: 'ready'; data: T } | { kind: 'error'; message: string };
const errorMessage = (error: unknown) => error instanceof Error ? error.message : '요청을 처리하지 못했습니다.';
const normalized = (value: string) => value.replace(/\s/g, '').toLocaleLowerCase('ko-KR');

export function OsanCustomerAdminPage({ developmentUserKey, mutationAllowed = true }: { developmentUserKey?: string; mutationAllowed?: boolean }) {
  const [state, setState] = useState<Load<AssignmentData>>({ kind: 'loading' });
  const [revision, setRevision] = useState(0);
  const [tab, setTab] = useState<'customer' | 'person'>('customer');
  const [search, setSearch] = useState('');
  const [unassigned, setUnassigned] = useState(false);
  const [editing, setEditing] = useState<{ kind: 'customer' | 'person'; id: string } | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [choiceSearch, setChoiceSearch] = useState('');
  const [registerOpen, setRegisterOpen] = useState(false);
  const [name, setName] = useState('');
  const [renaming, setRenaming] = useState<Customer | null>(null);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState('');
  const [formError, setFormError] = useState('');
  const assignmentDialog = useRef<HTMLDialogElement>(null);
  const registrationDialog = useRef<HTMLDialogElement>(null);
  const trigger = useRef<HTMLElement | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: 'loading' });
    fetchJson<AssignmentData>('/api/osan/admin/customer-assignments', developmentUserKey, { signal: controller.signal })
      .then(data => { if (!controller.signal.aborted) setState({ kind: 'ready', data }); })
      .catch(error => { if (!controller.signal.aborted) setState({ kind: 'error', message: errorMessage(error) }); });
    return () => controller.abort();
  }, [developmentUserKey, revision]);
  useEffect(() => {
    const dialog = assignmentDialog.current;
    if (!dialog) return;
    if (editing && !dialog.open) dialog.showModal();
    if (!editing && dialog.open) dialog.close();
  }, [editing]);
  useEffect(() => {
    const dialog = registrationDialog.current;
    if (!dialog) return;
    if (registerOpen && !dialog.open) dialog.showModal();
    if (!registerOpen && dialog.open) dialog.close();
  }, [registerOpen]);
  const data = state.kind === 'ready' ? state.data : null;
  const customers = data?.customers ?? [];
  const users = data?.users ?? [];
  const current = editing?.kind === 'customer'
    ? customers.find(customer => customer.customerId === editing.id)
    : users.find(user => user.userId === editing?.id);
  const choices = editing?.kind === 'customer' ? users : customers;
  const visibleChoices = choices.filter(item => normalized('displayName' in item ? `${item.displayName} ${item.departmentName ?? ''}` : item.name).includes(normalized(choiceSearch)));
  const selectedVisibleCount = visibleChoices.filter(item => selected.includes('userId' in item ? item.userId : item.customerId)).length;
  const records = (tab === 'customer' ? customers : users).filter(item => {
    const text = 'name' in item ? item.name : `${item.displayName} ${item.departmentName ?? ''}`;
    if (!normalized(text).includes(normalized(search))) return false;
    const related = 'name' in item ? users.filter(user => user.customerIds.includes(item.customerId)) : item.customerIds;
    return !unassigned || related.length === 0;
  });
  const openAssignment = (kind: 'customer' | 'person', id: string, element: HTMLElement) => {
    trigger.current = element;
    setChoiceSearch('');
    setFormError('');
    setEditing({ kind, id });
    setSelected(kind === 'customer'
      ? users.filter(user => user.customerIds.includes(id)).map(user => user.userId)
      : users.find(user => user.userId === id)?.customerIds ?? []);
  };
  const closeAssignment = () => { setEditing(null); trigger.current?.focus(); };
  const toggleChoice = (id: string) => setSelected(current => current.includes(id) ? current.filter(value => value !== id) : [...current, id]);
  async function saveAssignment() {
    if (!editing || !data || busy || !mutationAllowed) return;
    setBusy(true); setFormError('');
    try {
      if (editing.kind === 'person') {
        const user = users.find(item => item.userId === editing.id)!;
        await fetchJson(`/api/osan/admin/customer-assignments/${encodeURIComponent(user.userId)}`, developmentUserKey,
          { method: 'PUT', body: JSON.stringify({ customerIds: selected, expectedVersion: user.version }) });
        setFeedback(`${user.displayName} 담당 고객사를 저장했습니다.`);
        closeAssignment();
      } else {
        const customer = customers.find(item => item.customerId === editing.id)!;
        const failed: string[] = [];
        let saved = 0;
        for (const user of users) {
          const assigned = user.customerIds.includes(customer.customerId);
          if (assigned === selected.includes(user.userId)) continue;
          const next = selected.includes(user.userId)
            ? [...user.customerIds, customer.customerId]
            : user.customerIds.filter(id => id !== customer.customerId);
          try {
            await fetchJson(`/api/osan/admin/customer-assignments/${encodeURIComponent(user.userId)}`, developmentUserKey,
              { method: 'PUT', body: JSON.stringify({ customerIds: next, expectedVersion: user.version }) });
            saved += 1;
          } catch (error) { failed.push(`${user.displayName}: ${errorMessage(error)}`); }
        }
        setFeedback(`${customer.name} 담당자 ${saved}명 변경했습니다.${failed.length ? ` 실패: ${failed.join(' / ')}` : ''}`);
        if (!failed.length) closeAssignment();
        else setFormError('일부 배정을 저장하지 못했습니다. 최신 목록을 확인한 뒤 다시 선택해 주세요.');
      }
      setRevision(value => value + 1);
    } catch (error) { setFormError(errorMessage(error)); }
    finally { setBusy(false); }
  }
  const openRegistration = (customer?: Customer, element?: HTMLElement) => {
    trigger.current = element ?? null;
    setRenaming(customer ?? null); setName(customer?.name ?? ''); setFormError(''); setRegisterOpen(true);
  };
  const closeRegistration = () => { setRegisterOpen(false); trigger.current?.focus(); };
  async function saveCustomer() {
    if (busy || !name.trim() || !mutationAllowed) return;
    setBusy(true); setFormError('');
    try {
      if (renaming) {
        await fetchJson(`/api/osan/admin/customers/${encodeURIComponent(renaming.customerId)}`, developmentUserKey,
          { method: 'PUT', body: JSON.stringify({ name: name.trim(), expectedVersion: renaming.version }) });
        setFeedback(`${name.trim()} 이름을 저장했습니다.`);
      } else {
        await fetchJson('/api/osan/admin/customers', developmentUserKey,
          { method: 'POST', body: JSON.stringify({ name: name.trim() }) });
        setFeedback(`${name.trim()} 등록 · 담당자 미배정입니다.`);
      }
      closeRegistration(); setTab('customer'); setSearch(''); setUnassigned(false); setRevision(value => value + 1);
    } catch (error) { setFormError(errorMessage(error)); }
    finally { setBusy(false); }
  }
  return <section className="osan-admin-page" aria-label="오산 고객사 관리">
    <header className="osan-admin-heading"><div><h1>고객사 관리</h1><p>프로젝트에 사용할 고객사와 알림을 받을 담당자를 관리합니다.</p></div>
      <button type="button" className="osan-admin-primary" disabled={!mutationAllowed} onClick={event => openRegistration(undefined, event.currentTarget)}>고객사 등록</button></header>
    <div className="osan-admin-tabs" role="tablist" aria-label="배정 조회 방식">
      <button type="button" role="tab" aria-selected={tab === 'customer'} onClick={() => { setTab('customer'); setSearch(''); setUnassigned(false); }}>고객사별 담당자</button>
      <button type="button" role="tab" aria-selected={tab === 'person'} onClick={() => { setTab('person'); setSearch(''); setUnassigned(false); }}>사용자별 고객사</button>
    </div>
    <div className="osan-admin-toolbar"><input type="search" aria-label="목록 검색" placeholder={tab === 'customer' ? '고객사명 검색' : '이름 또는 부서 검색'} value={search} onChange={event => setSearch(event.target.value)} />
      <label><input type="checkbox" checked={unassigned} onChange={event => setUnassigned(event.target.checked)} />미배정만</label>
      <span aria-live="polite">{records.length}{tab === 'customer' ? '개 고객사' : '명'}</span></div>
    {state.kind === 'loading' && <p role="status">고객사와 담당자를 불러오는 중입니다.</p>}
    {state.kind === 'error' && <p role="alert">{state.message} <button type="button" onClick={() => setRevision(value => value + 1)}>다시 시도</button></p>}
    {data && <div className="osan-admin-records" role="table" aria-label={tab === 'customer' ? '고객사별 담당자' : '사용자별 고객사'}>
      <div className="osan-admin-record-head" role="row"><span role="columnheader">{tab === 'customer' ? '고객사명' : '사용자'}</span><span role="columnheader">{tab === 'customer' ? '알림 담당자' : '담당 고객사'}</span><span role="columnheader">{tab === 'customer' ? '인원' : '고객사 수'}</span><span role="columnheader">설정</span></div>
      {records.map(item => {
        const isCustomer = 'name' in item;
        const related = isCustomer ? users.filter(user => user.customerIds.includes(item.customerId)) : customers.filter(customer => item.customerIds.includes(customer.customerId));
        const title = isCustomer ? item.name : item.displayName;
        const id = isCustomer ? item.customerId : item.userId;
        const summary = related.slice(0, 3).map(value => 'name' in value ? value.name : value.displayName).join(', ');
        return <div role="row" className="osan-admin-record" key={id}>
          <div role="cell"><strong>{title}</strong>{!isCustomer && <small>{item.departmentName}</small>}</div>
          <div role="cell" className="osan-admin-related" title={related.map(value => 'name' in value ? value.name : value.displayName).join(', ')}>{related.length ? summary + (related.length > 3 ? ` 외 ${related.length - 3}${isCustomer ? '명' : '개'}` : '') : <span className="osan-admin-empty-tag">미배정</span>}</div>
          <span role="cell" className="osan-admin-count">{related.length}{isCustomer ? '명' : '개'}</span>
          <div role="cell" className="osan-admin-record-actions"><button type="button" disabled={!mutationAllowed} onClick={event => openAssignment(isCustomer ? 'customer' : 'person', id, event.currentTarget)} aria-label={`${title} ${isCustomer ? '담당자' : '고객사'} 설정`}>{isCustomer ? '담당자 설정' : '고객사 설정'}</button>{isCustomer && <button type="button" disabled={!mutationAllowed} onClick={event => openRegistration(item, event.currentTarget)} aria-label={`${title} 이름 변경`}>이름 변경</button>}</div>
        </div>;
      })}
      {!records.length && <p className="osan-admin-empty-list">검색 결과가 없습니다.</p>}
    </div>}
    <p className="osan-admin-policy">담당 고객사 설정은 메일·푸시·인앱 알림에 동일하게 적용됩니다. 프로젝트 조회 권한은 바뀌지 않습니다.</p>
    <details className="osan-admin-policy"><summary>배정 기준 확인</summary><ul><li>관리자도 담당 고객사를 지정해야 해당 고객사 알림을 받습니다.</li><li>새 고객사는 모두에게 미배정 상태로 등록됩니다.</li><li>신규 가입자는 가입 시점의 고객사를 모두 배정받습니다.</li></ul></details>
    {feedback && <p role="status">{feedback}</p>}
    <dialog ref={assignmentDialog} className="osan-admin-dialog" onCancel={event => { event.preventDefault(); if (!busy) closeAssignment(); }} aria-labelledby="osan-assignment-title">
      {editing && current && <><header><div><small>{editing.kind === 'customer' ? '알림 담당자 설정' : '담당 고객사 설정'}</small><h2 id="osan-assignment-title">{'name' in current ? current.name : current.displayName}</h2></div><button type="button" disabled={busy} onClick={closeAssignment} aria-label="배정 팝업 닫기">×</button></header>
        <p>{editing.kind === 'customer' ? '이 고객사의 알림을 받을 담당자를 선택해 주세요.' : '이 사용자가 알림을 받을 고객사를 선택해 주세요.'}</p>
        <label>검색<input autoFocus type="search" value={choiceSearch} placeholder={editing.kind === 'customer' ? '이름 또는 부서 검색' : '고객사명 검색'} onChange={event => setChoiceSearch(event.target.value)} /></label>
        <div className="osan-admin-selection-tools"><label><input type="checkbox" disabled={!visibleChoices.length || busy} checked={visibleChoices.length > 0 && selectedVisibleCount === visibleChoices.length}
          onChange={event => setSelected(current => {
            const ids = visibleChoices.map(item => 'userId' in item ? item.userId : item.customerId);
            return event.target.checked ? [...new Set([...current, ...ids])] : current.filter(id => !ids.includes(id));
          })} />검색 결과 전체 선택</label><span>검색 결과 {visibleChoices.length}{editing.kind === 'customer' ? '명' : '개'}</span></div>
        <fieldset className="osan-admin-options"><legend>배정 항목</legend>{visibleChoices.map(item => {
          const id = 'userId' in item ? item.userId : item.customerId;
          return <label key={id}><input type="checkbox" disabled={busy} checked={selected.includes(id)} onChange={() => toggleChoice(id)} /><strong>{'displayName' in item ? item.displayName : item.name}</strong>{'departmentName' in item && <small>{item.departmentName}</small>}</label>;
        })}{!visibleChoices.length && <p>검색 결과가 없습니다.</p>}</fieldset>
        <section className="osan-admin-chosen"><strong>선택한 {editing.kind === 'customer' ? '담당자' : '고객사'} {selected.length}{editing.kind === 'customer' ? '명' : '개'}</strong><div>{choices.filter(item => selected.includes('userId' in item ? item.userId : item.customerId)).map(item => {
          const id = 'userId' in item ? item.userId : item.customerId;
          const label = 'displayName' in item ? item.displayName : item.name;
          return <button type="button" disabled={busy} key={id} onClick={() => toggleChoice(id)} aria-label={`${label} 선택 해제`}>{label} ×</button>;
        })}</div>{!selected.length && <p>선택하지 않으면 일반 업무 알림을 받지 않습니다.</p>}</section>
        <p>메일·푸시·인앱 알림에 동일하게 적용됩니다.</p>{formError && <p role="alert">{formError}</p>}
        <footer><span>저장하면 배정이 적용됩니다.</span><div><button type="button" disabled={busy} onClick={closeAssignment}>취소</button><button type="button" className="osan-admin-primary" disabled={busy} onClick={() => void saveAssignment()}>{busy ? '저장 중…' : '저장'}</button></div></footer>
      </>}
    </dialog>
    <dialog ref={registrationDialog} className="osan-admin-dialog" onCancel={event => { event.preventDefault(); if (!busy) closeRegistration(); }} aria-labelledby="osan-registration-title">
      <form onSubmit={event => { event.preventDefault(); void saveCustomer(); }}><header><h2 id="osan-registration-title">{renaming ? '고객사 이름 변경' : '고객사 등록'}</h2><button type="button" disabled={busy} onClick={closeRegistration} aria-label="등록 팝업 닫기">×</button></header>
        <label>고객사명 *<input autoFocus required maxLength={200} value={name} onChange={event => setName(event.target.value)} placeholder="정식 고객사명을 입력해 주세요" /></label>
        {formError && <p role="alert">{formError}</p>}<p>새 고객사는 담당자가 없는 상태로 등록됩니다. 등록 후 담당자를 배정해 주세요.</p>
        <footer><span>오산 프로젝트에서 사용합니다.</span><div><button type="button" disabled={busy} onClick={closeRegistration}>취소</button><button type="submit" className="osan-admin-primary" disabled={busy}>{busy ? '저장 중…' : renaming ? '변경 저장' : '등록'}</button></div></footer>
      </form>
    </dialog>
  </section>;
}

export function OsanGateSettingsPage({ developmentUserKey, mutationAllowed = true }: { developmentUserKey?: string; mutationAllowed?: boolean }) {
  const [state, setState] = useState<Load<GateData>>({ kind: 'loading' });
  const [revision, setRevision] = useState(0);
  const [draft, setDraft] = useState<Gate[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: 'loading' });
    fetchJson<GateData>('/api/osan/admin/gates', developmentUserKey, { signal: controller.signal })
      .then(data => { if (!controller.signal.aborted) setState({ kind: 'ready', data }); })
      .catch(error => { if (!controller.signal.aborted) setState({ kind: 'error', message: errorMessage(error) }); });
    return () => controller.abort();
  }, [developmentUserKey, revision]);
  const data = state.kind === 'ready' ? state.data : null;
  const gates = useMemo(() => draft ?? data?.gates ?? [], [draft, data]);
  const changed = !!draft && !!data && JSON.stringify(draft) !== JSON.stringify(data.gates);
  const toggle = (stageSequence: number, departmentId: string) => setDraft(current => current?.map(gate => gate.stageSequence !== stageSequence ? gate : ({
    ...gate, departmentIds: gate.departmentIds.includes(departmentId)
      ? gate.departmentIds.filter(id => id !== departmentId) : [...gate.departmentIds, departmentId]
  })) ?? null);
  async function save() {
    if (!data || !draft || !changed || busy || !mutationAllowed) return;
    setBusy(true); setFeedback('');
    try {
      await fetchJson('/api/osan/admin/gates', developmentUserKey, { method: 'PUT',
        body: JSON.stringify({ expectedVersion: data.version,
          gates: draft.map(gate => ({ stageSequence: gate.stageSequence, departmentIds: gate.departmentIds })) }) });
      setDraft(null); setFeedback('Gate 설정을 저장했습니다.'); setRevision(value => value + 1);
    } catch (error) { setFeedback(errorMessage(error)); }
    finally { setBusy(false); }
  }
  return <section className="osan-admin-page osan-gate-page" aria-label="Gate 설정">
    <header className="osan-admin-heading"><div><h1>Gate 설정</h1><p>각 단계의 Gate 완료·조치 완료가 가능한 부서를 설정합니다.</p></div></header>
    <p className="osan-gate-exception">관리자는 부서 지정과 관계없이 모든 단계를 완료할 수 있습니다.</p>
    {state.kind === 'loading' && <p role="status">Gate 설정을 불러오는 중입니다.</p>}
    {state.kind === 'error' && <p role="alert">{state.message} <button type="button" onClick={() => setRevision(value => value + 1)}>다시 시도</button></p>}
    {data && <><div className="osan-gate-toolbar"><span className={changed ? 'unsaved' : ''}>{draft ? changed ? '변경 사항 있음 · 저장 필요' : '완료 가능한 Gate를 체크하세요' : '부서별 완료 권한 · 7개 Gate'}</span><div>
      {draft ? <><button type="button" disabled={busy} onClick={() => { setDraft(null); setFeedback('변경을 취소했습니다.'); }}>취소</button><button type="button" className="osan-admin-primary" disabled={!changed || busy} onClick={() => void save()}>{busy ? '저장 중…' : '변경 저장'}</button></>
        : <button type="button" className="osan-admin-primary" disabled={!mutationAllowed} onClick={() => { setDraft(data.gates.map(gate => ({ ...gate, departmentIds: [...gate.departmentIds] }))); setFeedback(''); }}>설정 변경</button>}</div></div>
      <div className="osan-gate-table-wrap"><table><caption>부서별 Gate 완료 가능 여부</caption><thead><tr><th scope="col">부서</th>{gates.map(gate => <th scope="col" key={gate.stageSequence} className={gate.name.endsWith('검사') ? 'split-stage' : undefined}>{gate.name.endsWith('검사') ? <><span>{gate.name.slice(0, -2)}</span><span>검사</span></> : gate.name}</th>)}</tr></thead>
        <tbody>{data.departments.map(department => <tr key={department.departmentId}><th scope="row">{department.name}</th>{gates.map(gate => {
          const allowed = gate.departmentIds.includes(department.departmentId);
          return <td key={gate.stageSequence}>{draft ? <label><input type="checkbox" aria-label={`${department.name} · ${gate.name} 완료 허용`} checked={allowed} disabled={busy} onChange={() => toggle(gate.stageSequence, department.departmentId)} /></label>
            : <span className="osan-gate-mark" aria-label={allowed ? '허용' : '미허용'}>{allowed ? '✓' : ''}</span>}</td>;
        })}</tr>)}</tbody></table></div></>}
    <p className="osan-admin-policy">공정 이상 발생 등록·사진 및 코멘트 수정 요청은 기존 권한을 유지합니다.<br />단계 순서, 동작검사 예외, 포장 선행조건은 기존 기준을 따릅니다.</p>
    {feedback && <p role={feedback.includes('저장했습니다') || feedback.includes('취소') ? 'status' : 'alert'}>{feedback}</p>}
  </section>;
}
