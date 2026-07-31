using Microsoft.EntityFrameworkCore;
using RemoteControlLAN.Gateway.Data;
using RemoteControlLAN.Gateway.Models;

namespace RemoteControlLAN.Gateway.Repositories;

public interface IUserRepository { Task<AppUser?> ByIdAsync(Guid id); Task<AppUser?> ByUsernameAsync(string username); Task AddAsync(AppUser user); Task SaveAsync(); }
public interface IAgentRepository { Task<AgentRecord?> ByIdAsync(Guid id); Task<AgentRecord?> ByNameAsync(string name); Task AddAsync(AgentRecord agent); Task SaveAsync(); }
public interface ISessionRepository { Task AddAsync(RemoteSession session); Task<RemoteSession?> ByIdAsync(Guid id); Task<RemoteSession?> ActiveForAgentAsync(Guid agentId); Task SaveAsync(); }
public interface IAuditLogRepository { Task AddAsync(AuditLog log); }
public sealed class UserRepository(AppDbContext db) : IUserRepository { public Task<AppUser?> ByIdAsync(Guid id) => db.Users.FindAsync(id).AsTask(); public Task<AppUser?> ByUsernameAsync(string username) => db.Users.SingleOrDefaultAsync(x => x.Username == username); public async Task AddAsync(AppUser user) => await db.Users.AddAsync(user); public Task SaveAsync() => db.SaveChangesAsync(); }
public sealed class AgentRepository(AppDbContext db) : IAgentRepository { public Task<AgentRecord?> ByIdAsync(Guid id) => db.Agents.FindAsync(id).AsTask(); public Task<AgentRecord?> ByNameAsync(string name) => db.Agents.SingleOrDefaultAsync(x => x.AgentName == name); public async Task AddAsync(AgentRecord agent) => await db.Agents.AddAsync(agent); public Task SaveAsync() => db.SaveChangesAsync(); }
public sealed class SessionRepository(AppDbContext db) : ISessionRepository { public async Task AddAsync(RemoteSession session) => await db.Sessions.AddAsync(session); public Task<RemoteSession?> ByIdAsync(Guid id) => db.Sessions.FindAsync(id).AsTask(); public Task<RemoteSession?> ActiveForAgentAsync(Guid agentId) => db.Sessions.SingleOrDefaultAsync(x => x.AgentId == agentId && x.Status == "Active"); public Task SaveAsync() => db.SaveChangesAsync(); }
public sealed class AuditLogRepository(AppDbContext db) : IAuditLogRepository { public async Task AddAsync(AuditLog log) { await db.AuditLogs.AddAsync(log); await db.SaveChangesAsync(); } }
