using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Labaik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGatheringPoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatheringPoint_Address",
                table: "Groups",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GatheringPoint_GatheringTimeUtc",
                table: "Groups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GatheringPoint_Latitude",
                table: "Groups",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GatheringPoint_Longitude",
                table: "Groups",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GatheringPoint_UpdatedAtUtc",
                table: "Groups",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatheringPoint_Address",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "GatheringPoint_GatheringTimeUtc",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "GatheringPoint_Latitude",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "GatheringPoint_Longitude",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "GatheringPoint_UpdatedAtUtc",
                table: "Groups");
        }
    }
}
