using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace RemoteControlLAN.Gateway.WebSockets;

/// <summary>
/// Lưu toàn bộ kết nối WebSocket đang mở (cả Agent lẫn Browser) — xem README mục 4.2.
/// Đây là "nguồn dữ liệu duy nhất" về ai đang online. MessageRouter dựa vào đây để biết
/// forward message tới đúng WebSocket nào (theo SessionId), và để biết Agent nào tương
/// ứng AgentId nào.
///
/// Dùng ConcurrentDictionary vì nhiều Task xử lý nhiều kết nối cùng lúc (mỗi kết nối
/// chạy 1 vòng lặp ReceiveAsync riêng — README mục 4.2 "Message loop").
/// </summary>
public class ConnectionManager
{
    // Toàn bộ kết nối đang mở, khoá theo ConnectionId (mỗi lần 1 WebSocket mở ra là 1 connectionId mới)
    private readonly ConcurrentDictionary<string, WebSocket> _connections = new();

    // Map AgentId -> ConnectionId hiện tại của Agent đó (1 Agent luôn có tối đa 1 kết nối tại 1 thời điểm)
    private readonly ConcurrentDictionary<string, string> _agentConnections = new();

    // Map SessionId -> (ConnectionId của Agent, ConnectionId của Browser) trong cặp pairing đó
    private readonly ConcurrentDictionary<string, (string AgentConnectionId, string BrowserConnectionId)> _sessions = new();

    public void AddConnection(string connectionId, WebSocket socket) => _connections[connectionId] = socket;

    public void RemoveConnection(string connectionId) => _connections.TryRemove(connectionId, out _);

    public WebSocket? GetSocketByConnectionId(string connectionId) =>
        _connections.TryGetValue(connectionId, out var s) ? s : null;

    public void RegisterAgent(string agentId, string connectionId) => _agentConnections[agentId] = connectionId;

    public string? GetAgentConnectionId(string agentId) =>
        _agentConnections.TryGetValue(agentId, out var connId) ? connId : null;

    public void BindSession(string sessionId, string agentConnectionId, string browserConnectionId) =>
        _sessions[sessionId] = (agentConnectionId, browserConnectionId);

    public bool IsSessionValid(string sessionId) => _sessions.ContainsKey(sessionId);

    public WebSocket? GetAgentSocketBySession(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var pair) ? GetSocketByConnectionId(pair.AgentConnectionId) : null;

    public WebSocket? GetBrowserSocketBySession(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var pair) ? GetSocketByConnectionId(pair.BrowserConnectionId) : null;

    /// <summary>Gọi khi Heartbeat (README mục 4.2) phát hiện Agent mất kết nối, để dọn dẹp session liên quan.</summary>
    public void RemoveSessionsForAgent(string agentConnectionId)
    {
        foreach (var kv in _sessions)
        {
            if (kv.Value.AgentConnectionId == agentConnectionId)
                _sessions.TryRemove(kv.Key, out _);
        }
    }
}
