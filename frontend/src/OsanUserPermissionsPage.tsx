import { useEffect, useMemo, useRef, useState } from 'react';
import { fetchJson } from './api';
import { OsanButton } from './OsanButton';
import { OsanFilterToolbar } from './OsanFilterToolbar';
import { OsanMenuHeading } from './OsanMenuHeading';
import { OsanMultiSelectFilter } from './OsanMultiSelectFilter';
import { OsanInlineState } from './OsanUiPrimitives';
import './OsanAdminPage.css';
import './OsanUserPermissionsPage.css';

type UserPermission = {
  userId: string;
  displayName: string;
  departmentCode: string | null;
  departmentName: string | null;
  allowed: boolean;
  isAdministrator: boolean;
  version: number;
};

type UserPermissionData = { items: UserPermission[] };
type Load = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'ready'; data: UserPermissionData };
type PermissionFilter = 'allowed' | 'denied';

function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : '사용자 권한을 불러오지 못했습니다.';
}

export function OsanUserPermissionsPage({ developmentUserKey, mutationAllowed = true }: {
  developmentUserKey?: string;
  mutationAllowed?: boolean;
}) {
  const [state, setState] = useState<Load>({ kind: 'loading' });
  const [revision, setRevision] = useState(0);
  const [searchDraft, setSearchDraft] = useState('');
  const [search, setSearch] = useState('');
  const [department, setDepartment] = useState<string[]>([]);
  const [permission, setPermission] = useState<PermissionFilter[]>([]);
  const [draft, setDraft] = useState<Record<string, boolean>>({});
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState('');
  const [conflict, setConflict] = useState(false);
  const scope = useRef(0);

  useEffect(() => {
    const requestScope = ++scope.current;
    const controller = new AbortController();
    setBusy(false);
    setFeedback('');
    setConflict(false);
    setState({ kind: 'loading' });
    fetchJson<UserPermissionData>('/api/osan/admin/user-project-create-permissions', developmentUserKey, {
      signal: controller.signal
    }).then(data => {
      if (!controller.signal.aborted && requestScope === scope.current) {
        setState({ kind: 'ready', data });
        setDraft({});
      }
    }).catch(error => {
      if (!controller.signal.aborted && requestScope === scope.current) setState({ kind: 'error', message: errorMessage(error) });
    });
    return () => controller.abort();
  }, [developmentUserKey, revision]);

  const items = state.kind === 'ready' ? state.data.items : [];
  const departments = useMemo(() => [...new Set(items.map(item => item.departmentName).filter((value): value is string => !!value))]
    .sort((left, right) => left.localeCompare(right, 'ko')), [items]);
  const currentAllowed = (item: UserPermission) => draft[item.userId] ?? item.allowed;
  const changed = items.filter(item => !item.isAdministrator && currentAllowed(item) !== item.allowed);
  const visible = items.filter(item => {
    const keyword = search.trim().toLocaleLowerCase('ko');
    if (keyword && !item.displayName.toLocaleLowerCase('ko').includes(keyword)) return false;
    if (department.length && (!item.departmentName || !department.includes(item.departmentName))) return false;
    if (permission.length && !permission.includes(currentAllowed(item) ? 'allowed' : 'denied')) return false;
    return true;
  });

  async function save() {
    if (!changed.length || busy || !mutationAllowed) return;
    const requestScope = scope.current;
    setBusy(true);
    setFeedback('');
    setConflict(false);
    try {
      const result = await fetchJson<UserPermissionData>(
        '/api/osan/admin/user-project-create-permissions',
        developmentUserKey,
        {
          method: 'PUT',
          body: JSON.stringify({ items: changed.map(item => ({
            userId: item.userId,
            allowed: currentAllowed(item),
            expectedVersion: item.version
          })) })
        });
      if (requestScope === scope.current) {
        setState({ kind: 'ready', data: result });
        setDraft({});
        setFeedback('개인별 프로젝트 생성 권한을 저장했습니다.');
      }
    } catch (error) {
      if (requestScope === scope.current) {
        setFeedback((error as { status?: number })?.status === 409
          ? `${errorMessage(error)} 최신 권한을 다시 불러오면 화면에서 변경한 내용은 취소됩니다.`
          : errorMessage(error));
        setConflict((error as { status?: number })?.status === 409);
      }
    } finally {
      if (requestScope === scope.current) setBusy(false);
    }
  }

  return <section className="osan-page osan-admin-page osan-user-permissions-page" aria-label="개인별 권한 설정">
    <OsanMenuHeading title="개인별 권한 설정" description="사용자별 프로젝트 생성 권한을 설정합니다." />
    <OsanFilterToolbar
      search={searchDraft}
      onSearchChange={setSearchDraft}
      onSearch={() => setSearch(searchDraft)}
      searchLabel="이름 검색"
      placeholder="이름 검색"
      active={department.length > 0 || permission.length > 0}
      onReset={() => { setDepartment([]); setPermission([]); }}>
      <OsanMultiSelectFilter label="부서" options={departments.map(name => ({ value: name, label: name }))}
        values={department} onApply={setDepartment} />
      <OsanMultiSelectFilter label="프로젝트 생성" options={[{ value: 'allowed', label: '허용' }, { value: 'denied', label: '미허용' }]}
        values={permission} onApply={values => setPermission(values as PermissionFilter[])} align="end" />
    </OsanFilterToolbar>

    <div className="osan-user-permission-toolbar">
      <span aria-live="polite">사용자 {visible.length}명{changed.length ? ` · ${changed.length}명 변경` : ''}</span>
      <div>
        {changed.length > 0 && <OsanButton disabled={busy} onClick={() => { setDraft({}); setFeedback('변경을 취소했습니다.'); }}>취소</OsanButton>}
        <OsanButton tone="primary" disabled={!changed.length || busy || !mutationAllowed} onClick={() => void save()}>
          {busy ? '저장 중…' : '변경 저장'}
        </OsanButton>
      </div>
    </div>

    {state.kind === 'loading' && <OsanInlineState kind="loading">사용자 권한을 불러오는 중입니다.</OsanInlineState>}
    {state.kind === 'error' && <OsanInlineState kind="error" onRetry={() => setRevision(value => value + 1)}>{state.message}</OsanInlineState>}
    {state.kind === 'ready' && <div className="osan-gate-table-wrap osan-user-permission-table-wrap">
      <table><caption>사용자별 프로젝트 생성 권한</caption><thead><tr><th scope="col">사용자</th><th scope="col">부서</th><th scope="col">프로젝트 생성</th></tr></thead>
        <tbody>{visible.map(item => <tr key={item.userId} className={currentAllowed(item) !== item.allowed ? 'changed' : undefined}>
          <th scope="row">{item.displayName}</th>
          <td>{item.departmentName ?? '미지정'}</td>
          <td>{item.isAdministrator ? <span className="osan-user-permission-admin">항상 허용</span> : <label>
            <input type="checkbox" aria-label={`${item.displayName} 프로젝트 생성 허용`} checked={currentAllowed(item)} disabled={busy || !mutationAllowed}
              onChange={event => setDraft(current => ({ ...current, [item.userId]: event.target.checked }))} />
          </label>}</td>
        </tr>)}{visible.length === 0 && <tr><td colSpan={3} className="osan-user-permission-empty">검색 결과가 없습니다.</td></tr>}</tbody>
      </table>
    </div>}
    <p className="osan-admin-policy">프로젝트 단건 등록과 Excel 등록에 동일하게 적용됩니다. 프로젝트 조회·수정 및 공정 진행 권한은 바뀌지 않습니다.</p>
    {feedback && <div className="osan-user-permission-feedback">
      <p role={!conflict && (feedback.includes('저장했습니다') || feedback === '변경을 취소했습니다.') ? 'status' : 'alert'}>{feedback}</p>
      {conflict && <OsanButton onClick={() => { setDraft({}); setConflict(false); setRevision(value => value + 1); }}>
        최신 권한 다시 불러오기
      </OsanButton>}
    </div>}
  </section>;
}
