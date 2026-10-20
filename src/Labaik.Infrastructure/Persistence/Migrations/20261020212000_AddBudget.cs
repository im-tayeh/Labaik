using Labaik.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Labaik.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261020212000_AddBudget")]
public partial class AddBudget : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Budgets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Budgets", x => x.Id));

        migrationBuilder.CreateTable(
            name: "BudgetExpenses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric", nullable: false),
                Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                SpentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                BudgetId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BudgetExpenses", x => x.Id);
                table.ForeignKey("FK_BudgetExpenses_Budgets_BudgetId", x => x.BudgetId, "Budgets", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_Budgets_UserId", "Budgets", "UserId", unique: true);
        migrationBuilder.CreateIndex("IX_BudgetExpenses_BudgetId", "BudgetExpenses", "BudgetId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("BudgetExpenses");
        migrationBuilder.DropTable("Budgets");
    }
}
