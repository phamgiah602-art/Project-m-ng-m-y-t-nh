using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace RemoteControlLAN.Gateway.WebSockets;

public enum ConnectionRole { Pending, Browser, Agent }
public sealed class ConnectionState(WebSocket socket, ConnectionRole role, string? userId, string? agentId, DateTime lastSeenAt)
{
    public WebSocket Socket { get; } = socket;
    public ConnectionRole Role { get; set; } = role;
    public string? UserId { get; } = userId;
    public string? AgentId { get; set; } = agentId;
    public DateTime LastSeenAt { get; set; } = lastSeenAt;
    public SemaphoreSlim SendLock { get; } = new(1, 1);
}
public sealed record SessionBinding(string SessionId, string AgentConnectionId, string BrowserConnectionId, string AgentId, string UserId);

public sealed class ConnectionManager
{
    private readonly ConcurrentDictionary<string, ConnectionState> _connections = new();
    private readonly ConcurrentDictionary<string, string> _agentConnections = new();
    private readonly ConcurrentDictionary<string, SessionBinding> _sessions = new();
    public IEnumerable<KeyValuePair<string, ConnectionState>> All => _connections;
    public void Add(string connectionId, WebSocket socket, string? userId) => _connections[connectionId] = new(socket, userId is null ? ConnectionRole.Pending : ConnectionRole.Browser, userId, null, DateTime.UtcNow);
    public ConnectionState? Get(string id) => _connections.TryGetValue(id, out var state) ? state : null;
    public void Touch(string id) { if (_connections.TryGetValue(id, out var state)) state.LastSeenAt = DateTime.UtcNow; }
    public string? RegisterAgent(string agentId, string connectionId) { if (!_connections.TryGetValue(connectionId, out var state)) return null; state.Role = ConnectionRole.Agent; state.AgentId = agentId; state.LastSeenAt = DateTime.UtcNow; _agentConnections.TryGetValue(agentId, out var previous); _agentConnections[agentId] = connectionId; return previous == connectionId ? null : previous; }
    public string? AgentConnection(string agentId) => _agentConnections.TryGetValue(agentId, out var id) ? id : null;
    public void Bind(string sessionId, string agentConnectionId, string browserConnectionId, string agentId, string userId) => _sessions[sessionId] = new(sessionId, agentConnectionId, browserConnectionId, agentId, userId);
    public SessionBinding? Session(string sessionId) => _sessions.TryGetValue(sessionId, out var value) ? value : null;
    public bool Authorizes(string sessionId, string connectionId, ConnectionRole expected) => Session(sessionId) is { } session && ((expected == ConnectionRole.Agent && session.AgentConnectionId == connectionId) || (expected == ConnectionRole.Browser && session.BrowserConnectionId == connectionId));
    public WebSocket? AgentSocket(string sessionId) => Session(sessionId) is { } binding ? Get(binding.AgentConnectionId)?.Socket : null;
    public WebSocket? BrowserSocket(string sessionId) => Session(sessionId) is { } binding ? Get(binding.BrowserConnectionId)?.Socket : null;
    public WebSocket? BrowserSocketByConnectionId(string connectionId) => Get(connectionId)?.Socket;
    public bool IsAgentOnline(string agentId) => AgentConnection(agentId) is { } id && Get(id)?.Socket.State == WebSocketState.Open;
    public async Task<bool> SendAsync(string connectionId, string text, CancellationToken cancellationToken = default)
    {
        if (!_connections.TryGetValue(connectionId, out var state) || state.Socket.State != WebSocketState.Open) return false;
        await state.SendLock.WaitAsync(cancellationToken);
        try
        {
            if (state.Socket.State != WebSocketState.Open) return false;
            await state.Socket.SendAsync(System.Text.Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, cancellationToken);
            return true;
        }
        catch (WebSocketException) { return false; }
        finally { state.SendLock.Release(); }
    }
    public IEnumerable<SessionBinding> Remove(string connectionId) { _connections.TryRemove(connectionId, out var state); if (state?.AgentId is not null && _agentConnections.TryGetValue(state.AgentId, out var active) && active == connectionId) _agentConnections.TryRemove(state.AgentId, out _); var removed = _sessions.Where(x => x.Value.AgentConnectionId == connectionId || x.Value.BrowserConnectionId == connectionId).Select(x => x.Value).ToList(); foreach (var item in _sessions.Where(x => x.Value.AgentConnectionId == connectionId || x.Value.BrowserConnectionId == connectionId)) _sessions.TryRemove(item.Key, out _); return removed; }
}
