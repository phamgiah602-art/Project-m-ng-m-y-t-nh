import { useEffect, useState } from 'react';
import { wsClient } from '../services/wsClient';
type Entry = { text: string; windowTitle?: string; timestamp: string };
export function KeyloggerPanel({ sessionId }: { sessionId: string }) {
  const [enabled, setEnabled] = useState(false);
  const [notice, setNotice] = useState('Chưa yêu cầu quyền.');
  const [entries, setEntries] = useState<Entry[]>([]);

  useEffect(() => wsClient.subscribe(message => {
    if (message.sessionId !== sessionId) return;
    if (message.action === 'KEYLOGGER_CONSENT_RESULT') {
      const accepted = Boolean(message.payload.accepted);
      setEnabled(accepted);
      setNotice(accepted ? 'Target đã đồng ý. Ghi nhận đang bật.' : 'Target đã từ chối.');
    }
    if (message.action === 'DISABLE_KEYLOGGER_RESULT') {
      setEnabled(false);
      setNotice('Ghi nhận bàn phím đã tắt.');
    }
    if (message.action === 'KEYLOG_BATCH') {
      setEntries(old => [...old, ...((message.payload.entries ?? []) as Entry[])].slice(-200));
    }
  }), [sessionId]);

  return (
    <section className="panel">
      <div className="panel-title">
        <h2>Ghi nhận bàn phím</h2>
        <button
          className={enabled ? 'danger' : ''}
          onClick={() => wsClient.send(enabled ? 'DISABLE_KEYLOGGER' : 'ENABLE_KEYLOGGER', {}, sessionId)}
        >
          {enabled ? 'Tắt' : 'Yêu cầu bật'}
        </button>
      </div>
      <p className="hint">Chỉ hoạt động khi người dùng Target chấp thuận tại chỗ và đã cấp quyền Accessibility.</p>
      <p>{notice}</p>
      <pre className="keylog">
        {entries.map(x => `[${new Date(x.timestamp).toLocaleTimeString()}] ${x.windowTitle ?? 'Unknown'}: ${x.text}`).join('\n') || 'Chưa có dữ liệu'}
      </pre>
    </section>
  );
}
