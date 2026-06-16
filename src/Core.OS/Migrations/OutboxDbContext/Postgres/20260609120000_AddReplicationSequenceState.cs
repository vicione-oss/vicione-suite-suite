using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.OS.Migrations.OutboxDbContext.Postgres
{
    /// <inheritdoc />
    public partial class AddReplicationSequenceState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReplicationSequenceState",
                schema: "outbox",
                columns: table => new
                {
                    ContextType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplicationSequenceState", x => x.ContextType);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReplicationSequenceState",
                schema: "outbox");
        }
    }
}
