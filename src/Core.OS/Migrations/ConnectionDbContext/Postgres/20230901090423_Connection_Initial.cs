using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.OS.Migrations.ConnectionDbContext.Postgres
{
    /// <inheritdoc />
    public partial class Connection_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "connection");

            migrationBuilder.CreateTable(
                name: "Connections",
                schema: "connection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Json = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Connections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                schema: "connection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Protected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConnectionTag",
                schema: "connection",
                columns: table => new
                {
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionTag", x => new { x.ConnectionId, x.TagId });
                    table.ForeignKey(
                        name: "FK_ConnectionTag_Connections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalSchema: "connection",
                        principalTable: "Connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConnectionTag_Tags_TagId",
                        column: x => x.TagId,
                        principalSchema: "connection",
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionTag_TagId",
                schema: "connection",
                table: "ConnectionTag",
                column: "TagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectionTag",
                schema: "connection");

            migrationBuilder.DropTable(
                name: "Connections",
                schema: "connection");

            migrationBuilder.DropTable(
                name: "Tags",
                schema: "connection");
        }
    }
}
