using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Labaik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastMemberNumber",
                table: "Groups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MemberNumber",
                table: "GroupMembers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "GroupMembers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PilgrimProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Gender = table.Column<int>(type: "integer", nullable: true),
                    Nationality = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    PhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HealthNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilgrimProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PilgrimProfiles_UserId",
                table: "PilgrimProfiles",
                column: "UserId",
                unique: true);

            // Backfill: number existing pilgrims (Role 2 = Member) per group in join order,
            // then start each group's counter after its highest number.
            migrationBuilder.Sql(@"
UPDATE ""GroupMembers"" AS m SET ""MemberNumber"" = r.rn
FROM (SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY ""GroupId"" ORDER BY ""JoinedAtUtc"", ""Id"") AS rn
      FROM ""GroupMembers"" WHERE ""Role"" = 2) AS r
WHERE m.""Id"" = r.""Id"";

UPDATE ""Groups"" AS g SET ""LastMemberNumber"" =
    COALESCE((SELECT MAX(gm.""MemberNumber"") FROM ""GroupMembers"" AS gm WHERE gm.""GroupId"" = g.""Id""), 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PilgrimProfiles");

            migrationBuilder.DropColumn(
                name: "LastMemberNumber",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "MemberNumber",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "GroupMembers");
        }
    }
}
