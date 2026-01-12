using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.OS.Migrations.ApplicationDbContext.Postgres
{
    /// <inheritdoc />
    public partial class ApplicationAddInstanceInformationRecoveryFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InRecoveryMode",
                schema: "app",
                table: "InstanceInfo",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InRecoveryMode",
                schema: "app",
                table: "InstanceInfo");
        }
    }
}
