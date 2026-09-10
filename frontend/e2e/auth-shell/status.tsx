import { createRoot } from 'react-dom/client';
import { AuthInitializationScreen, AuthStatusScreen } from '../../src/App';
import '../../src/styles.css';
import '../../src/design-system/tokens.css';
import '../../src/design-system/wireframe.css';
import '../../src/auth-figma.css';
const state = new URLSearchParams(location.search).get('state');
createRoot(document.getElementById('root')!).render(state === 'loading' ? <AuthInitializationScreen /> :
  <AuthStatusScreen state={state === 'pending' ? 'pending' : state === 'error' ? 'error' : 'reauth'}
    message={state === 'error' ? '로그인을 완료하지 못했습니다. 다시 시도해 주세요.' : undefined}
    onAction={() => undefined} />);
