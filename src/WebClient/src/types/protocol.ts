export type Envelope = { type: string; action: string; sessionId?: string; agentId?: string; connectionId?: string; timestamp?: string; payload: Record<string, unknown> };
export type Agent = { id: string; name: string; platform: string; lastOnlineAt?: string };
export type ProcessInfo = { pid: number; name: string; path?: string; cpuPercent?: number; memoryMB?: number };
export type DirEntry = { name: string; isDirectory: boolean; sizeBytes?: number; modifiedAt?: string };
