using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JournalRecall.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auth_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SelfRegistrationEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_settings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_settings");
        }
    }
}
