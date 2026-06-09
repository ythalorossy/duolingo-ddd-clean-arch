using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Engagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStreakFreeze : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FreezeBalance",
                schema: "engagement",
                table: "LearnerStreaks",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreezeBalance",
                schema: "engagement",
                table: "LearnerStreaks");
        }
    }
}
