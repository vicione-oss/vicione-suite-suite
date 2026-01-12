using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.OS.Migrations.ApplicationDbContext.Sqlite
{
    /// <inheritdoc />
    public partial class ApplicationAddInstanceInformationVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Version",
                schema: "app",
                table: "InstanceInfo",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                schema: "app",
                table: "InstanceInfo");
        }
    }
}
