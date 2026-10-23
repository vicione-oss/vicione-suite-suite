using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.OS.Migrations.UserDbContext.Postgres
{
    /// <inheritdoc />
    public partial class RemoveExpiryDateOfUsersWithoutPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"user\".\"AspNetUsers\" SET \"PasswordExpirationDate\" = NULL "
                                 + "WHERE \"PasswordHash\" IS NULL AND \"PasswordExpirationDate\" = '-infinity'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
