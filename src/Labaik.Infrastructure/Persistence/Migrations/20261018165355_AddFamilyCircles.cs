using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Labaik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyCircles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FamilyCircles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    JoinCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyCircles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FamilyCircleMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsSharingLocation = table.Column<bool>(type: "boolean", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyCircleMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyCircleMembers_FamilyCircles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "FamilyCircles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyCircleMembers_CircleId",
                table: "FamilyCircleMembers",
                column: "CircleId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyCircleMembers_UserId_CircleId",
                table: "FamilyCircleMembers",
                columns: new[] { "UserId", "CircleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FamilyCircles_JoinCode",
                table: "FamilyCircles",
                column: "JoinCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FamilyCircleMembers");

            migrationBuilder.DropTable(
                name: "FamilyCircles");
        }
    }
}
