using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.OS.Migrations.UserDbContext.Sqlite
{
    /// <inheritdoc />
    public partial class ResetFeatureClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"AspNetRoleClaims\"");
            migrationBuilder.Sql("DELETE FROM \"AspNetUserClaims\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
