import { useEffect, useState } from 'react';
import { agentsApi } from '../services/api';
import { wsClient } from '../services/wsClient';
import type { Agent } from '../types/protocol';

interface DashboardProps {
  token: string;
  onPaired: (sessionId: string, agent: Agent) => void;
  onLogout: () => void;
}

export function DashboardPage({ token, onPaired, onLogout }: DashboardProps) {
  const [agents, setAgents] = useState<Agent[]>([]);
  const [agentId, setAgentId] = useState('');
  const [pin, setPin] = useState('');
  const [message, setMessage] = useState('');
  const [loading, setLoading] = useState(false);

  const load = () => {
    setLoading(true);
    setMessage('');
    agentsApi
      .list(token)
      .then(setAgents)
      .catch((error) => setMessage(error.message))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    return wsClient.subscribe((msg) => {
      if (msg.action === 'PAIRING_RESULT') {
        if (msg.payload.success) {
          const agent = agents.find((x) => x.id === agentId) ?? {
            id: agentId,
            name: agentId,
            platform: '',
          };
          onPaired(String(msg.payload.sessionId), agent);
        } else {
          setMessage(String(msg.payload.message ?? 'Không thể ghép cặp.'));
        }
      }
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [agentId, agents, token, onPaired]);

  return (
    <main className="dashboard">
      <header>
        <div>
          <p className="eyebrow">REMOTE CONTROL LAN</p>
          <h1>Chọn máy Target</h1>
        </div>
        <div className="header-actions">
          <button onClick={load} disabled={loading}>
            {loading ? 'Đang tải...' : 'Làm mới danh sách'}
          </button>
          <button className="logout-btn" onClick={onLogout}>
            Đăng xuất
          </button>
        </div>
      </header>

      <section className="panel">
        <h2>Ghép cặp bằng PIN</h2>
        <p className="hint">
          PIN 6 số phải được hiển thị trực tiếp trên máy Target, và hết hạn sau
          5 phút.
        </p>
        <div className="row">
          <select
            value={agentId}
            onChange={(e) => setAgentId(e.target.value)}
          >
            <option value="">Chọn Agent</option>
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id}>
                {agent.name} — {agent.platform}
              </option>
            ))}
          </select>
          <input
            value={pin}
            onChange={(e) =>
              setPin(e.target.value.replace(/\D/g, '').slice(0, 6))
            }
            placeholder="PIN 6 số"
            inputMode="numeric"
          />
          <button
            disabled={!agentId || pin.length !== 6}
            onClick={() =>
              wsClient.send('REQUEST_PAIRING', { agentId, pin })
            }
          >
            Kết nối
          </button>
        </div>
        {message && <p className="error">{message}</p>}
      </section>

      <section className="panel">
        <h2>Agent đã đăng ký</h2>
        {agents.length ? (
          <ul className="agent-list">
            {agents.map((agent) => (
              <li key={agent.id}>
                <strong>{agent.name}</strong>
                <span>{agent.platform}</span>
                <code>{agent.id}</code>
                <span>
                  {agent.lastOnlineAt
                    ? `Online gần nhất: ${new Date(agent.lastOnlineAt).toLocaleString()}`
                    : 'Chưa online'}
                </span>
              </li>
            ))}
          </ul>
        ) : (
          <p className="empty">
            Chưa có Agent. Tạo cấu hình qua API{' '}
            <code>POST /api/agents</code>, sau đó sao chép AgentId và
            AgentSecretKey vào appsettings của Agent.
          </p>
        )}
      </section>
    </main>
  );
}
