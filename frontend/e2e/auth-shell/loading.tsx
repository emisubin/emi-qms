import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { AuthInitializationScreen } from '../../src/App';
import '../../src/styles.css';
import '../../src/design-system/tokens.css';
import '../../src/shared-shell.css';
import '../../src/auth-figma.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthInitializationScreen rememberSession={false} />
  </StrictMode>
);
