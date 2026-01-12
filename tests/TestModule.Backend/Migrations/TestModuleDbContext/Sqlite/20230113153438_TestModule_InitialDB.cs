using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestModule.Migrations.TestModuleDbContext.Sqlite
{
    /// <inheritdoc />
    public partial class TestModuleInitialDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "datatypes");

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "datatypes",
                columns: table => new
                {
                    empno = table.Column<int>(name: "emp_no", type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Salutaion = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.empno);
                });

            migrationBuilder.CreateTable(
                name: "SimpleDataTypes",
                schema: "datatypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MachineName = table.Column<string>(type: "TEXT", nullable: false),
                    MachineDescription = table.Column<string>(type: "TEXT", nullable: false),
                    Users = table.Column<int>(type: "INTEGER", nullable: false),
                    Built = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimpleDataTypes", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "datatypes",
                table: "Employees",
                columns: ["emp_no", "Name", "Salutaion"],
                values: new object[,]
                {
                    { 1, "Hans.Wurst", "Mr" },
                    { 2, "Karla.Kolumna", "Mrs" }
                });

            migrationBuilder.InsertData(
                schema: "datatypes",
                table: "SimpleDataTypes",
                columns: ["Id", "Built", "Created", "MachineDescription", "MachineId", "MachineName", "Users"],
                values: new object[,]
                {
                    { 1L, new DateTime(2001, 2, 12, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2012, 12, 12, 0, 0, 0, 0, DateTimeKind.Utc), "Presse für Stoßfänger", new Guid("71a29584-3012-484b-8788-03812c638254"), "Presse T1000", 4 },
                    { 2L, new DateTime(2010, 6, 10, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2023, 1, 13, 15, 34, 37, 854, DateTimeKind.Utc).AddTicks(8633), "Presse für Stoßfänger - T2000", new Guid("a55aab33-74e0-4191-bf2b-183ddf474c31"), "Presse T2000", 3 },
                    { 3L, new DateTime(2011, 11, 30, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2017, 1, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Presse für Stoßfänger - T5000", new Guid("0aff3abe-f36b-4a74-a279-2b631940dd73"), "Presse T5000", 1 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Employees",
                schema: "datatypes");

            migrationBuilder.DropTable(
                name: "SimpleDataTypes",
                schema: "datatypes");
        }
    }
}
