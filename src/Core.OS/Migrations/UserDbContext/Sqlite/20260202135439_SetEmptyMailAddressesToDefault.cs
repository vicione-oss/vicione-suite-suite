using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.OS.Migrations.UserDbContext.Sqlite
{
    public partial class SetEmptyMailAddressesToDefault : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"AspNetUsers\" SET \"Email\" = 'default@example.com'"
                                 + "WHERE \"Email\" IS NULL OR \"Email\" = ''");
            migrationBuilder.Sql("UPDATE \"AspNetUsers\" SET \"NormalizedEmail\" = 'DEFAULT@EXAMPLE.COM'"
                                 + "WHERE \"NormalizedEmail\" IS NULL OR \"NormalizedEmail\" = ''");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
