import { useCallback, useEffect, useRef, useState } from 'react';
import { agentsApi } from '../services/api';
import { wsClient } from '../services/wsClient';
import type { Agent } from '../types/protocol';

interface DashboardProps {
  token: string;
  onPaired: (sessionId: string, agent: Agent) => void;
  onLogout: () => void;
  onOpenAdmin: () => void;
}

export function DashboardPage({ token, onPaired, onLogout, onOpenAdmin }: DashboardProps) {
  const [agents, setAgents] = useState<Agent[]>([]);
  const [agentId, setAgentId] = useState('');
  const [pin, setPin] = useState('');
  const [message, setMessage] = useState('');
  const [loading, setLoading] = useState(false);
  const [isCreatingAgent, setIsCreatingAgent] = useState(false);
  const [newAgentName, setNewAgentName] = useState('');
  const [newAgentPlatform, setNewAgentPlatform] = useState('Windows');
  const [createdAgentInfo, setCreatedAgentInfo] = useState<{ id: string; secret: string } | null>(null);
  const agentsRef = useRef<Agent[]>([]);
  const agentIdRef = useRef('');
  useEffect(() => { agentsRef.current = agents; }, [agents]);
  useEffect(() => { agentIdRef.current = agentId; }, [agentId]);

  const isAdmin = () => {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const role = payload.role ?? payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
      return Array.isArray(role) ? role.includes('admin') : role === 'admin';
    } catch {
      return false;
    }
  };

  const load = useCallback(async () => {
    setLoading(true);
    setMessage('');
    try { setAgents(await agentsApi.list(token)); }
    catch (error) { setMessage(error instanceof Error ? error.message : 'Không tải được Agent.'); }
    finally { setLoading(false); }
  }, [token]);

  const handleCreateAgent = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newAgentName) return;
    try {
      setLoading(true);
      const res = await agentsApi.create(newAgentName, newAgentPlatform, token);
      setCreatedAgentInfo({ id: res.agentId, secret: res.agentSecretKey });
      setAgentId(res.agentId);
      setNewAgentName('');
      load();
    } catch (e) {
      setMessage(e instanceof Error ? e.message : 'Lỗi tạo Agent.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
    return wsClient.subscribe((msg) => {
      if (msg.action === 'PAIRING_RESULT') {
        if (msg.payload.success) {
          const selectedId = agentIdRef.current;
          const agent = agentsRef.current.find((x) => x.id === selectedId) ?? {
            id: selectedId,
            name: selectedId,
            platform: '',
          };
          onPaired(String(msg.payload.sessionId), agent);
        } else {
          setMessage(String(msg.payload.message ?? 'Không thể ghép cặp.'));
        }
      }
    });
  }, [load, onPaired]);

  return (
    <main className="dashboard">
      <header>
        <div>
          <p className="eyebrow">REMOTE CONTROL LAN</p>
          <h1>Chọn máy Target</h1>
        </div>
        <div className="header-actions">
          {isAdmin() && (
            <button className="compact warning" onClick={onOpenAdmin}>
              Admin Panel
            </button>
          )}
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
          PIN 6 số tự động hiển thị trên máy Target mỗi 4 phút, và hết hạn sau 5 phút.
        </p>
        <div className="row">
          <select
            value={agentId}
            onChange={(e) => setAgentId(e.target.value)}
          >
            <option value="">Chọn Agent</option>
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id}>
                {agent.name} ({agent.platform}) — ID: {agent.id.slice(0, 8)}...
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
            className="connect-btn"
            disabled={!agentId || pin.length !== 6 || loading}
            onClick={() => {
              if (!wsClient.send('REQUEST_PAIRING', { agentId, pin })) setMessage('WebSocket chưa kết nối; hãy thử lại sau.');
            }}
          >
            {loading ? 'Đang kết nối...' : 'Kết nối'}
          </button>
        </div>
        {message && <p className="error">{message}</p>}
      </section>

      <section className="panel">
        <div className="panel-title">
          <h2>Agent đã đăng ký</h2>
          <button className="compact" onClick={() => setIsCreatingAgent(!isCreatingAgent)}>
            {isCreatingAgent ? 'Hủy tạo' : '+ Tạo Agent'}
          </button>
        </div>

        {isCreatingAgent && (
          <form className="row" onSubmit={handleCreateAgent}>
            <input value={newAgentName} onChange={e => setNewAgentName(e.target.value)} placeholder="Tên máy (VD: May-01)" required />
            <select value={newAgentPlatform} onChange={e => setNewAgentPlatform(e.target.value)}>
              <option value="Windows">Windows</option>
              <option value="MacOS">MacOS</option>
            </select>
            <button type="submit" disabled={loading || !newAgentName}>Tạo mới</button>
          </form>
        )}

        {createdAgentInfo && (
          <div className="panel" style={{ background: '#082f49', border: '1px solid #0369a1' }}>
            <h3 style={{ margin: '0 0 0.5rem', color: '#7dd3fc' }}>Agent tạo thành công!</h3>
            <p style={{ margin: '0 0 0.5rem', fontSize: '0.9rem' }}>Vui lòng sao chép thông tin này vào <code>appsettings.json</code> của Agent mới. Bạn sẽ không thể xem lại mã bí mật này.</p>
            <div><small style={{ color: '#bae6fd' }}>AgentId:</small><br/><code style={{ userSelect: 'all' }}>{createdAgentInfo.id}</code></div>
            <div style={{ marginTop: '0.5rem' }}><small style={{ color: '#bae6fd' }}>AgentSecretKey:</small><br/><code style={{ userSelect: 'all' }}>{createdAgentInfo.secret}</code></div>
          </div>
        )}

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
            Chưa có Agent. Hãy bấm nút "+ Tạo Agent" ở trên.
          </p>
        )}
      </section>
    </main>
  );
}
