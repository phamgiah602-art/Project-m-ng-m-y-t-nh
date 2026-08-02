using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RemoteControlLAN.Gateway.Data;

#nullable disable

namespace RemoteControlLAN.Gateway.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608010002_EnsureOneActiveSessionPerAgent")]
public partial class EnsureOneActiveSessionPerAgent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS IX_Sessions_OneActiveAgent ON Sessions (AgentId) WHERE Status = 'Active';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Sessions_OneActiveAgent;");
    }
}
