using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Engagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReshapeLeagueStandingKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LeagueStandings",
                schema: "engagement",
                table: "LeagueStandings");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LeagueStandings",
                schema: "engagement",
                table: "LeagueStandings",
                columns: new[] { "LearnerId", "WeekStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LeagueStandings",
                schema: "engagement",
                table: "LeagueStandings");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LeagueStandings",
                schema: "engagement",
                table: "LeagueStandings",
                column: "LearnerId");
        }
    }
}
