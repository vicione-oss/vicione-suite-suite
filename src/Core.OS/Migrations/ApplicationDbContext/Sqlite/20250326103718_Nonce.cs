using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.OS.Migrations.ApplicationDbContext.Sqlite
{
    /// <inheritdoc />
    public partial class Nonce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Nonces",
                schema: "app",
                columns: table => new
                {
                    Value = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nonces", x => x.Value);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Nonces",
                schema: "app");
        }
    }
}
