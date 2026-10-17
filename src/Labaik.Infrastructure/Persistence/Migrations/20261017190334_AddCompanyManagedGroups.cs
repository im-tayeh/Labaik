using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Labaik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyManagedGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "LeaderId",
                table: "Groups",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TripId",
                table: "Groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Groups_CompanyId",
                table: "Groups",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_TripId",
                table: "Groups",
                column: "TripId");

            migrationBuilder.AddForeignKey(
                name: "FK_Groups_Companies_CompanyId",
                table: "Groups",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Groups_Trips_TripId",
                table: "Groups",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Groups_Companies_CompanyId",
                table: "Groups");

            migrationBuilder.DropForeignKey(
                name: "FK_Groups_Trips_TripId",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Groups_CompanyId",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Groups_TripId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "TripId",
                table: "Groups");

            migrationBuilder.AlterColumn<Guid>(
                name: "LeaderId",
                table: "Groups",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
