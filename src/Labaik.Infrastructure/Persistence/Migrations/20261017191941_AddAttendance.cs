using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Labaik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AttendanceConfirmedAtUtc",
                table: "GroupMembers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttendanceStatus",
                table: "GroupMembers",
                type: "integer",
                nullable: false,
                defaultValue: 2); // existing members start Absent (AttendanceStatus.Absent)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttendanceConfirmedAtUtc",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "AttendanceStatus",
                table: "GroupMembers");
        }
    }
}
