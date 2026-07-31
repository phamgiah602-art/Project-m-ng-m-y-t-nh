const baseUrl = import.meta.env.VITE_GATEWAY_HTTP_URL ?? 'http://localhost:5050';
export const gatewayBaseUrl = baseUrl;

async function request<T>(path: string, method = 'GET', body?: unknown, token?: string): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!response.ok) {
    // Nếu server trả 401 (Unauthorized) và đang có token → token hết hạn
    if (response.status === 401 && token) {
      localStorage.removeItem('rclan-token');
      window.location.reload(); // Tự động chuyển về trang đăng nhập
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
    request<{ confirmationToken: string }>('/api/auth/reverify-password', 'POST', { sessionId, password }, token),
};

export const agentsApi = {
  list: (token: string) =>
    request<import('../types/protocol').Agent[]>('/api/agents', 'GET', undefined, token),
  create: (agentName: string, platform: string, token: string) =>
    request<{ agentId: string; agentSecretKey: string; agentName: string }>('/api/agents', 'POST', { agentName, platform }, token),
};
