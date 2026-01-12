#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Core.OS.Migrations.ApplicationDbContext.Sqlite
{
    public partial class Application_AddRegistrationTimestamps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstTimeRegistered",
                schema: "app",
                table: "InstanceInfo",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRegistered",
                schema: "app",
                table: "InstanceInfo",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstTimeRegistered",
                schema: "app",
                table: "InstanceInfo");

            migrationBuilder.DropColumn(
                name: "LastRegistered",
                schema: "app",
                table: "InstanceInfo");
        }
    }
}
