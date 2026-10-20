using Labaik.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Labaik.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261020211000_RitualProgressSnapshot")]
public partial class RitualProgressSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>("ClientUpdatedAtUtc", "RitualProgresses", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>("CurrentStepKey", "RitualProgresses", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<int>("IhramState", "RitualProgresses", type: "integer", nullable: true);
        migrationBuilder.AddColumn<int>("SaeeLap", "RitualProgresses", type: "integer", nullable: true);
        migrationBuilder.AddColumn<int>("TawafRound", "RitualProgresses", type: "integer", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("ClientUpdatedAtUtc", "RitualProgresses");
        migrationBuilder.DropColumn("CurrentStepKey", "RitualProgresses");
        migrationBuilder.DropColumn("IhramState", "RitualProgresses");
        migrationBuilder.DropColumn("SaeeLap", "RitualProgresses");
        migrationBuilder.DropColumn("TawafRound", "RitualProgresses");
    }
}
