using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JournalRecall.Api.Migrations
{
    /// <inheritdoc />
    public partial class LwwClientSaveOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RawDraftRevisionNumber",
                table: "sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "RawDraftSavedAt",
                table: "sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SettingsSavedAt",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            // Backfill (issue 0032): before LWW the current Draft always was the stream head, saved at
            // the Session's last write — so existing rows pin the Draft to the head Revision and claim
            // UpdatedAt (UTC ticks) as the save time. SettingsSavedAt stays null (never saved yet).
            migrationBuilder.Sql(
                """
                UPDATE sessions SET
                    "RawDraftRevisionNumber" = (
                        SELECT COUNT(*) FROM session_raw_revisions r WHERE r."SessionId" = sessions."Id"),
                    "RawDraftSavedAt" = "UpdatedAt";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RawDraftRevisionNumber",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "RawDraftSavedAt",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "SettingsSavedAt",
                table: "AspNetUsers");
        }
    }
}
