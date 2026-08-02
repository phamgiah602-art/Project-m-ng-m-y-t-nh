import { useCallback, useEffect, useState } from 'react';
import type { Agent } from './types/protocol';
import { wsClient } from './services/wsClient';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { ControlPage } from './pages/ControlPage';
import { AdminPage } from './pages/AdminPage';

export default function App() {
  const [token, setToken] = useState(() => localStorage.getItem('rclan-token'));
  const [connectionState, setConnectionState] = useState('connecting');
  const [control, setControl] = useState<{ sessionId: string; agent: Agent }>();
  const [showAdmin, setShowAdmin] = useState(false);

  const logout = useCallback(() => {
    localStorage.removeItem('rclan-token');
    wsClient.close();
    setToken(null);
    setControl(undefined);
  }, []);

  useEffect(() => {
    if (!token) return;
    wsClient.connect(token);
    return wsClient.onState(setConnectionState);
  }, [token]);

  useEffect(() => wsClient.subscribe(message => {
    if (message.action === 'SESSION_ENDED' || message.action === 'AGENT_DISCONNECTED') setControl(undefined);
  }), []);

  const paired = useCallback((sessionId: string, agent: Agent) => setControl({ sessionId, agent }), []);
  const leaveControl = useCallback(() => {
    if (control) wsClient.send('END_SESSION', {}, control.sessionId, control.agent.id);
    setControl(undefined);
  }, [control]);

  if (!token) return <LoginPage onAuthenticated={setToken} />;

  if (showAdmin) return <AdminPage token={token} onBack={() => setShowAdmin(false)} />;

  if (!control)
    return (
      <DashboardPage
        token={token}
        onPaired={paired}
        onLogout={logout}
        onOpenAdmin={() => setShowAdmin(true)}
      />
    );

  return (
    <ControlPage
      token={token}
      sessionId={control.sessionId}
      agent={control.agent}
      connectionState={connectionState}
      onLeave={leaveControl}
    />
  );
}
