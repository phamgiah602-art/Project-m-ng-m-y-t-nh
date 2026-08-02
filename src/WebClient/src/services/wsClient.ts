import { getGatewayBaseUrl } from './api';
import type { Envelope } from '../types/protocol';
type Listener = (message: Envelope) => void;
export class WsClient {
  private socket?: WebSocket; private token?: string; private retry = 1000; private timer?: number; private listeners = new Set<Listener>(); private stateListeners = new Set<(state: string) => void>();
  connect(token: string) { this.token = token; this.close(); const baseUrl = getGatewayBaseUrl(); const url = new URL(baseUrl.replace(/^http/, 'ws') + '/ws'); this.state('connecting'); this.socket = new WebSocket(url, ['bearer', token]); this.socket.onopen = () => { this.retry = 1000; this.state('connected'); }; this.socket.onmessage = event => { try { const message = JSON.parse(event.data) as Envelope; if (message.action === 'PING') this.send('PONG', {}, message.sessionId); this.listeners.forEach(listener => listener(message)); } catch { /* ignore malformed server packet */ } }; this.socket.onclose = () => { this.state('reconnecting'); this.timer = window.setTimeout(() => this.token && this.connect(this.token), this.retry); this.retry = Math.min(this.retry * 2, 30000); }; this.socket.onerror = () => this.socket?.close(); }
  close() { if (this.timer) window.clearTimeout(this.timer); this.timer = undefined; if (this.socket && this.socket.readyState < WebSocket.CLOSING) { this.socket.onclose = null; this.socket.close(); } this.socket = undefined; }
  send(action: string, payload: Record<string, unknown>, sessionId?: string, agentId?: string) { if (this.socket?.readyState !== WebSocket.OPEN) return false; this.socket.send(JSON.stringify({ type: 'COMMAND', action, sessionId, agentId, timestamp: new Date().toISOString(), payload })); return true; }
  subscribe(listener: Listener) { this.listeners.add(listener); return () => { this.listeners.delete(listener); }; }
  onState(listener: (state: string) => void) { this.stateListeners.add(listener); return () => { this.stateListeners.delete(listener); }; }
  private state(value: string) { this.stateListeners.forEach(listener => listener(value)); }
}
export const wsClient = new WsClient();
