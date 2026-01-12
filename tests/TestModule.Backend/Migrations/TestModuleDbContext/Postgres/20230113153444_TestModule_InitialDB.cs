using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TestModule.Migrations.TestModuleDbContext.Postgres
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
                    empno = table.Column<int>(name: "emp_no", type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Salutaion = table.Column<string>(type: "text", nullable: false)
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineName = table.Column<string>(type: "text", nullable: false),
                    MachineDescription = table.Column<string>(type: "text", nullable: false),
                    Users = table.Column<int>(type: "integer", nullable: false),
                    Built = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    { 1L, new DateTime(2001, 2, 12, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2012, 12, 12, 0, 0, 0, 0, DateTimeKind.Utc), "Presse für Stoßfänger", new Guid("ff79fbab-2399-468f-b2ab-1109205dec08"), "Presse T1000", 4 },
                    { 2L, new DateTime(2010, 6, 10, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2023, 1, 13, 15, 34, 43, 561, DateTimeKind.Utc).AddTicks(6198), "Presse für Stoßfänger - T2000", new Guid("ca05a1d1-6833-4d76-9231-1ecb60f47fa5"), "Presse T2000", 3 },
                    { 3L, new DateTime(2011, 11, 30, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2017, 1, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Presse für Stoßfänger - T5000", new Guid("d6aea99b-1f4a-4ed3-ae3f-aef54bb4aad9"), "Presse T5000", 1 }
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
