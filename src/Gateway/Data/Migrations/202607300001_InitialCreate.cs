using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RemoteControlLAN.Gateway.Data;

#nullable disable

namespace RemoteControlLAN.Gateway.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202607300001_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "Agents", columns: table => new { Id = table.Column<Guid>(nullable: false), AgentName = table.Column<string>(nullable: false), Platform = table.Column<string>(nullable: false), AgentSecretKeyHash = table.Column<string>(nullable: false), PairingPinHash = table.Column<string>(nullable: true), PairingPinExpiresAt = table.Column<DateTime>(nullable: true), LastSeenIp = table.Column<string>(nullable: true), LastOnlineAt = table.Column<DateTime>(nullable: true) }, constraints: table => table.PrimaryKey("PK_Agents", x => x.Id));
        migrationBuilder.CreateTable(name: "Users", columns: table => new { Id = table.Column<Guid>(nullable: false), Username = table.Column<string>(nullable: false), PasswordHash = table.Column<string>(nullable: false), CreatedAt = table.Column<DateTime>(nullable: false), FailedLoginCount = table.Column<int>(nullable: false), LockedUntil = table.Column<DateTime>(nullable: true) }, constraints: table => table.PrimaryKey("PK_Users", x => x.Id));
        migrationBuilder.CreateTable(name: "Sessions", columns: table => new { Id = table.Column<Guid>(nullable: false), UserId = table.Column<Guid>(nullable: false), AgentId = table.Column<Guid>(nullable: false), StartedAt = table.Column<DateTime>(nullable: false), EndedAt = table.Column<DateTime>(nullable: true), Status = table.Column<string>(nullable: false) }, constraints: table => { table.PrimaryKey("PK_Sessions", x => x.Id); table.ForeignKey("FK_Sessions_Agents_AgentId", x => x.AgentId, "Agents", "Id", onDelete: ReferentialAction.Restrict); table.ForeignKey("FK_Sessions_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Restrict); });
        migrationBuilder.CreateTable(name: "AuditLogs", columns: table => new { Id = table.Column<long>(nullable: false).Annotation("Sqlite:Autoincrement", true), SessionId = table.Column<Guid>(nullable: true), UserId = table.Column<Guid>(nullable: true), AgentId = table.Column<Guid>(nullable: true), Action = table.Column<string>(nullable: false), Payload = table.Column<string>(nullable: true), Timestamp = table.Column<DateTime>(nullable: false), Result = table.Column<string>(nullable: false) }, constraints: table => { table.PrimaryKey("PK_AuditLogs", x => x.Id); table.ForeignKey("FK_AuditLogs_Agents_AgentId", x => x.AgentId, "Agents", "Id", onDelete: ReferentialAction.SetNull); table.ForeignKey("FK_AuditLogs_Sessions_SessionId", x => x.SessionId, "Sessions", "Id", onDelete: ReferentialAction.SetNull); table.ForeignKey("FK_AuditLogs_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.SetNull); });
        migrationBuilder.CreateIndex(name: "IX_Agents_AgentName", table: "Agents", column: "AgentName", unique: true); migrationBuilder.CreateIndex(name: "IX_Sessions_AgentId_Status", table: "Sessions", columns: new[] { "AgentId", "Status" }); migrationBuilder.CreateIndex(name: "IX_Sessions_UserId", table: "Sessions", column: "UserId"); migrationBuilder.CreateIndex(name: "IX_AuditLogs_AgentId", table: "AuditLogs", column: "AgentId"); migrationBuilder.CreateIndex(name: "IX_AuditLogs_SessionId", table: "AuditLogs", column: "SessionId"); migrationBuilder.CreateIndex(name: "IX_AuditLogs_UserId", table: "AuditLogs", column: "UserId"); migrationBuilder.CreateIndex(name: "IX_Users_Username", table: "Users", column: "Username", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder) { migrationBuilder.DropTable("AuditLogs"); migrationBuilder.DropTable("Sessions"); migrationBuilder.DropTable("Agents"); migrationBuilder.DropTable("Users"); }
}
