import { StrictMode, useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { MsalProvider } from '@azure/msal-react';
import { App, AuthInitializationScreen } from './App';
import { PwaInstallProvider } from './PwaInstallExperience';
import {
  createMsalInstance,
  getRememberSessionPreference,
  isEntraAuthMode,
  registerInteractiveLoginAuditTracker,
  setRememberSessionPreference
} from './auth';
import './styles.css';
import './design-system/tokens.css';
import './design-system/wireframe.css';

const root = createRoot(document.getElementById('root')!);

function EntraRoot() {
  const [rememberSession, setRememberSession] = useState(() => getRememberSessionPreference());
  const [instance, setInstance] = useState(() => createMsalInstance(rememberSession));
  const [initializedInstance, setInitializedInstance] = useState<typeof instance | null>(null);

  useEffect(() => {
    let cancelled = false;
    const auditCallbackId = registerInteractiveLoginAuditTracker(instance, rememberSession);
    void instance.initialize().then(() => {
      if (!cancelled) {
        setInitializedInstance(instance);
      }
    });

    return () => {
      cancelled = true;
      if (auditCallbackId) instance.removeEventCallback(auditCallbackId);
    };
  }, [instance, rememberSession]);

  const handleRememberSessionChange = (nextRememberSession: boolean) => {
    setRememberSessionPreference(nextRememberSession);
    setRememberSession(nextRememberSession);
    setInstance(createMsalInstance(nextRememberSession));
  };

  if (initializedInstance !== instance) {
    return <AuthInitializationScreen rememberSession={rememberSession} />;
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
      <PwaInstallProvider>
        {isEntraAuthMode ? (
          <EntraRoot />
        ) : (
          <App />
        )}
      </PwaInstallProvider>
    </StrictMode>
  );
}

renderApp();
