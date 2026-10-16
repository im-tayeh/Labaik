using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Labaik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRitualGuide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RitualProgresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PilgrimageType = table.Column<int>(type: "integer", nullable: false),
                    CurrentOrder = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RitualProgresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RitualSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PilgrimageType = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RitualSteps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompletedRitualSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RitualProgressId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompletedRitualSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompletedRitualSteps_RitualProgresses_RitualProgressId",
                        column: x => x.RitualProgressId,
                        principalTable: "RitualProgresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompletedRitualSteps_RitualProgressId_StepOrder",
                table: "CompletedRitualSteps",
                columns: new[] { "RitualProgressId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RitualProgresses_UserId_PilgrimageType",
                table: "RitualProgresses",
                columns: new[] { "UserId", "PilgrimageType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RitualSteps_PilgrimageType_Order",
                table: "RitualSteps",
                columns: new[] { "PilgrimageType", "Order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompletedRitualSteps");

            migrationBuilder.DropTable(
                name: "RitualSteps");

            migrationBuilder.DropTable(
                name: "RitualProgresses");
        }
    }
}
