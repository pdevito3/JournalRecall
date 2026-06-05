using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JournalRecall.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "sessions",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "sessions",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LocationCaptureEnabled",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "LocationCaptureEnabled",
                table: "AspNetUsers");
        }
    }
}
