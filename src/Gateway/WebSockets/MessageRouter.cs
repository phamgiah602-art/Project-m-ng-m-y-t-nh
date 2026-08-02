using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RemoteControlLAN.Gateway.Repositories;
using RemoteControlLAN.Gateway.Services;
using RemoteControlLAN.Shared.Messages;

namespace RemoteControlLAN.Gateway.WebSockets;

public sealed class MessageRouter(ConnectionManager connections, IServiceScopeFactory scopeFactory)
{
    private static readonly HashSet<string> ToAgent = ["GET_PROCESS_LIST", "START_PROCESS", "STOP_PROCESS", "SHUTDOWN", "RESTART", "ENABLE_KEYLOGGER", "DISABLE_KEYLOGGER", "START_SCREEN_VIEW", "STOP_SCREEN_VIEW", "START_WEBCAM", "STOP_WEBCAM", "LIST_DIR", "DOWNLOAD_FILE", "UPLOAD_FILE_INIT", "UPLOAD_FILE_CHUNK"];
    private static readonly HashSet<string> ToBrowser = ["PROCESS_LIST_RESULT", "START_PROCESS_RESULT", "STOP_PROCESS_RESULT", "RESTART_RESULT", "SHUTDOWN_RESULT", "KEYLOGGER_CONSENT_RESULT", "KEYLOG_BATCH", "DISABLE_KEYLOGGER_RESULT", "SCREEN_FRAME", "WEBCAM_FRAME", "LIST_DIR_RESULT", "FILE_CHUNK", "FILE_TRANSFER_COMPLETE", "UPLOAD_FILE_INIT_RESULT", "UPLOAD_FILE_CHUNK_RESULT", "UPLOAD_FILE_RESULT", "ERROR"];
    public async Task RouteAsync(string rawJson, string connectionId, string? remoteIpAddress = null)
    {
        connections.Touch(connectionId);
        MessageEnvelope? message;
        try { message = JsonSerializer.Deserialize<MessageEnvelope>(rawJson, JsonConfig.Default); } catch { await ErrorAsync(connectionId, "UNKNOWN_ACTION", "Message không đúng định dạng JSON", null); return; }
        if (message is null || string.IsNullOrWhiteSpace(message.Action)) { await ErrorAsync(connectionId, "UNKNOWN_ACTION", "Thiếu action", null); return; }
        switch (message.Action)
        {
            case "PING": await SendAsync(connectionId, MessageEnvelope.Create("PONG", "PONG", new EmptyPayload())); return;
            case "PONG": return;
            case "REGISTER_AGENT": await RegisterAsync(message, connectionId, remoteIpAddress); return;
            case "UPDATE_PAIRING_PIN": await UpdatePinAsync(message, connectionId); return;
            case "REQUEST_PAIRING": await PairAsync(message, connectionId); return;
            case "END_SESSION": await EndSessionAsync(message.SessionId, connectionId, "Operator đã kết thúc phiên."); return;
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
        var targetId = toAgent ? session.AgentConnectionId : session.BrowserConnectionId;
        if (!await connections.SendAsync(targetId, rawJson)) { await ErrorAsync(connectionId, "AGENT_OFFLINE", "Đầu nhận đang ngoại tuyến", message.Action); return; }
        using (var scope = scopeFactory.CreateScope()) await scope.ServiceProvider.GetRequiredService<IAuditService>().WriteAsync(message.Action, "Forwarded", System.Text.Json.JsonSerializer.Serialize(new { message.Type, message.Action }), Guid.TryParse(message.SessionId, out var sessionId) ? sessionId : null, Guid.TryParse(session.UserId, out var userId) ? userId : null, Guid.TryParse(session.AgentId, out var agentId) ? agentId : null);
    }
    private async Task RegisterAsync(MessageEnvelope message, string connectionId, string? remoteIpAddress)
    {
        var state = connections.Get(connectionId); var payload = message.GetPayload<RegisterAgentPayload>();
        using var scope = scopeFactory.CreateScope();
        if (state?.Role != ConnectionRole.Pending || payload is null || string.IsNullOrWhiteSpace(message.AgentId) || !await scope.ServiceProvider.GetRequiredService<IAuthService>().ValidateAgentSecretKeyAsync(message.AgentId, payload.AgentSecretKey)) { await SendAsync(connectionId, MessageEnvelope.Create("RESPONSE", "REGISTER_AGENT_RESULT", new RegisterAgentResultPayload { Success = false, Message = "Không xác thực được Agent." })); return; }
        var replacedConnection = connections.RegisterAgent(message.AgentId, connectionId);
        await scope.ServiceProvider.GetRequiredService<IPairingService>().MarkAgentOnlineAsync(message.AgentId, remoteIpAddress);
        await SendAsync(connectionId, MessageEnvelope.Create("RESPONSE", "REGISTER_AGENT_RESULT", new RegisterAgentResultPayload { Success = true, AgentId = message.AgentId, Message = "Agent đã kết nối." }));
        await scope.ServiceProvider.GetRequiredService<IAuditService>().WriteAsync("REGISTER_AGENT", "Success", agentId: Guid.TryParse(message.AgentId, out var agentId) ? agentId : null);
        if (replacedConnection is not null && connections.Get(replacedConnection)?.Socket is { } oldSocket) oldSocket.Abort();
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
        if (outcome.Success && outcome.SessionId is not null) await connections.SendAsync(agentConnectionId, JsonSerializer.Serialize(response, JsonConfig.Default));
        await scope.ServiceProvider.GetRequiredService<IAuditService>().WriteAsync("REQUEST_PAIRING", outcome.Success ? "Success" : "Rejected", sessionId: outcome.SessionId is not null ? Guid.Parse(outcome.SessionId) : null, userId: userId, agentId: Guid.TryParse(payload.AgentId, out var agentId) ? agentId : null);
    }
    public async Task NotifyDisconnectAsync(SessionBinding session, string disconnectedConnectionId)
    {
        var recipient = disconnectedConnectionId == session.AgentConnectionId ? session.BrowserConnectionId : session.AgentConnectionId;
        await EndSessionAsync(session, recipient, disconnectedConnectionId == session.AgentConnectionId ? "Agent đã ngắt kết nối." : "Operator đã ngắt kết nối.");
    }
    private async Task EndSessionAsync(string? sessionId, string connectionId, string reason)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !connections.Authorizes(sessionId, connectionId, ConnectionRole.Browser) || connections.Session(sessionId) is not { } session) { await ErrorAsync(connectionId, "SESSION_INVALID", "Session không hợp lệ", "END_SESSION"); return; }
        await EndSessionAsync(session, session.AgentConnectionId, reason);
        await connections.SendAsync(connectionId, JsonSerializer.Serialize(MessageEnvelope.Create("EVENT", "SESSION_ENDED", new SessionEndedPayload { Message = reason }, sessionId, session.AgentId), JsonConfig.Default));
    }
    private async Task EndSessionAsync(SessionBinding binding, string recipientConnectionId, string reason)
    {
        using var scope = scopeFactory.CreateScope();
        var sessionRepository = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        if (!Guid.TryParse(binding.SessionId, out var parsedSessionId)) return;
        var active = await sessionRepository.ByIdAsync(parsedSessionId);
        if (active is not { Status: "Active" }) return;
        await sessionRepository.EndAsync(parsedSessionId);
        await sessionRepository.SaveAsync();
        await scope.ServiceProvider.GetRequiredService<IAuditService>().WriteAsync("SESSION_ENDED", "Success", sessionId: active.Id, userId: active.UserId, agentId: active.AgentId);
        await connections.SendAsync(recipientConnectionId, JsonSerializer.Serialize(MessageEnvelope.Create("EVENT", "SESSION_ENDED", new SessionEndedPayload { Message = reason }, active.Id.ToString(), binding.AgentId), JsonConfig.Default));
    }
    private async Task ErrorAsync(string connectionId, string code, string message, string? relatedAction) => await SendAsync(connectionId, MessageEnvelope.Create("RESPONSE", "ERROR", new ErrorPayload { Code = code, Message = message, RelatedAction = relatedAction }));
    public Task SendAsync(string connectionId, MessageEnvelope message) => connections.SendAsync(connectionId, JsonSerializer.Serialize(message, JsonConfig.Default));
}

public sealed class PinResultPayload { public bool Success { get; set; } public string? Message { get; set; } }
