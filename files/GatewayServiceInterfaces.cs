namespace RemoteControlLAN.Gateway.Services;

/// <summary>
/// Interface cho phần Auth (thành viên A phụ trách — README mục 15). MessageRouter chỉ
/// gọi qua interface này, không quan tâm bên trong dùng ASP.NET Core Identity hay DB gì.
/// </summary>
public interface IAuthService
{
    /// <summary>Dùng khi Agent gửi REGISTER_AGENT — kiểm tra AgentSecretKey khớp AgentId.</summary>
    Task<bool> ValidateAgentSecretKeyAsync(string agentId, string secretKey);

    /// <summary>
    /// Dùng khi SHUTDOWN/RESTART — kiểm tra confirmationToken (sinh ra từ endpoint REST
    /// POST /api/auth/reverify-password) có đúng session, còn hạn, và CHƯA từng được dùng.
    /// Xem docs/kien-truc-chi-tiet.md mục 2.3 và 5.2.
    /// </summary>
    Task<bool> ValidateConfirmationTokenAsync(string sessionId, string confirmationToken);
}

/// <summary>
/// Interface cho phần Pairing (thành viên A phụ trách). Cài đặt cụ thể sẽ lo việc sinh PIN,
/// kiểm tra PIN, hết hạn PIN, và tạo SessionId mới khi pairing thành công.
/// </summary>
public interface IPairingService
{
    Task<PairingOutcome> VerifyPinAsync(string agentId, string pin);
}

/// <summary>Kết quả của việc verify PIN — SessionId chỉ có giá trị khi Success = true.</summary>
public record PairingOutcome(bool Success, string? SessionId, string? Message);
