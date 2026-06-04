using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Engagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameLearnersToXpAccounts : Migration
    {
        // EF scaffolded a drop+create because it diffs the model structurally and can't
        // infer a rename. We replace it with an explicit table rename so existing XP rows
        // (and the AppliedAwards FK, which follows the table) survive the migration.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.RenameTable(
                name: "Learners",
                schema: "engagement",
                newName: "XpAccounts",
                newSchema: "engagement"); // explicit: stay in 'engagement' (null => moves to dbo)

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.RenameTable(
                name: "XpAccounts",
                schema: "engagement",
                newName: "Learners",
                newSchema: "engagement");
    }
}
