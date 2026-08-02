using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RemoteControlLAN.Gateway.Data;
using RemoteControlLAN.Gateway.Repositories;
using RemoteControlLAN.Gateway.WebSockets;
using Microsoft.EntityFrameworkCore;

namespace RemoteControlLAN.Gateway.Controllers;

[ApiController, Authorize(Roles = "admin"), Route("api/admin")]
public sealed class AdminController(IUserRepository users, ISessionRepository sessions, AppDbContext db, ConnectionManager connections) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IResult> ListUsers()
    {
        var all = await users.ListAllAsync();
        return Results.Ok(all.Select(u => new { id = u.Id, username = u.Username, isAdmin = u.IsAdmin, createdAt = u.CreatedAt, failedLoginCount = u.FailedLoginCount, lockedUntil = u.LockedUntil }));
    }

    [HttpGet("agents")]
    public async Task<IResult> ListAgents()
    {
        var agents = await db.Agents.OrderBy(a => a.AgentName).ToListAsync();
        return Results.Ok(agents.Select(a => new { id = a.Id, name = a.AgentName, platform = a.Platform, lastOnlineAt = a.LastOnlineAt, lastSeenIp = a.LastSeenIp, isOnline = connections.IsAgentOnline(a.Id.ToString()), hasPairingPin = !string.IsNullOrWhiteSpace(a.PairingPinHash), pairingPinExpiresAt = a.PairingPinExpiresAt }));
    }

    [HttpDelete("users/{id}")]
    public async Task<IResult> DeleteUser(Guid id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (Guid.TryParse(currentUserId, out var myId) && myId == id) return Results.BadRequest(new { message = "Không thể xóa chính mình." });
        var user = await users.ByIdAsync(id);
        if (user is null) return Results.NotFound(new { message = "Không tìm thấy user." });
        if (user.IsAdmin) return Results.BadRequest(new { message = "Không thể xóa tài khoản admin." });
        await sessions.RemoveForUserAsync(id);
        await users.DeleteAsync(user);
        await users.SaveAsync();
        return Results.Ok(new { message = $"Đã xóa user '{user.Username}'." });
    }

    [HttpDelete("agents/{id}")]
    public async Task<IResult> DeleteAgent(Guid id)
    {
        var agent = await db.Agents.FindAsync(id);
        if (agent is null) return Results.NotFound(new { message = "Không tìm thấy agent." });
        if (connections.IsAgentOnline(id.ToString())) return Results.Conflict(new { message = "Không thể xóa Agent đang online. Hãy tắt Agent trước." });
        await sessions.RemoveForAgentAsync(id);
        db.Agents.Remove(agent);
        await db.SaveChangesAsync();
        return Results.Ok(new { message = $"Đã xóa agent '{agent.AgentName}'." });
    }
}
