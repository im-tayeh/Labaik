using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Labaik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChatReadStateAndLocationMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ChatLastReadAtUtc",
                table: "GroupMembers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "ChatMessages",
                type: "integer",
                nullable: false,
                defaultValue: 1); // existing messages are text (ChatMessageKind.Text)

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "ChatMessages",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "ChatMessages",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatLastReadAtUtc",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "ChatMessages");
        }
    }
}
