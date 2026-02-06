using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.OS.Migrations.UserDbContext.Postgres
{
    public partial class SetEmptyMailAddressesToDefault : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE  \"user\".\"AspNetUsers\" SET \"Email\" = 'default@example.com' "
                                 + "WHERE \"Email\" IS NULL OR \"Email\" = ''");
            migrationBuilder.Sql("UPDATE \"user\".\"AspNetUsers\" SET \"NormalizedEmail\" = 'DEFAULT@EXAMPLE.COM'"
                                 + "WHERE \"NormalizedEmail\" IS NULL OR \"NormalizedEmail\" = ''");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
