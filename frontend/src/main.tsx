import { StrictMode, useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { MsalProvider } from '@azure/msal-react';
import { App } from './App';
import {
  createMsalInstance,
  getRememberSessionPreference,
  isEntraAuthMode,
  setRememberSessionPreference
} from './auth';
import './styles.css';

const root = createRoot(document.getElementById('root')!);

function EntraRoot() {
  const [rememberSession, setRememberSession] = useState(() => getRememberSessionPreference());
  const [instance, setInstance] = useState(() => createMsalInstance(rememberSession));
  const [initializedInstance, setInitializedInstance] = useState<typeof instance | null>(null);

  useEffect(() => {
    let cancelled = false;
    void instance.initialize().then(() => {
      if (!cancelled) {
        setInitializedInstance(instance);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [instance]);

  const handleRememberSessionChange = (nextRememberSession: boolean) => {
    setRememberSessionPreference(nextRememberSession);
    setRememberSession(nextRememberSession);
    setInstance(createMsalInstance(nextRememberSession));
  };

  if (initializedInstance !== instance) {
    return null;
  }

  return (
    <MsalProvider instance={instance}>
      <App
        rememberSession={rememberSession}
        onRememberSessionChange={handleRememberSessionChange}
      />
    </MsalProvider>
  );
}

function renderApp() {
  root.render(
    <StrictMode>
      {isEntraAuthMode ? (
        <EntraRoot />
      ) : (
        <App />
      )}
    </StrictMode>
  );
}

renderApp();
