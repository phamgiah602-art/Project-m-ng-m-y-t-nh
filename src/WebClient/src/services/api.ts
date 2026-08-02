let activeBaseUrl = import.meta.env.VITE_GATEWAY_HTTP_URL || '';

export function getGatewayBaseUrl(): string {
  return activeBaseUrl || (window.location.origin.includes(':5173') ? 'http://localhost:5001' : 'http://localhost:5000');
}

export let gatewayBaseUrl = activeBaseUrl || 'http://localhost:5000';

async function request<T>(path: string, method = 'GET', body?: unknown, token?: string, clearOnUnauthorized = true): Promise<T> {
  const candidates = activeBaseUrl
    ? [activeBaseUrl]
    : [
        'http://localhost:5001',
        'http://localhost:5000',
        window.location.origin,
      ];

  let response: Response | null = null;

  for (const targetUrl of candidates) {
    try {
      const fullUrl = `${targetUrl.replace(/\/$/, '')}${path}`;
      const res = await fetch(fullUrl, {
        method,
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: body ? JSON.stringify(body) : undefined,
      });

      if (res && (res.ok || res.status < 500)) {
        response = res;
        activeBaseUrl = targetUrl;
        gatewayBaseUrl = targetUrl;
        break;
      }
    } catch {
      // Continue trying next candidate
    }
  }

  if (!response) {
    throw new Error(
      `Không thể kết nối đến Gateway. Vui lòng kiểm tra xem Gateway (Terminal 1 hoặc Docker) đang chạy trên cổng 5000/5001.`
    );
  }

  if (!response.ok) {
    if (response.status === 401 && token && clearOnUnauthorized) {
      localStorage.removeItem('rclan-token');
      window.location.reload();
      throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
    }
    const errorBody = await response.json().catch(() => ({ message: response.statusText }));
    throw new Error(errorBody.message ?? 'Yêu cầu thất bại.');
  }

  return response.json() as Promise<T>;
}

export const authApi = {
  login: (username: string, password: string) =>
    request<{ success: boolean; token?: string; message: string }>('/api/auth/login', 'POST', { username, password }),
  register: (username: string, password: string) =>
    request<{ success: boolean; token?: string; message: string }>('/api/auth/register', 'POST', { username, password }),
  reverify: (sessionId: string, password: string, token: string) =>
    request<{ confirmationToken: string }>('/api/auth/reverify-password', 'POST', { sessionId, password }, token, false),
};

export const agentsApi = {
  list: (token: string) =>
    request<import('../types/protocol').Agent[]>('/api/agents', 'GET', undefined, token),
  create: (agentName: string, platform: string, token: string) =>
    request<{ agentId: string; agentSecretKey: string; agentName: string }>('/api/agents', 'POST', { agentName, platform }, token),
};

export const adminApi = {
  listUsers: (token: string) =>
    request<{ id: string; username: string; isAdmin: boolean; createdAt: string; failedLoginCount: number; lockedUntil: string | null }[]>('/api/admin/users', 'GET', undefined, token),
  listAgents: (token: string) =>
    request<{ id: string; name: string; platform: string; lastOnlineAt: string | null; lastSeenIp: string | null; isOnline: boolean; hasPairingPin: boolean; pairingPinExpiresAt: string | null }[]>('/api/admin/agents', 'GET', undefined, token),
  deleteUser: (id: string, token: string) =>
    request<{ message: string }>(`/api/admin/users/${id}`, 'DELETE', undefined, token),
  deleteAgent: (id: string, token: string) =>
    request<{ message: string }>(`/api/admin/agents/${id}`, 'DELETE', undefined, token),
};
