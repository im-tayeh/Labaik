using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Labaik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySupervisorRoster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanySupervisors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySupervisors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanySupervisors_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanySupervisors_CompanyId",
                table: "CompanySupervisors",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanySupervisors_UserId_CompanyId",
                table: "CompanySupervisors",
                columns: new[] { "UserId", "CompanyId" },
                unique: true);

            // Backfill: supervisors already assigned to company groups join that company's roster
            // (excluding the owner account), so assignments and counts stay consistent.
            migrationBuilder.Sql("""
                INSERT INTO "CompanySupervisors" ("Id", "UserId", "AddedAtUtc", "CompanyId")
                SELECT gen_random_uuid(), x."LeaderId", now(), x."CompanyId"
                FROM (SELECT DISTINCT g."CompanyId", g."LeaderId"
                      FROM "Groups" g
                      JOIN "Companies" c ON c."Id" = g."CompanyId"
                      WHERE g."LeaderId" IS NOT NULL AND g."LeaderId" <> c."OwnerUserId") x;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanySupervisors");
        }
    }
}
