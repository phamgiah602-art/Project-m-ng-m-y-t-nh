using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RemoteControlLAN.Gateway.Data;

#nullable disable

namespace RemoteControlLAN.Gateway.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608010001_AddIsAdminToUser")]
public partial class AddIsAdminToUser : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "IsAdmin", table: "Users", nullable: false, defaultValue: false);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsAdmin", table: "Users");
    }
}
