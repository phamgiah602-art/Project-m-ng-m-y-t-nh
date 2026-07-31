using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RemoteControlLAN.Gateway.Services;
using RemoteControlLAN.Shared.Messages;

namespace RemoteControlLAN.Gateway.WebSockets;

public sealed class MessageRouter(ConnectionManager connections, IServiceScopeFactory scopeFactory)
{
    private static readonly HashSet<string> ToAgent = ["GET_PROCESS_LIST", "START_PROCESS", "STOP_PROCESS", "SHUTDOWN", "RESTART", "ENABLE_KEYLOGGER", "DISABLE_KEYLOGGER", "START_SCREEN_VIEW", "STOP_SCREEN_VIEW", "START_WEBCAM", "STOP_WEBCAM", "LIST_DIR", "DOWNLOAD_FILE", "UPLOAD_FILE_INIT", "UPLOAD_FILE_CHUNK"];
    private static readonly HashSet<string> ToBrowser = ["PROCESS_LIST_RESULT", "START_PROCESS_RESULT", "STOP_PROCESS_RESULT", "SHUTDOWN_RESULT", "RESTART_RESULT", "KEYLOGGER_CONSENT_RESULT", "KEYLOG_BATCH", "DISABLE_KEYLOGGER_RESULT", "SCREEN_FRAME", "WEBCAM_FRAME", "LIST_DIR_RESULT", "FILE_CHUNK", "FILE_TRANSFER_COMPLETE", "UPLOAD_FILE_INIT_RESULT", "UPLOAD_FILE_RESULT", "ERROR"];
    public async Task RouteAsync(string rawJson, string connectionId)
    {
        connections.Touch(connectionId);
        MessageEnvelope? message;
        try { message = JsonSerializer.Deserialize<MessageEnvelope>(rawJson, JsonConfig.Default); } catch { await ErrorAsync(connectionId, "UNKNOWN_ACTION", "Message không đúng định dạng JSON", null); return; }
        if (message is null || string.IsNullOrWhiteSpace(message.Action)) { await ErrorAsync(connectionId, "UNKNOWN_ACTION", "Thiếu action", null); return; }
        switch (message.Action)
        {
            case "PING": await SendAsync(connectionId, MessageEnvelope.Create("PONG", "PONG", new EmptyPayload())); return;
            case "PONG": return;
            case "REGISTER_AGENT": await RegisterAsync(message, connectionId); return;
            case "UPDATE_PAIRING_PIN": await UpdatePinAsync(message, connectionId); return;
            case "REQUEST_PAIRING": await PairAsync(message, connectionId); return;
        }
        var source = connections.Get(connectionId);
        var toAgent = ToAgent.Contains(message.Action); var toBrowser = ToBrowser.Contains(message.Action);
        if (source is null || (!toAgent && !toBrowser)) { await ErrorAsync(connectionId, "UNKNOWN_ACTION", "Action không được hỗ trợ", message.Action); return; }
        if (string.IsNullOrWhiteSpace(message.SessionId) || !connections.Authorizes(message.SessionId, connectionId, toAgent ? ConnectionRole.Browser : ConnectionRole.Agent)) { await ErrorAsync(connectionId, "SESSION_INVALID", "Session không hợp lệ", message.Action); return; }
        if (message.Action is "SHUTDOWN" or "RESTART")
        {
            var payload = message.GetPayload<ShutdownPayload>();
            using var scope = scopeFactory.CreateScope();
            if (payload is null || !await scope.ServiceProvider.GetRequiredService<IAuthService>().ConsumeConfirmationTokenAsync(message.SessionId, payload.ConfirmationToken)) { await ErrorAsync(connectionId, "CONFIRMATION_INVALID", "Xác nhận mật khẩu không hợp lệ", message.Action); return; }
        }
        var session = connections.Session(message.SessionId)!;
        var target = toAgent ? connections.AgentSocket(message.SessionId) : connections.BrowserSocket(message.SessionId);
        if (target?.State != WebSocketState.Open) { await ErrorAsync(connectionId, "AGENT_OFFLINE", "Đầu nhận đang ngoại tuyến", message.Action); return; }
        await SendRawAsync(target, rawJson);
        using (var scope = scopeFactory.CreateScope()) await scope.ServiceProvider.GetRequiredService<IAuditService>().WriteAsync(message.Action, "Forwarded", rawJson, Guid.TryParse(message.SessionId, out var sessionId) ? sessionId : null, Guid.TryParse(session.UserId, out var userId) ? userId : null, Guid.TryParse(session.AgentId, out var agentId) ? agentId : null);
    }
    private async Task RegisterAsync(MessageEnvelope message, string connectionId)
    {
        var state = connections.Get(connectionId); var payload = message.GetPayload<RegisterAgentPayload>();
        using var scope = scopeFactory.CreateScope();
        if (state?.Role != ConnectionRole.Pending || payload is null || string.IsNullOrWhiteSpace(message.AgentId) || !await scope.ServiceProvider.GetRequiredService<IAuthService>().ValidateAgentSecretKeyAsync(message.AgentId, payload.AgentSecretKey)) { await SendAsync(connectionId, MessageEnvelope.Create("RESPONSE", "REGISTER_AGENT_RESULT", new RegisterAgentResultPayload { Success = false, Message = "Không xác thực được Agent." })); return; }
        connections.RegisterAgent(message.AgentId, connectionId);
        await SendAsync(connectionId, MessageEnvelope.Create("RESPONSE", "REGISTER_AGENT_RESULT", new RegisterAgentResultPayload { Success = true, AgentId = message.AgentId, Message = "Agent đã kết nối." }));
        await scope.ServiceProvider.GetRequiredService<IAuditService>().WriteAsync("REGISTER_AGENT", "Success", agentId: Guid.TryParse(message.AgentId, out var agentId) ? agentId : null);
    }
    private async Task UpdatePinAsync(MessageEnvelope message, string connectionId)
    {
        var state = connections.Get(connectionId); var pin = message.GetPayload<PinPayload>()?.Pin ?? string.Empty;
        using var scope = scopeFactory.CreateScope();
        var valid = state?.Role == ConnectionRole.Agent && state.AgentId == message.AgentId && await scope.ServiceProvider.GetRequiredService<IPairingService>().UpdateAgentPinAsync(message.AgentId ?? string.Empty, pin);
        await SendAsync(connectionId, MessageEnvelope.Create("RESPONSE", "UPDATE_PAIRING_PIN_RESULT", new PinResultPayload { Success = valid, Message = valid ? "PIN đã được cập nhật." : "PIN không hợp lệ." }));
    }
    private async Task PairAsync(MessageEnvelope message, string connectionId)
    {
        var state = connections.Get(connectionId); var payload = message.GetPayload<RequestPairingPayload>();
        if (state?.Role != ConnectionRole.Browser || !Guid.TryParse(state.UserId, out var userId) || payload is null) { await ErrorAsync(connectionId, "AUTH_FAILED", "Cần đăng nhập trước khi ghép cặp.", message.Action); return; }
        var agentConnectionId = connections.AgentConnection(payload.AgentId);
        if (agentConnectionId is null) { await SendAsync(connectionId, MessageEnvelope.Create("RESPONSE", "PAIRING_RESULT", new PairingResultPayload { Success = false, Message = "Agent đang ngoại tuyến." })); return; }
        using var scope = scopeFactory.CreateScope();
        var outcome = await scope.ServiceProvider.GetRequiredService<IPairingService>().VerifyPinAsync(userId, payload.AgentId, payload.Pin);
        if (outcome.Success && outcome.SessionId is not null) connections.Bind(outcome.SessionId, agentConnectionId, connectionId, payload.AgentId, state.UserId!);
        var response = MessageEnvelope.Create("RESPONSE", "PAIRING_RESULT", new PairingResultPayload { Success = outcome.Success, SessionId = outcome.SessionId, Message = outcome.Message }, outcome.SessionId, payload.AgentId);
        await SendAsync(connectionId, response);
        if (outcome.Success && outcome.SessionId is not null && connections.AgentSocket(outcome.SessionId) is { } socket) await SendRawAsync(socket, JsonSerializer.Serialize(response, JsonConfig.Default));
        await scope.ServiceProvider.GetRequiredService<IAuditService>().WriteAsync("REQUEST_PAIRING", outcome.Success ? "Success" : "Rejected", sessionId: outcome.SessionId is not null ? Guid.Parse(outcome.SessionId) : null, userId: userId, agentId: Guid.TryParse(payload.AgentId, out var agentId) ? agentId : null);
    }
    public async Task NotifyDisconnectAsync(SessionBinding session)
    {
        var payload = new AgentDisconnectedPayload { AgentId = session.AgentId, LastSeenAt = DateTime.UtcNow };
        if (connections.BrowserSocketByConnectionId(session.BrowserConnectionId) is { } socket) await SendRawAsync(socket, JsonSerializer.Serialize(MessageEnvelope.Create("EVENT", "AGENT_DISCONNECTED", payload, null, session.AgentId), JsonConfig.Default));
    }
    private async Task ErrorAsync(string connectionId, string code, string message, string? relatedAction) => await SendAsync(connectionId, MessageEnvelope.Create("RESPONSE", "ERROR", new ErrorPayload { Code = code, Message = message, RelatedAction = relatedAction }));
    public async Task SendAsync(string connectionId, MessageEnvelope message) { if (connections.Get(connectionId)?.Socket is { State: WebSocketState.Open } socket) await SendRawAsync(socket, JsonSerializer.Serialize(message, JsonConfig.Default)); }
    public static Task SendRawAsync(WebSocket socket, string text) => socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);
}

public sealed class PinResultPayload { public bool Success { get; set; } public string? Message { get; set; } }
