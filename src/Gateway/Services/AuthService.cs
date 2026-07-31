using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RemoteControlLAN.Gateway.Models;
using RemoteControlLAN.Gateway.Options;
using RemoteControlLAN.Gateway.Repositories;

namespace RemoteControlLAN.Gateway.Services;

public sealed class AuthService(IUserRepository users, IAgentRepository agents, IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly PasswordHasher<AppUser> _hasher = new();
    private readonly PasswordHasher<AgentRecord> _agentHasher = new();
    private readonly JwtOptions _jwt = jwtOptions.Value;
    private readonly HashSet<string> _usedConfirmationTokens = [];
    public async Task<AuthResult> RegisterAsync(string username, string password)
    {
        if (username.Length < 3 || password.Length < 8) return new(false, null, "Tên đăng nhập tối thiểu 3 và mật khẩu tối thiểu 8 ký tự.");
        if (await users.ByUsernameAsync(username) is not null) return new(false, null, "Tên đăng nhập đã tồn tại.");
        var user = new AppUser { Username = username.Trim() }; user.PasswordHash = _hasher.HashPassword(user, password);
        await users.AddAsync(user); await users.SaveAsync(); return new(true, CreateToken(user), "Đăng ký thành công.");
    }
    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        var user = await users.ByUsernameAsync(username); if (user is null) return new(false, null, "Sai tên đăng nhập hoặc mật khẩu.");
        if (user.LockedUntil > DateTime.UtcNow) return new(false, null, "Tài khoản đang bị khóa tạm thời.");
        if (_hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed) { user.FailedLoginCount++; if (user.FailedLoginCount >= 5) { user.LockedUntil = DateTime.UtcNow.AddMinutes(5); user.FailedLoginCount = 0; } await users.SaveAsync(); return new(false, null, "Sai tên đăng nhập hoặc mật khẩu."); }
        user.FailedLoginCount = 0; user.LockedUntil = null; await users.SaveAsync(); return new(true, CreateToken(user), "Đăng nhập thành công.");
    }
    public async Task<string?> ReverifyAsync(Guid userId, string sessionId, string password)
    {
        var user = await users.ByIdAsync(userId); if (user is null || _hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed) return null;
        var claims = new[] { new Claim("kind", "confirmation"), new Claim("sessionId", sessionId), new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()) };
        return CreateToken(claims, TimeSpan.FromMinutes(1));
    }
    public async Task<bool> ValidateAgentSecretKeyAsync(string agentId, string secretKey)
    { if (!Guid.TryParse(agentId, out var id)) return false; var agent = await agents.ByIdAsync(id); return agent is not null && _agentHasher.VerifyHashedPassword(agent, agent.AgentSecretKeyHash, secretKey) != PasswordVerificationResult.Failed; }
    public Task<bool> ConsumeConfirmationTokenAsync(string sessionId, string token)
    {
        lock (_usedConfirmationTokens) { if (_usedConfirmationTokens.Contains(token)) return Task.FromResult(false); }
        try { var principal = new JwtSecurityTokenHandler().ValidateToken(token, ValidationParameters(), out _); if (principal.FindFirst("kind")?.Value != "confirmation" || principal.FindFirst("sessionId")?.Value != sessionId) return Task.FromResult(false); lock (_usedConfirmationTokens) _usedConfirmationTokens.Add(token); return Task.FromResult(true); } catch { return Task.FromResult(false); }
    }
    private string CreateToken(AppUser user) => CreateToken([new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Username)], TimeSpan.FromMinutes(_jwt.AccessTokenMinutes));
    private string CreateToken(IEnumerable<Claim> claims, TimeSpan lifespan) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims, expires: DateTime.UtcNow.Add(lifespan), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)), SecurityAlgorithms.HmacSha256)));
    private TokenValidationParameters ValidationParameters() => new() { ValidateIssuer = true, ValidIssuer = _jwt.Issuer, ValidateAudience = true, ValidAudience = _jwt.Audience, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)), ValidateLifetime = true, ClockSkew = TimeSpan.Zero };
}
