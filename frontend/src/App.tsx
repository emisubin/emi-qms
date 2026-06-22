import { useEffect, useState } from 'react';
import { getCurrentUser, getReadyHealth } from './api';
import type { ReadyHealth } from './health';
import type { CurrentUser } from './identity';

type ApiState =
  | { kind: 'loading' }
  | { kind: 'ready'; health: ReadyHealth }
  | { kind: 'error'; message: string };

type UserState =
  | { kind: 'loading' }
  | { kind: 'ready'; user: CurrentUser }
  | { kind: 'error'; message: string };

const menuItems = [
  { label: '프로젝트 조회', permission: 'projects.read' },
  { label: '사용자 관리', permission: 'users.manage' },
  { label: '생산 계획', permission: 'production.plan' },
  { label: '제조 입력', permission: 'manufacturing.update' },
  { label: '품질 검사', permission: 'quality.inspect' },
  { label: '출하 관리', permission: 'logistics.ship' }
];

export function App() {
  const [apiState, setApiState] = useState<ApiState>({ kind: 'loading' });
  const [userState, setUserState] = useState<UserState>({ kind: 'loading' });

  useEffect(() => {
    let isMounted = true;

    getReadyHealth()
      .then((health) => {
        if (isMounted) {
          setApiState({ kind: 'ready', health });
        }
      })
      .catch((error: unknown) => {
        if (isMounted) {
          setApiState({
            kind: 'error',
            message: error instanceof Error ? error.message : 'API 상태를 확인할 수 없습니다.'
          });
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  useEffect(() => {
    let isMounted = true;

    getCurrentUser()
      .then((user) => {
        if (isMounted) {
          setUserState({ kind: 'ready', user });
        }
      })
      .catch((error: unknown) => {
        if (isMounted) {
          setUserState({
            kind: 'error',
            message: error instanceof Error ? error.message : '개발 사용자를 확인할 수 없습니다.'
          });
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const apiStatus = apiState.kind === 'ready' ? apiState.health.status : apiState.kind;
  const databaseStatus = apiState.kind === 'ready' ? apiState.health.database.reason : '-';
  const visibleMenuItems = userState.kind === 'ready'
    ? menuItems.filter((item) => userState.user.permissions.includes(item.permission))
    : [];

  return (
    <main className="shell">
      <section className="status-panel" aria-labelledby="page-title">
        <div>
          <p className="eyebrow">EMI QMS</p>
          <h1 id="page-title">개발환경 상태</h1>
        </div>

        <dl className="status-grid">
          <div>
            <dt>API</dt>
            <dd data-state={apiStatus}>{apiStatus}</dd>
          </div>
          <div>
            <dt>Database</dt>
            <dd>{databaseStatus}</dd>
          </div>
        </dl>

        {apiState.kind === 'error' ? <p role="alert" className="error-text">{apiState.message}</p> : null}
      </section>

      <section className="status-panel" aria-labelledby="identity-title">
        <div>
          <p className="eyebrow">Authorization</p>
          <h2 id="identity-title">개발 사용자 권한</h2>
        </div>

        {userState.kind === 'ready' ? (
          <>
            <dl className="status-grid">
              <div>
                <dt>User</dt>
                <dd>{userState.user.developmentUserKey}</dd>
              </div>
              <div>
                <dt>Role</dt>
                <dd>{userState.user.roles.join(', ') || '-'}</dd>
              </div>
            </dl>

            <nav aria-label="권한 메뉴">
              <ul className="menu-list">
                {visibleMenuItems.map((item) => (
                  <li key={item.permission}>{item.label}</li>
                ))}
              </ul>
            </nav>
          </>
        ) : null}

        {userState.kind === 'loading' ? <p className="muted-text">loading</p> : null}
        {userState.kind === 'error' ? <p role="alert" className="error-text">{userState.message}</p> : null}
      </section>
    </main>
  );
}
