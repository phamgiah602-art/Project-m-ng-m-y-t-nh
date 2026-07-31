using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace RemoteControlLAN.Gateway.WebSockets;

public enum ConnectionRole { Pending, Browser, Agent }
public sealed record ConnectionState(WebSocket Socket, ConnectionRole Role, string? UserId, string? AgentId, DateTime LastSeenAt);
public sealed record SessionBinding(string AgentConnectionId, string BrowserConnectionId, string AgentId, string UserId);

public sealed class ConnectionManager
{
    private readonly ConcurrentDictionary<string, ConnectionState> _connections = new();
    private readonly ConcurrentDictionary<string, string> _agentConnections = new();
    private readonly ConcurrentDictionary<string, SessionBinding> _sessions = new();
    public IEnumerable<KeyValuePair<string, ConnectionState>> All => _connections;
    public void Add(string connectionId, WebSocket socket, string? userId) => _connections[connectionId] = new(socket, userId is null ? ConnectionRole.Pending : ConnectionRole.Browser, userId, null, DateTime.UtcNow);
    public ConnectionState? Get(string id) => _connections.TryGetValue(id, out var state) ? state : null;
    public void Touch(string id) { if (_connections.TryGetValue(id, out var state)) _connections[id] = state with { LastSeenAt = DateTime.UtcNow }; }
    public void RegisterAgent(string agentId, string connectionId) { if (_connections.TryGetValue(connectionId, out var state)) _connections[connectionId] = state with { Role = ConnectionRole.Agent, AgentId = agentId, LastSeenAt = DateTime.UtcNow }; _agentConnections[agentId] = connectionId; }
    public string? AgentConnection(string agentId) => _agentConnections.TryGetValue(agentId, out var id) ? id : null;
    public void Bind(string sessionId, string agentConnectionId, string browserConnectionId, string agentId, string userId) => _sessions[sessionId] = new(agentConnectionId, browserConnectionId, agentId, userId);
    public SessionBinding? Session(string sessionId) => _sessions.TryGetValue(sessionId, out var value) ? value : null;
    public bool Authorizes(string sessionId, string connectionId, ConnectionRole expected) => Session(sessionId) is { } session && ((expected == ConnectionRole.Agent && session.AgentConnectionId == connectionId) || (expected == ConnectionRole.Browser && session.BrowserConnectionId == connectionId));
    public WebSocket? AgentSocket(string sessionId) => Session(sessionId) is { } binding ? Get(binding.AgentConnectionId)?.Socket : null;
    public WebSocket? BrowserSocket(string sessionId) => Session(sessionId) is { } binding ? Get(binding.BrowserConnectionId)?.Socket : null;
    public WebSocket? BrowserSocketByConnectionId(string connectionId) => Get(connectionId)?.Socket;
    public IEnumerable<SessionBinding> Remove(string connectionId) { _connections.TryRemove(connectionId, out var state); if (state?.AgentId is not null) _agentConnections.TryRemove(state.AgentId, out _); var removed = _sessions.Where(x => x.Value.AgentConnectionId == connectionId || x.Value.BrowserConnectionId == connectionId).Select(x => x.Value).ToList(); foreach (var item in _sessions.Where(x => x.Value.AgentConnectionId == connectionId || x.Value.BrowserConnectionId == connectionId)) _sessions.TryRemove(item.Key, out _); return removed; }
}
