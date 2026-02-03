using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.OS.Migrations.ApplicationDbContext.Sqlite
{
    /// <inheritdoc />
    public partial class AddSystemType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SystemType",
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
                name: "SystemType",
                schema: "app",
                table: "InstanceInfo");
        }
    }
}
