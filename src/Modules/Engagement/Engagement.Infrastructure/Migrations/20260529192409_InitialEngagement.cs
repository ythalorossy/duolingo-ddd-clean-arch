using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Engagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialEngagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "engagement");

            migrationBuilder.CreateTable(
                name: "Learners",
                schema: "engagement",
                columns: table => new
                {
                    LearnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalXp = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Learners", x => x.LearnerId);
                });

            migrationBuilder.CreateTable(
                name: "AppliedAwards",
                schema: "engagement",
                columns: table => new
                {
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppliedAwards", x => new { x.LearnerId, x.SourceId });
                    table.ForeignKey(
                        name: "FK_AppliedAwards_Learners_LearnerId",
                        column: x => x.LearnerId,
                        principalSchema: "engagement",
                        principalTable: "Learners",
                        principalColumn: "LearnerId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppliedAwards",
                schema: "engagement");

            migrationBuilder.DropTable(
                name: "Learners",
                schema: "engagement");
        }
    }
}
