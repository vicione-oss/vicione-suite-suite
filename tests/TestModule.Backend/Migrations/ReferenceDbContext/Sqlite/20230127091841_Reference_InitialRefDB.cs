using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestModule.Migrations.ReferenceDbContext.Sqlite
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
                    deptno = table.Column<string>(name: "dept_no", type: "TEXT", maxLength: 4, nullable: false),
                    deptname = table.Column<string>(name: "dept_name", type: "TEXT", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.deptno);
                    table.CheckConstraint("departments_length_dept_name", "Length(dept_name) < 41 ");
                    table.CheckConstraint("departments_length_dept_no", "Length(dept_no) < 5 ");
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "reference",
                columns: table => new
                {
                    empno = table.Column<int>(name: "emp_no", type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    birthdate = table.Column<DateTime>(name: "birth_date", type: "DATE", nullable: false),
                    firstname = table.Column<string>(name: "first_name", type: "TEXT", maxLength: 14, nullable: false),
                    lastname = table.Column<string>(name: "last_name", type: "TEXT", maxLength: 16, nullable: false),
                    gender = table.Column<int>(type: "INTEGER", nullable: false),
                    hiredate = table.Column<DateTime>(name: "hire_date", type: "DATE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.empno);
                    table.CheckConstraint("employees_length_first_name", "Length(first_name) < 15 ");
                    table.CheckConstraint("employees_length_last_name", "Length(last_name) < 17 ");
                });

            migrationBuilder.CreateTable(
                name: "DepartmentEmployees",
                schema: "reference",
                columns: table => new
                {
                    empno = table.Column<int>(name: "emp_no", type: "INTEGER", nullable: false),
                    deptno = table.Column<string>(name: "dept_no", type: "TEXT", nullable: false),
                    fromdate = table.Column<DateTime>(name: "from_date", type: "DATE", nullable: false),
                    todate = table.Column<DateTime>(name: "to_date", type: "DATE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentEmployees", x => new { x.empno, x.deptno });
                    table.CheckConstraint("deptempl_length_dept_no", "Length(dept_no) < 5 ");
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
                    empno = table.Column<int>(name: "emp_no", type: "INTEGER", nullable: false),
                    deptno = table.Column<string>(name: "dept_no", type: "TEXT", nullable: false),
                    fromdate = table.Column<DateTime>(name: "from_date", type: "DATE", nullable: false),
                    todate = table.Column<DateTime>(name: "to_date", type: "DATE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentManagers", x => new { x.empno, x.deptno });
                    table.CheckConstraint("deptmanager_length_dept_no", "Length(dept_no) < 5 ");
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
                    empno = table.Column<int>(name: "emp_no", type: "INTEGER", nullable: false),
                    fromdate = table.Column<DateTime>(name: "from_date", type: "DATE", nullable: false),
                    salary = table.Column<int>(type: "INTEGER", nullable: false),
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
                    empno = table.Column<int>(name: "emp_no", type: "INTEGER", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    fromdate = table.Column<DateTime>(name: "from_date", type: "DATE", nullable: false),
                    todate = table.Column<DateTime>(name: "to_date", type: "DATE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Titles", x => new { x.empno, x.title, x.fromdate });
                    table.CheckConstraint("titles_length_title", "Length(title) < 257 ");
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

            migrationBuilder.CreateIndex(
                name: "IX_Departments_dept_name",
                schema: "reference",
                table: "Departments",
                column: "dept_name",
                unique: true);
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
