using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JournalRecall.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "corrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CanonicalTerm = table.Column<string>(type: "TEXT", nullable: false),
                    Mishearings = table.Column<string>(type: "TEXT", nullable: false),
                    HardReplace = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_corrections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_corrections_UserId",
                table: "corrections",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "corrections");
        }
    }
}
