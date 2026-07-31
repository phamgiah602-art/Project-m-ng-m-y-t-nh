using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RemoteControlLAN.Gateway.Services;
using RemoteControlLAN.Shared.Messages;

namespace RemoteControlLAN.Gateway.WebSockets;

/// <summary>
/// Nơi DUY NHẤT đọc field "action" của message và quyết định việc cần làm — README mục 8
/// gọi đây là Mediator Pattern: Gateway không hiểu logic nghiệp vụ của từng action, chỉ
/// biết (1) tự xử lý vài action nội bộ, hoặc (2) forward nguyên văn tới đúng đầu bên kia.
///
/// Cách hoạt động, gọi RouteAsync(...) mỗi khi nhận 1 message JSON từ bất kỳ WebSocket nào
/// (cả từ Agent lẫn từ Browser):
///
///   1) Nếu action nằm trong bảng _internalHandlers → Gateway tự xử lý (vd REGISTER_AGENT,
///      REQUEST_PAIRING, PING) — các action này cần Gateway hiểu payload để làm gì đó
///      (kiểm tra DB, sinh SessionId...), không đơn thuần forward.
///
///   2) Nếu action nằm trong _forwardToAgentActions / _forwardToBrowserActions → Gateway
///      CHỈ kiểm tra SessionId hợp lệ (README mục 9: bắt buộc kiểm tra trên MỌI message)
///      rồi forward nguyên văn JSON sang đúng đầu kia, không đọc/hiểu payload bên trong.
///
///   3) Riêng SHUTDOWN/RESTART có thêm 1 bước xác thực confirmationToken TRƯỚC khi forward
///      (docs/kien-truc-chi-tiet.md mục 2.3) — vì đây là lệnh nguy hiểm nhất, sai 1 field
///      cũng không được để lọt xuống Agent.
///
///   4) Action không khớp bảng nào ở trên → trả ERROR với code UNKNOWN_ACTION.
///
/// Thêm 1 action mới trong tương lai chỉ cần thêm vào 1 trong 3 bảng ở trên — không cần
/// sửa logic RouteAsync.
/// </summary>
public class MessageRouter
{
    private readonly ConnectionManager _connections;
    private readonly IAuthService _authService;
    private readonly IPairingService _pairingService;

    private readonly Dictionary<string, Func<MessageEnvelope, string, Task>> _internalHandlers;

    // Action Browser → Gateway → Agent (Gateway chỉ forward, không hiểu payload)
    private static readonly HashSet<string> _forwardToAgentActions = new()
    {
        "GET_PROCESS_LIST", "START_PROCESS", "STOP_PROCESS",
        "SHUTDOWN", "RESTART",
        "ENABLE_KEYLOGGER", "DISABLE_KEYLOGGER",
        "START_SCREEN_VIEW", "STOP_SCREEN_VIEW",
        "START_WEBCAM", "STOP_WEBCAM",
        "LIST_DIR", "DOWNLOAD_FILE",
        "UPLOAD_FILE_INIT", "UPLOAD_FILE_CHUNK"
    };

    // Action Agent → Gateway → Browser (Gateway chỉ forward, không hiểu payload)
    private static readonly HashSet<string> _forwardToBrowserActions = new()
    {
        "PROCESS_LIST_RESULT", "START_PROCESS_RESULT", "STOP_PROCESS_RESULT",
        "SHUTDOWN_RESULT", "RESTART_RESULT",
        "KEYLOGGER_CONSENT_RESULT", "KEYLOG_BATCH", "DISABLE_KEYLOGGER_RESULT",
        "SCREEN_FRAME", "WEBCAM_FRAME",
        "LIST_DIR_RESULT", "FILE_CHUNK", "FILE_TRANSFER_COMPLETE",
        "UPLOAD_FILE_INIT_RESULT", "UPLOAD_FILE_RESULT",
        "ERROR"
    };

    public MessageRouter(ConnectionManager connections, IAuthService authService, IPairingService pairingService)
    {
        _connections = connections;
        _authService = authService;
        _pairingService = pairingService;

        // Bảng tra cứu action Gateway tự xử lý nội bộ.
        _internalHandlers = new()
        {
            ["REGISTER_AGENT"] = HandleRegisterAgent,
            ["REQUEST_PAIRING"] = HandlePairing,
            ["PING"] = HandlePing,
        };
    }

    /// <summary>Điểm vào duy nhất — gọi hàm này mỗi khi nhận 1 message JSON từ 1 WebSocket.</summary>
    public async Task RouteAsync(string rawJson, string connectionId)
    {
        MessageEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<MessageEnvelope>(rawJson, JsonConfig.Default);
        }
        catch (JsonException)
        {
            await SendErrorAsync(connectionId, "UNKNOWN_ACTION", "Message không đúng định dạng JSON", null);
            return;
        }

        if (envelope is null || string.IsNullOrEmpty(envelope.Action))
        {
            await SendErrorAsync(connectionId, "UNKNOWN_ACTION", "Thiếu field 'action'", null);
            return;
        }

        // (1) Action Gateway tự xử lý nội bộ
        if (_internalHandlers.TryGetValue(envelope.Action, out var handler))
        {
            await handler(envelope, connectionId);
            return;
        }

        var isForwardToAgent = _forwardToAgentActions.Contains(envelope.Action);
        var isForwardToBrowser = _forwardToBrowserActions.Contains(envelope.Action);

        if (!isForwardToAgent && !isForwardToBrowser)
        {
            // (4) Action lạ, không nằm trong bảng nào
            await SendErrorAsync(connectionId, "UNKNOWN_ACTION", $"Action '{envelope.Action}' không được hỗ trợ", envelope.Action);
            return;
        }

        // (2) Kiểm tra SessionId hợp lệ trên MỌI message trước khi forward — README mục 9
        if (string.IsNullOrEmpty(envelope.SessionId) || !_connections.IsSessionValid(envelope.SessionId))
        {
            await SendErrorAsync(connectionId, "SESSION_INVALID", "Session không hợp lệ hoặc đã hết hạn", envelope.Action);
            return;
        }

        // (3) Trường hợp đặc biệt: SHUTDOWN/RESTART phải xác thực confirmationToken TRƯỚC
        // khi forward — sai token thì CHẶN LUÔN tại Gateway, Agent không bao giờ nhận được lệnh.
        if (envelope.Action is "SHUTDOWN" or "RESTART")
        {
            var shutdownPayload = envelope.GetPayload<ShutdownPayload>();
            var tokenValid = shutdownPayload is not null &&
                await _authService.ValidateConfirmationTokenAsync(envelope.SessionId, shutdownPayload.ConfirmationToken);

            if (!tokenValid)
            {
                await SendErrorAsync(connectionId, "CONFIRMATION_INVALID", "Token xác nhận mật khẩu sai hoặc đã hết hạn", envelope.Action);
                return;
            }
        }

        var target = isForwardToAgent
            ? _connections.GetAgentSocketBySession(envelope.SessionId)
            : _connections.GetBrowserSocketBySession(envelope.SessionId);

        if (target is null)
        {
            await SendErrorAsync(connectionId, "AGENT_OFFLINE", "Không tìm thấy đầu nhận đang online", envelope.Action);
            return;
        }

        await ForwardRawAsync(target, rawJson);
    }

    private async Task HandleRegisterAgent(MessageEnvelope msg, string connectionId)
    {
        var payload = msg.GetPayload<RegisterAgentPayload>();
        var agentId = msg.AgentId ?? string.Empty;

        var valid = payload is not null && await _authService.ValidateAgentSecretKeyAsync(agentId, payload.AgentSecretKey);

        if (!valid)
        {
            var fail = MessageEnvelope.Create("RESPONSE", "REGISTER_AGENT_RESULT",
                new RegisterAgentResultPayload { Success = false, Message = "AgentSecretKey không hợp lệ" });
            await SendToConnectionAsync(connectionId, fail);
            return;
        }

        // Lưu ý: connectionId đã được đăng ký vào ConnectionManager từ lúc middleware
        // Accept WebSocket (trước khi RouteAsync chạy) — ở đây chỉ cần gắn thêm AgentId
        // vào đúng connectionId đó.
        _connections.RegisterAgent(agentId, connectionId);

        var result = MessageEnvelope.Create("RESPONSE", "REGISTER_AGENT_RESULT",
            new RegisterAgentResultPayload { Success = true, AgentId = agentId, Message = "Đăng ký thành công" });
        await SendToConnectionAsync(connectionId, result);
    }

    private async Task HandlePairing(MessageEnvelope msg, string connectionId)
    {
        var payload = msg.GetPayload<RequestPairingPayload>();
        if (payload is null)
        {
            await SendErrorAsync(connectionId, "UNKNOWN_ACTION", "Payload REQUEST_PAIRING không hợp lệ", "REQUEST_PAIRING");
            return;
        }

        var outcome = await _pairingService.VerifyPinAsync(payload.AgentId, payload.Pin);

        if (outcome.Success && outcome.SessionId is not null)
        {
            var agentConnectionId = _connections.GetAgentConnectionId(payload.AgentId) ?? string.Empty;
            _connections.BindSession(outcome.SessionId, agentConnectionId, connectionId);
        }

        var resultForBrowser = MessageEnvelope.Create("RESPONSE", "PAIRING_RESULT",
            new PairingResultPayload { Success = outcome.Success, SessionId = outcome.SessionId, Message = outcome.Message });
        await SendToConnectionAsync(connectionId, resultForBrowser);

        // Báo luôn cho Agent để nó ẩn PIN và bắt đầu hiện "đang được điều khiển bởi..." (mục 5.1)
        if (outcome.Success && outcome.SessionId is not null)
        {
            var agentSocket = _connections.GetAgentSocketBySession(outcome.SessionId);
            if (agentSocket is not null)
            {
                var json = JsonSerializer.Serialize(resultForBrowser, JsonConfig.Default);
                await ForwardRawAsync(agentSocket, json);
            }
        }
    }

    private async Task HandlePing(MessageEnvelope msg, string connectionId)
    {
        var pong = MessageEnvelope.Create("PONG", "PONG", new EmptyPayload(),
            sessionId: msg.SessionId, agentId: msg.AgentId, connectionId: msg.ConnectionId);
        await SendToConnectionAsync(connectionId, pong);
    }

    private async Task SendErrorAsync(string connectionId, string code, string message, string? relatedAction)
    {
        var error = MessageEnvelope.Create("RESPONSE", "ERROR",
            new ErrorPayload { Code = code, Message = message, RelatedAction = relatedAction });
        await SendToConnectionAsync(connectionId, error);
    }

    private async Task SendToConnectionAsync(string connectionId, MessageEnvelope envelope)
    {
        var socket = _connections.GetSocketByConnectionId(connectionId);
        if (socket is null || socket.State != WebSocketState.Open) return;
        var json = JsonSerializer.Serialize(envelope, JsonConfig.Default);
        await ForwardRawAsync(socket, json);
    }

    private static async Task ForwardRawAsync(WebSocket target, string rawJson)
    {
        if (target.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(rawJson);
        await target.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
    }
}
