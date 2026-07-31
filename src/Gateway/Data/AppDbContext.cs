using Microsoft.EntityFrameworkCore;
using RemoteControlLAN.Gateway.Models;

namespace RemoteControlLAN.Gateway.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AgentRecord> Agents => Set<AgentRecord>();
    public DbSet<RemoteSession> Sessions => Set<RemoteSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<AppUser>().HasIndex(x => x.Username).IsUnique();
        builder.Entity<AgentRecord>().HasIndex(x => x.AgentName).IsUnique();
        builder.Entity<RemoteSession>().HasIndex(x => new { x.AgentId, x.Status });
        builder.Entity<RemoteSession>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RemoteSession>().HasOne<AgentRecord>().WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AuditLog>().HasOne<RemoteSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<AuditLog>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<AuditLog>().HasOne<AgentRecord>().WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.SetNull);
    }
}
