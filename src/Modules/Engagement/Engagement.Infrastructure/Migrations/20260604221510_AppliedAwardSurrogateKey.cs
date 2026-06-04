using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Engagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AppliedAwardSurrogateKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_AppliedAwards",
                schema: "engagement",
                table: "AppliedAwards");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                schema: "engagement",
                table: "AppliedAwards",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppliedAwards",
                schema: "engagement",
                table: "AppliedAwards",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_AppliedAwards_LearnerId_SourceId",
                schema: "engagement",
                table: "AppliedAwards",
                columns: new[] { "LearnerId", "SourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_AppliedAwards",
                schema: "engagement",
                table: "AppliedAwards");

            migrationBuilder.DropIndex(
                name: "IX_AppliedAwards_LearnerId_SourceId",
                schema: "engagement",
                table: "AppliedAwards");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "engagement",
                table: "AppliedAwards");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppliedAwards",
                schema: "engagement",
                table: "AppliedAwards",
                columns: new[] { "LearnerId", "SourceId" });
        }
    }
}
