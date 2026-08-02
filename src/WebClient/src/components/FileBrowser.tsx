import { useEffect, useRef, useState } from 'react';
import { wsClient } from '../services/wsClient';
import type { DirEntry } from '../types/protocol';

const chunkSize = 64 * 1024;
const sha256 = async (bytes: ArrayBuffer) =>
  Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', bytes)))
    .map(x => x.toString(16).padStart(2, '0'))
    .join('')
    .toUpperCase();

export function FileBrowser({ sessionId }: { sessionId: string }) {
  const [path, setPath] = useState('');
  const [entries, setEntries] = useState<DirEntry[]>([]);
  const [status, setStatus] = useState('');
  const downloads = useRef(new Map<string, { chunks: string[]; total: number }>());
  const pendingUploads = useRef(new Map<string, string[]>());
  const [targetPath, setTargetPath] = useState('');

  // Selected file preview & confirmation state
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() =>
    wsClient.subscribe(message => {
      if (message.sessionId !== sessionId) return;
      if (message.action === 'LIST_DIR_RESULT') {
        setPath(String(message.payload.path ?? ''));
        setEntries((message.payload.entries ?? []) as DirEntry[]);
      }
      if (message.action === 'FILE_CHUNK') {
        const id = String(message.payload.transferId);
        const state = downloads.current.get(id) ?? { chunks: [], total: Number(message.payload.totalChunks) };
        state.chunks[Number(message.payload.chunkIndex)] = String(message.payload.dataBase64);
        downloads.current.set(id, state);
        setStatus(`Đang tải ${state.chunks.filter(Boolean).length}/${state.total} chunks…`);
      }
      if (message.action === 'FILE_TRANSFER_COMPLETE') {
        const id = String(message.payload.transferId);
        const state = downloads.current.get(id);
        if (state && Boolean(message.payload.success)) {
          const bytes = state.chunks.flatMap(item => Array.from(Uint8Array.from(atob(item), c => c.charCodeAt(0))));
          const url = URL.createObjectURL(new Blob([new Uint8Array(bytes)]));
          const link = document.createElement('a');
          link.href = url;
          link.download = `download-${id}`;
          link.click();
          URL.revokeObjectURL(url);
          setStatus('Tải file hoàn tất.');
        }
        downloads.current.delete(id);
      }
      if (message.action === 'UPLOAD_FILE_INIT_RESULT') {
        const id = String(message.payload.transferId);
        const chunks = pendingUploads.current.get(id);
        if (Boolean(message.payload.accepted) && chunks) {
          chunks.forEach((dataBase64, chunkIndex) =>
            wsClient.send('UPLOAD_FILE_CHUNK', { transferId: id, chunkIndex, dataBase64 }, sessionId)
          );
          setStatus(`Đang gửi ${chunks.length} chunks…`);
        } else {
          setStatus(String(message.payload.message ?? 'Target từ chối upload.'));
        }
      }
      if (message.action === 'UPLOAD_FILE_RESULT') {
        pendingUploads.current.delete(String(message.payload.transferId));
        setStatus(String(message.payload.message ?? 'Upload xong.'));
      }
      if (message.action === 'ERROR') {
        setStatus(String(message.payload.message ?? 'Thao tác tệp không được phép.'));
      }
    }), [sessionId]);

  const list = (next: string) => {
    if (next) wsClient.send('LIST_DIR', { path: next }, sessionId);
  };

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setSelectedFile(file);
    if (file.type.startsWith('image/')) {
      setPreviewUrl(URL.createObjectURL(file));
    } else {
      setPreviewUrl(null);
    }
  };

  const clearSelectedFile = () => {
    setSelectedFile(null);
    if (previewUrl) {
      URL.revokeObjectURL(previewUrl);
      setPreviewUrl(null);
    }
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const startUpload = async () => {
    if (!selectedFile) return;
    if (!targetPath) return setStatus('Hãy nhập thư mục đích trên Target.');

    const file = selectedFile;
    const bytes = await file.arrayBuffer();
    const id = crypto.randomUUID();
    const view = new Uint8Array(bytes);
    const chunks = Array.from({ length: Math.ceil(view.length / chunkSize) }, (_, index) => {
      const part = view.slice(index * chunkSize, (index + 1) * chunkSize);
      let binary = '';
      for (let offset = 0; offset < part.length; offset += 8192) {
        binary += String.fromCharCode(...part.subarray(offset, offset + 8192));
      }
      return btoa(binary);
    });

    pendingUploads.current.set(id, chunks);
    wsClient.send('UPLOAD_FILE_INIT', {
      transferId: id,
      targetPath,
      fileName: file.name,
      totalChunks: chunks.length,
      sha256: await sha256(bytes)
    }, sessionId);

    setStatus(`Đang gửi yêu cầu upload '${file.name}' tới Target…`);
    clearSelectedFile();
  };

  return (
    <section className="panel">
      <div className="panel-title">
        <h2>Tệp trên Target</h2>
        <button onClick={() => list(path)}>Làm mới</button>
      </div>

      <div className="row">
        <input value={path} onChange={e => setPath(e.target.value)} placeholder="Đường dẫn cần duyệt (VD: /Users hoặc C:\)" />
        <button onClick={() => list(path)}>Mở</button>
      </div>

      <div className="file-list">
        {entries.map(item => (
          <div key={item.name} className="file-row">
            <button
              className="file-name"
              onClick={() => item.isDirectory ? list(`${path.replace(/[\\/]$/, '')}/${item.name}`) : undefined}
            >
              {item.isDirectory ? '📁' : '📄'} {item.name}
            </button>
            <span>{item.isDirectory ? '' : `${item.sizeBytes ?? 0} bytes`}</span>
            {!item.isDirectory && (
              <button
                className="compact"
                onClick={() => {
                  const id = crypto.randomUUID();
                  downloads.current.set(id, { chunks: [], total: 0 });
                  wsClient.send('DOWNLOAD_FILE', { path: `${path.replace(/[\\/]$/, '')}/${item.name}`, transferId: id }, sessionId);
                }}
              >
                Tải xuống
              </button>
            )}
          </div>
        ))}
      </div>

      <div className="upload-section" style={{ marginTop: '1rem', paddingTop: '1rem', borderTop: '1px solid #293451' }}>
        <div className="row">
          <input
            value={targetPath}
            onChange={e => setTargetPath(e.target.value)}
            placeholder="Thư mục đích trên Target (VD: /tmp hoặc C:\Users\Public)"
          />
          <input
            ref={fileInputRef}
            type="file"
            onChange={handleFileSelect}
            style={{ width: 'auto' }}
          />
        </div>

        {selectedFile && (
          <div className="panel" style={{ background: '#0f172a', border: '1px solid #334155', marginTop: '0.75rem', padding: '0.85rem' }}>
            <h4 style={{ margin: '0 0 0.5rem', color: '#38bdf8' }}>📄 Tệp đã chọn để tải lên:</h4>
            
            {previewUrl && (
              <div style={{ marginBottom: '0.75rem' }}>
                <img
                  src={previewUrl}
                  alt="Preview"
                  style={{ maxHeight: '140px', maxWidth: '100%', borderRadius: '6px', border: '1px solid #475569', objectFit: 'contain' }}
                />
              </div>
            )}

            <p style={{ margin: '0 0 0.75rem', fontSize: '0.9rem' }}>
              <strong>Tên tệp:</strong> {selectedFile.name}<br />
              <strong>Dung lượng:</strong> {(selectedFile.size / 1024).toFixed(1)} KB ({selectedFile.size} bytes)<br />
              <strong>Đích đến:</strong> {targetPath || '(Chưa nhập thư mục đích)'}
            </p>

            <div className="row" style={{ margin: 0, gap: '0.5rem' }}>
              <button
                className="compact"
                disabled={!targetPath}
                onClick={startUpload}
                style={{ background: '#16a34a' }}
              >
                ✓ Xác nhận Tải lên máy Target
              </button>
              <button
                className="compact danger"
                onClick={clearSelectedFile}
              >
                ✕ Hủy / Loại bỏ tệp
              </button>
            </div>
          </div>
        )}
      </div>

      {status && <p className="hint" style={{ marginTop: '0.5rem' }}>{status}</p>}
    </section>
  );
}
