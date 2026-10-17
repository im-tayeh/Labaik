using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Labaik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnrichAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "GroupId",
                table: "Announcements",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "Audience",
                table: "Announcements",
                type: "integer",
                nullable: false,
                defaultValue: 1); // existing rows: Group / Normal

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Announcements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GatheringTimeUtc",
                table: "Announcements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Announcements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Importance",
                table: "Announcements",
                type: "integer",
                nullable: false,
                defaultValue: 1); // existing rows: Group / Normal

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Announcements",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_CompanyId_Audience_CreatedAtUtc",
                table: "Announcements",
                columns: new[] { "CompanyId", "Audience", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Announcements_CompanyId_Audience_CreatedAtUtc",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "Audience",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "GatheringTimeUtc",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "Importance",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Announcements");

            migrationBuilder.AlterColumn<Guid>(
                name: "GroupId",
                table: "Announcements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
