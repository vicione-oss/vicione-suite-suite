using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TestModule.Migrations.ReferenceDbContext.Postgres
{
    /// <inheritdoc />
    public partial class ReferenceInitialRefDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reference");

            migrationBuilder.CreateTable(
                name: "Departments",
                schema: "reference",
                columns: table => new
                {
                    deptno = table.Column<string>(name: "dept_no", type: "character varying(4)", maxLength: 4, nullable: false),
                    deptname = table.Column<string>(name: "dept_name", type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.deptno);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "reference",
                columns: table => new
                {
                    empno = table.Column<int>(name: "emp_no", type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    birthdate = table.Column<DateTime>(name: "birth_date", type: "DATE", nullable: false),
                    firstname = table.Column<string>(name: "first_name", type: "character varying(14)", maxLength: 14, nullable: false),
                    lastname = table.Column<string>(name: "last_name", type: "character varying(16)", maxLength: 16, nullable: false),
                    gender = table.Column<int>(type: "integer", nullable: false),
                    hiredate = table.Column<DateTime>(name: "hire_date", type: "DATE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.empno);
                });

            migrationBuilder.CreateTable(
                name: "DepartmentEmployees",
                schema: "reference",
                columns: table => new
                {
                    empno = table.Column<int>(name: "emp_no", type: "integer", nullable: false),
                    deptno = table.Column<string>(name: "dept_no", type: "character varying(4)", nullable: false),
                    fromdate = table.Column<DateTime>(name: "from_date", type: "DATE", nullable: false),
                    todate = table.Column<DateTime>(name: "to_date", type: "DATE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentEmployees", x => new { x.empno, x.deptno });
                    table.ForeignKey(
                        name: "FK_DepartmentEmployees_Departments_dept_no",
                        column: x => x.deptno,
                        principalSchema: "reference",
                        principalTable: "Departments",
                        principalColumn: "dept_no",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepartmentEmployees_Employees_emp_no",
                        column: x => x.empno,
                        principalSchema: "reference",
                        principalTable: "Employees",
                        principalColumn: "emp_no",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DepartmentManagers",
                schema: "reference",
                columns: table => new
                {
                    empno = table.Column<int>(name: "emp_no", type: "integer", nullable: false),
                    deptno = table.Column<string>(name: "dept_no", type: "character varying(4)", nullable: false),
                    fromdate = table.Column<DateTime>(name: "from_date", type: "DATE", nullable: false),
                    todate = table.Column<DateTime>(name: "to_date", type: "DATE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentManagers", x => new { x.empno, x.deptno });
                    table.ForeignKey(
                        name: "FK_DepartmentManagers_Departments_dept_no",
                        column: x => x.deptno,
                        principalSchema: "reference",
                        principalTable: "Departments",
                        principalColumn: "dept_no",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepartmentManagers_Employees_emp_no",
                        column: x => x.empno,
                        principalSchema: "reference",
                        principalTable: "Employees",
                        principalColumn: "emp_no",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Salaries",
                schema: "reference",
                columns: table => new
                {
                    empno = table.Column<int>(name: "emp_no", type: "integer", nullable: false),
                    fromdate = table.Column<DateTime>(name: "from_date", type: "DATE", nullable: false),
                    salary = table.Column<int>(type: "integer", nullable: false),
                    todate = table.Column<DateTime>(name: "to_date", type: "DATE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Salaries", x => new { x.empno, x.fromdate });
                    table.ForeignKey(
                        name: "FK_Salaries_Employees_emp_no",
                        column: x => x.empno,
                        principalSchema: "reference",
                        principalTable: "Employees",
                        principalColumn: "emp_no",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Titles",
                schema: "reference",
                columns: table => new
                {
                    empno = table.Column<int>(name: "emp_no", type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    fromdate = table.Column<DateTime>(name: "from_date", type: "DATE", nullable: false),
                    todate = table.Column<DateTime>(name: "to_date", type: "DATE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Titles", x => new { x.empno, x.title, x.fromdate });
                    table.ForeignKey(
                        name: "FK_Titles_Employees_emp_no",
                        column: x => x.empno,
                        principalSchema: "reference",
                        principalTable: "Employees",
                        principalColumn: "emp_no",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentEmployees_dept_no",
                schema: "reference",
                table: "DepartmentEmployees",
                column: "dept_no");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentManagers_dept_no",
                schema: "reference",
                table: "DepartmentManagers",
                column: "dept_no");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepartmentEmployees",
                schema: "reference");

            migrationBuilder.DropTable(
                name: "DepartmentManagers",
                schema: "reference");

            migrationBuilder.DropTable(
                name: "Salaries",
                schema: "reference");

            migrationBuilder.DropTable(
                name: "Titles",
                schema: "reference");

            migrationBuilder.DropTable(
                name: "Departments",
                schema: "reference");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "reference");
        }
    }
}
