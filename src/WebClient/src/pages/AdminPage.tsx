import { useEffect, useState } from 'react';
import { adminApi } from '../services/api';

type UserInfo = { id: string; username: string; isAdmin: boolean; createdAt: string; failedLoginCount: number; lockedUntil: string | null };
type AgentInfo = { id: string; name: string; platform: string; lastOnlineAt: string | null; lastSeenIp: string | null; isOnline: boolean; hasPairingPin: boolean; pairingPinExpiresAt: string | null };

export function AdminPage({ token, onBack }: { token: string; onBack: () => void }) {
  const [users, setUsers] = useState<UserInfo[]>([]);
  const [agents, setAgents] = useState<AgentInfo[]>([]);
  const [message, setMessage] = useState('');
  const [tab, setTab] = useState<'users' | 'agents'>('users');
  const [loading, setLoading] = useState(false);

  const loadData = async () => {
    setLoading(true);
    setMessage('');
    try {
      const [u, a] = await Promise.all([adminApi.listUsers(token), adminApi.listAgents(token)]);
      setUsers(u);
      setAgents(a);
    } catch (e) {
      setMessage(e instanceof Error ? e.message : 'Lỗi tải dữ liệu.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { loadData(); }, [token]);

  const deleteUser = async (id: string, username: string) => {
    if (!confirm(`Xóa user "${username}"?`)) return;
    try {
      const result = await adminApi.deleteUser(id, token);
      setMessage(result.message);
      loadData();
    } catch (e) {
      setMessage(e instanceof Error ? e.message : 'Lỗi xóa user.');
    }
  };

  const deleteAgent = async (id: string, name: string) => {
    if (!confirm(`Xóa agent "${name}"? Các phiên liên quan sẽ bị ảnh hưởng.`)) return;
    try {
      const result = await adminApi.deleteAgent(id, token);
      setMessage(result.message);
      loadData();
    } catch (e) {
      setMessage(e instanceof Error ? e.message : 'Lỗi xóa agent.');
    }
  };

  return (
    <main className="dashboard">
      <header>
        <div>
          <button className="link" onClick={onBack}>← Quay lại Dashboard</button>
          <p className="eyebrow">ADMIN PANEL</p>
          <h1>Quản lý hệ thống</h1>
        </div>
        <div className="header-actions">
          <button onClick={loadData} disabled={loading}>
            {loading ? 'Đang tải...' : 'Làm mới'}
          </button>
        </div>
      </header>

      <div className="admin-tabs">
        <button
          className={`tab-btn ${tab === 'users' ? 'active' : ''}`}
          onClick={() => setTab('users')}
        >
          👤 Tài khoản ({users.length})
        </button>
        <button
          className={`tab-btn ${tab === 'agents' ? 'active' : ''}`}
          onClick={() => setTab('agents')}
        >
          🖥️ Agent ({agents.length})
        </button>
      </div>

      {message && <p className={message.includes('Đã xóa') ? 'success-msg' : 'error'}>{message}</p>}

      {tab === 'users' && (
        <section className="panel">
          <h2>Danh sách tài khoản</h2>
          {users.length ? (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Username</th>
                    <th>Vai trò</th>
                    <th>Ngày tạo</th>
                    <th>Đăng nhập sai</th>
                    <th>Trạng thái</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {users.map((u) => (
                    <tr key={u.id}>
                      <td><strong>{u.username}</strong></td>
                      <td>
                        <span className={`role-badge ${u.isAdmin ? 'admin' : 'operator'}`}>
                          {u.isAdmin ? 'Admin' : 'Operator'}
                        </span>
                      </td>
                      <td>{new Date(u.createdAt).toLocaleDateString('vi-VN')}</td>
                      <td>{u.failedLoginCount}</td>
                      <td>
                        {u.lockedUntil && new Date(u.lockedUntil) > new Date() ? (
                          <span className="status-locked">🔒 Bị khóa</span>
                        ) : (
                          <span className="status-active">✓ Hoạt động</span>
                        )}
                      </td>
                      <td>
                        {!u.isAdmin && (
                          <button className="compact danger" onClick={() => deleteUser(u.id, u.username)}>
                            Xóa
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="empty">Không có tài khoản nào.</p>
          )}
        </section>
      )}

      {tab === 'agents' && (
        <section className="panel">
          <h2>Danh sách Agent</h2>
          {agents.length ? (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Tên Agent</th>
                    <th>Platform</th>
                    <th>Trạng thái</th>
                    <th>Online gần nhất</th>
                    <th>IP</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {agents.map((a) => (
                    <tr key={a.id}>
                      <td><strong>{a.name}</strong></td>
                      <td>{a.platform}</td>
                      <td>
                        {a.isOnline ? (
                          <span className="status-active">🟢 Online</span>
                        ) : a.lastOnlineAt ? (
                          <span className="status-idle">🟡 Offline (đã từng kết nối)</span>
                        ) : (
                          <span className="status-locked">🔴 Offline</span>
                        )}
                      </td>
                      <td>{a.lastOnlineAt ? new Date(a.lastOnlineAt).toLocaleString('vi-VN') : '—'}</td>
                      <td><code>{a.lastSeenIp ?? '—'}</code></td>
                      <td>
                        <button className="compact danger" onClick={() => deleteAgent(a.id, a.name)}>
                          Xóa
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="empty">Chưa có Agent nào.</p>
          )}
        </section>
      )}
    </main>
  );
}
