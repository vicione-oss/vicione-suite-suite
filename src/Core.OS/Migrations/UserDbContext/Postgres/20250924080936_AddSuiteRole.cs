using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.OS.Migrations.UserDbContext.Postgres
{
    /// <inheritdoc />
    public partial class AddSuiteRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "user",
                table: "AspNetRoles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Managed",
                schema: "user",
                table: "AspNetRoles",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                schema: "user",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "Managed",
                schema: "user",
                table: "AspNetRoles");
        }
    }
}
