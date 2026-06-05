using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JournalRecall.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCleanupCleanedSynopsis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CleanedDraft",
                table: "sessions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CleanupStatus",
                table: "sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LastCleanedRawRevisionNumber",
                table: "sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Synopsis",
                table: "sessions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "session_cleaned_revisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_cleaned_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_cleaned_revisions_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_cleaned_revisions_SessionId_RevisionNumber",
                table: "session_cleaned_revisions",
                columns: new[] { "SessionId", "RevisionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_cleaned_revisions");

            migrationBuilder.DropColumn(
                name: "CleanedDraft",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "CleanupStatus",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "LastCleanedRawRevisionNumber",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "Synopsis",
                table: "sessions");
        }
    }
}
