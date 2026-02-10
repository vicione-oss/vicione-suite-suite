using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.OS.Migrations.UserDbContext.Postgres
{
    /// <inheritdoc />
    public partial class ResetFeatureClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"user\".\"AspNetRoleClaims\"");
            migrationBuilder.Sql("DELETE FROM \"user\".\"AspNetUserClaims\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
