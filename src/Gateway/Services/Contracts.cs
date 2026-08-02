using RemoteControlLAN.Gateway.Models;
namespace RemoteControlLAN.Gateway.Services;
public record PairingOutcome(bool Success, string? SessionId, string Message);
public interface IAuthService { Task<AuthResult> RegisterAsync(string username, string password); Task<AuthResult> LoginAsync(string username, string password); Task<string?> ReverifyAsync(Guid userId, string sessionId, string password); Task<bool> ValidateAgentSecretKeyAsync(string agentId, string secretKey); Task<bool> ConsumeConfirmationTokenAsync(string sessionId, string confirmationToken); }
public record AuthResult(bool Success, string? Token, string Message);
public interface IPairingService { Task<PairingOutcome> VerifyPinAsync(Guid userId, string agentId, string pin); Task<bool> UpdateAgentPinAsync(string agentId, string pin); Task MarkAgentOnlineAsync(string agentId, string? ipAddress); }
public interface IAuditService { Task WriteAsync(string action, string result, string? payload = null, Guid? sessionId = null, Guid? userId = null, Guid? agentId = null); }
