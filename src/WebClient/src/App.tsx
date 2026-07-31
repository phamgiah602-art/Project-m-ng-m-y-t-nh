import { useEffect, useState } from 'react';
import type { Agent } from './types/protocol';
import { wsClient } from './services/wsClient';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { ControlPage } from './pages/ControlPage';
export default function App() { const [token, setToken] = useState(() => localStorage.getItem('rclan-token')); const [connectionState, setConnectionState] = useState('connecting'); const [control, setControl] = useState<{ sessionId: string; agent: Agent }>(); useEffect(() => { if (!token) return; wsClient.connect(token); return wsClient.onState(setConnectionState); }, [token]); if (!token) return <LoginPage onAuthenticated={setToken} />; if (!control) return <DashboardPage token={token} onPaired={(sessionId, agent) => setControl({ sessionId, agent })} />; return <ControlPage token={token} sessionId={control.sessionId} agent={control.agent} connectionState={connectionState} onLeave={() => setControl(undefined)} />; }
