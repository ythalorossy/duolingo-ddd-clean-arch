using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialLearning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "learning");

            migrationBuilder.CreateTable(
                name: "Courses",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lessons",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lessons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Units",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "learning",
                table: "Courses",
                columns: new[] { "Id", "Language", "Title" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), "es", "Spanish" });

            migrationBuilder.InsertData(
                schema: "learning",
                table: "Lessons",
                columns: new[] { "Id", "IsPublished", "Position", "Title", "UnitId" },
                values: new object[,]
                {
                    { new Guid("33333333-3333-3333-3333-333333333331"), true, 1, "Greetings", new Guid("22222222-2222-2222-2222-222222222221") },
                    { new Guid("33333333-3333-3333-3333-333333333332"), true, 2, "The verb ser", new Guid("22222222-2222-2222-2222-222222222221") },
                    { new Guid("33333333-3333-3333-3333-333333333333"), true, 1, "At the cafe", new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("33333333-3333-3333-3333-333333333334"), false, 2, "Ordering dessert", new Guid("22222222-2222-2222-2222-222222222222") }
                });

            migrationBuilder.InsertData(
                schema: "learning",
                table: "Units",
                columns: new[] { "Id", "CourseId", "Position", "Title" },
                values: new object[,]
                {
                    { new Guid("22222222-2222-2222-2222-222222222221"), new Guid("11111111-1111-1111-1111-111111111111"), 1, "Basics" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("11111111-1111-1111-1111-111111111111"), 2, "Food" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Courses",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "Lessons",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "Units",
                schema: "learning");
        }
    }
}
