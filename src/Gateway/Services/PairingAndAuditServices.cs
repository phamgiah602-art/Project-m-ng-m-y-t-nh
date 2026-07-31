using Microsoft.AspNetCore.Identity;
using RemoteControlLAN.Gateway.Models;
using RemoteControlLAN.Gateway.Repositories;

namespace RemoteControlLAN.Gateway.Services;

public sealed class PairingService(IAgentRepository agents, ISessionRepository sessions) : IPairingService
{
    private readonly PasswordHasher<AgentRecord> _hasher = new();
    public async Task<PairingOutcome> VerifyPinAsync(Guid userId, string agentId, string pin)
    {
        if (!Guid.TryParse(agentId, out var parsedId)) return new(false, null, "AgentId không hợp lệ.");
        var agent = await agents.ByIdAsync(parsedId);
        if (agent is null || agent.PairingPinExpiresAt <= DateTime.UtcNow || string.IsNullOrWhiteSpace(agent.PairingPinHash)) return new(false, null, "PIN không hợp lệ hoặc đã hết hạn.");
        if (_hasher.VerifyHashedPassword(agent, agent.PairingPinHash, pin) == PasswordVerificationResult.Failed) return new(false, null, "PIN không hợp lệ hoặc đã hết hạn.");
        if (await sessions.ActiveForAgentAsync(parsedId) is not null) return new(false, null, "Agent đang có một phiên điều khiển hoạt động.");
        agent.PairingPinHash = null; agent.PairingPinExpiresAt = null;
        var session = new RemoteSession { UserId = userId, AgentId = parsedId };
        await sessions.AddAsync(session); await sessions.SaveAsync(); await agents.SaveAsync();
        return new(true, session.Id.ToString(), "Ghép cặp thành công.");
    }
    public async Task<bool> UpdateAgentPinAsync(string agentId, string pin)
    {
        if (!Guid.TryParse(agentId, out var parsedId) || pin.Length != 6 || !pin.All(char.IsAsciiDigit)) return false;
        var agent = await agents.ByIdAsync(parsedId); if (agent is null) return false;
        agent.PairingPinHash = _hasher.HashPassword(agent, pin); agent.PairingPinExpiresAt = DateTime.UtcNow.AddMinutes(5); agent.LastOnlineAt = DateTime.UtcNow;
        await agents.SaveAsync(); return true;
    }
}

public sealed class AuditService(IAuditLogRepository logs) : IAuditService
{
    public Task WriteAsync(string action, string result, string? payload = null, Guid? sessionId = null, Guid? userId = null, Guid? agentId = null) => logs.AddAsync(new AuditLog { Action = action, Result = result, Payload = payload, SessionId = sessionId, UserId = userId, AgentId = agentId });
}

public record ProvisionedAgent(string AgentId, string AgentSecretKey, string AgentName);
public interface IAgentProvisioningService { Task<ProvisionedAgent?> CreateAsync(string agentName, string platform); }
public sealed class AgentProvisioningService(IAgentRepository agents) : IAgentProvisioningService
{
    private readonly PasswordHasher<AgentRecord> _hasher = new();
    public async Task<ProvisionedAgent?> CreateAsync(string agentName, string platform)
    {
        if (string.IsNullOrWhiteSpace(agentName) || await agents.ByNameAsync(agentName) is not null) return null;
        var secret = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var agent = new AgentRecord { Id = Guid.NewGuid(), AgentName = agentName.Trim(), Platform = platform };
        agent.AgentSecretKeyHash = _hasher.HashPassword(agent, secret); await agents.AddAsync(agent); await agents.SaveAsync();
        return new(agent.Id.ToString(), secret, agent.AgentName);
    }
}
