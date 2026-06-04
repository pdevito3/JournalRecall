using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JournalRecall.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRawRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "session_raw_revisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_raw_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_raw_revisions_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_raw_revisions_SessionId_RevisionNumber",
                table: "session_raw_revisions",
                columns: new[] { "SessionId", "RevisionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_raw_revisions");
        }
    }
}
