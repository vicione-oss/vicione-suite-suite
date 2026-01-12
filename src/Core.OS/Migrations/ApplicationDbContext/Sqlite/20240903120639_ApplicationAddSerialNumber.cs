#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Core.OS.Migrations.ApplicationDbContext.Sqlite
{
    /// <inheritdoc />
    public partial class ApplicationAddSerialNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                schema: "app",
                table: "InstanceInfo",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE InstanceInfo SET SerialNumber = REPLACE(Id, '-', '')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SerialNumber",
                schema: "app",
                table: "InstanceInfo");
        }
    }
}
