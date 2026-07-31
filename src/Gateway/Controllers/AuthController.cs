using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RemoteControlLAN.Gateway.Repositories;
using RemoteControlLAN.Gateway.Services;

namespace RemoteControlLAN.Gateway.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(IAuthService auth, ISessionRepository sessions) : ControllerBase
{
    [HttpPost("register")] public async Task<IResult> Register(RegisterRequest request) => Results.Ok(await auth.RegisterAsync(request.Username, request.Password));
    [HttpPost("login")] public async Task<IResult> Login(LoginRequest request) => Results.Ok(await auth.LoginAsync(request.Username, request.Password));
    [Authorize, HttpPost("reverify-password")]
    public async Task<IResult> Reverify(ReverifyRequest request)
    {
        var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(userIdText, out var userId) || !Guid.TryParse(request.SessionId, out var sessionId)) return Results.Unauthorized();
        var session = await sessions.ByIdAsync(sessionId); if (session is null || session.UserId != userId || session.Status != "Active") return Results.Forbid();
        var token = await auth.ReverifyAsync(userId, request.SessionId, request.Password); return token is null ? Results.Unauthorized() : Results.Ok(new { confirmationToken = token });
    }
}
public sealed record RegisterRequest(string Username, string Password);
public sealed record LoginRequest(string Username, string Password);
public sealed record ReverifyRequest(string SessionId, string Password);
