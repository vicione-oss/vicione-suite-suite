using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Burger.Backend.Ef.Migrations.BurgerDbContext.Postgres
{
    public partial class Init : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "burger");

            migrationBuilder.CreateTable(
                name: "OrderBurgerState",
                schema: "burger",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentState = table.Column<int>(type: "integer", nullable: false),
                    Burgers = table.Column<string>(type: "text", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderBurgerState", x => x.CorrelationId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderBurgerState",
                schema: "burger");
        }
    }
}
