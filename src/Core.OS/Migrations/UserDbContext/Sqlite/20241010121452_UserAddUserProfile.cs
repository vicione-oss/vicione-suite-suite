#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Core.OS.Migrations.UserDbContext.Sqlite
{
    /// <inheritdoc />
    public partial class UserAddUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "user",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                schema: "user",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                schema: "user",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                schema: "user",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "user",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                schema: "user",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mobile",
                schema: "user",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                schema: "user",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street",
                schema: "user",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreetNumber",
                schema: "user",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                schema: "user",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "user",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                schema: "user",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                schema: "user",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Country",
                schema: "user",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Department",
                schema: "user",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FirstName",
                schema: "user",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Language",
                schema: "user",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastName",
                schema: "user",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Mobile",
                schema: "user",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Occupation",
                schema: "user",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Street",
                schema: "user",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StreetNumber",
                schema: "user",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                schema: "user",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Title",
                schema: "user",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                schema: "user",
                table: "AspNetUsers");
        }
    }
}
