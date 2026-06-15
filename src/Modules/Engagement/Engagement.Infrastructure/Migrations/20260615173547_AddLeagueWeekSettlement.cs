using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Engagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueWeekSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeagueWeekSettlements",
                schema: "engagement",
                columns: table => new
                {
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    SettledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueWeekSettlements", x => x.WeekStart);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeagueWeekSettlements",
                schema: "engagement");
        }
    }
}
