using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.OS.Migrations.ApplicationDbContext.Postgres
{
    /// <inheritdoc />
    public partial class Application_OnboardingState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OnboardingStates",
                schema: "app",
                columns: table => new
                {
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    ShowWizardWhenNotCompleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingStates", x => x.InstanceId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingStates",
                schema: "app");
        }
    }
}
