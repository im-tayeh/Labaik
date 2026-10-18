using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Labaik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandCompanyProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Companies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Companies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommercialRegistrationNumber",
                table: "Companies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactFullName",
                table: "Companies",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactJobTitle",
                table: "Companies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactMobile",
                table: "Companies",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ContractEndDate",
                table: "Companies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractFileUrl",
                table: "Companies",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractReferenceNumber",
                table: "Companies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ContractStartDate",
                table: "Companies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Companies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsStopped",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Companies",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxPilgrims",
                table: "Companies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxSupervisors",
                table: "Companies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficialEmail",
                table: "Companies",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryPhone",
                table: "Companies",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ServiceChatWithGuides",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ServiceDuaAndRituals",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ServiceGpsTracking",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ServiceInstantNotifications",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ServiceReportsAndStats",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionPeriod",
                table: "Companies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubscriptionPrice",
                table: "Companies",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatId",
                table: "Companies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_CommercialRegistrationNumber",
                table: "Companies",
                column: "CommercialRegistrationNumber",
                unique: true,
                filter: "\"CommercialRegistrationNumber\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Companies_CommercialRegistrationNumber",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "CommercialRegistrationNumber",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ContactFullName",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ContactJobTitle",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ContactMobile",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ContractEndDate",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ContractFileUrl",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ContractReferenceNumber",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ContractStartDate",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "IsStopped",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "MaxPilgrims",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "MaxSupervisors",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "OfficialEmail",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PrimaryPhone",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ServiceChatWithGuides",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ServiceDuaAndRituals",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ServiceGpsTracking",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ServiceInstantNotifications",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ServiceReportsAndStats",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "SubscriptionPeriod",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "SubscriptionPrice",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "VatId",
                table: "Companies");
        }
    }
}
