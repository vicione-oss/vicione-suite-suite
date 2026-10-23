using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.OS.Migrations.UserDbContext.Sqlite
{
    /// <inheritdoc />
    public partial class RemoveExpiryDateOfUsersWithoutPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"AspNetUsers\" SET \"PasswordExpirationDate\" = NULL "
                                 + "WHERE \"PasswordHash\" IS NULL AND \"PasswordExpirationDate\" = '0001-01-01 00:00:00+00:00'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
