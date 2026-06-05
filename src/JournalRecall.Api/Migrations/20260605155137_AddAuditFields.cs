using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JournalRecall.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CreatedAt",
                table: "summaries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "summaries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UpdatedAt",
                table: "summaries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "summaries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UpdatedAt",
                table: "sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "corrections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UpdatedAt",
                table: "corrections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "corrections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreatedAt",
                table: "ai_provider_settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "ai_provider_settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UpdatedAt",
                table: "ai_provider_settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "ai_provider_settings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "summaries");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "summaries");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "summaries");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "summaries");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "corrections");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "corrections");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "corrections");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ai_provider_settings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ai_provider_settings");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ai_provider_settings");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ai_provider_settings");
        }
    }
}
