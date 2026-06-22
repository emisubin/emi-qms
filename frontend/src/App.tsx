import { useEffect, useState } from 'react';
import { getReadyHealth } from './api';
import type { ReadyHealth } from './health';

type ApiState =
  | { kind: 'loading' }
  | { kind: 'ready'; health: ReadyHealth }
  | { kind: 'error'; message: string };

export function App() {
  const [apiState, setApiState] = useState<ApiState>({ kind: 'loading' });

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

  const apiStatus = apiState.kind === 'ready' ? apiState.health.status : apiState.kind;
  const databaseStatus = apiState.kind === 'ready' ? apiState.health.database.reason : '-';

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
    </main>
  );
}
