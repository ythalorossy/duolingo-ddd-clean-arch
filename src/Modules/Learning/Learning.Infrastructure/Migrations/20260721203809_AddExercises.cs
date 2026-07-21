using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExercises : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Exercises",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Choices = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LessonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectChoiceIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Exercises_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalSchema: "learning",
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "learning",
                table: "Exercises",
                columns: new[] { "Id", "Choices", "LessonId", "Position", "Prompt", "CorrectChoiceIndex" },
                values: new object[,]
                {
                    { new Guid("44444444-4444-4444-4444-000000000001"), "[\"Hola\",\"Adios\",\"Gracias\"]", new Guid("33333333-3333-3333-3333-333333333331"), 1, "How do you say hello?", 0 },
                    { new Guid("44444444-4444-4444-4444-000000000002"), "[\"Hola\",\"Adios\",\"Gracias\"]", new Guid("33333333-3333-3333-3333-333333333331"), 2, "How do you say goodbye?", 1 },
                    { new Guid("44444444-4444-4444-4444-000000000003"), "[\"soy\",\"es\",\"eres\"]", new Guid("33333333-3333-3333-3333-333333333332"), 1, "Yo ___ estudiante.", 0 },
                    { new Guid("44444444-4444-4444-4444-000000000004"), "[\"soy\",\"es\",\"eres\"]", new Guid("33333333-3333-3333-3333-333333333332"), 2, "Ella ___ profesora.", 1 },
                    { new Guid("44444444-4444-4444-4444-000000000005"), "[\"Un cafe, por favor\",\"Adios\",\"Gracias\"]", new Guid("33333333-3333-3333-3333-333333333333"), 1, "How do you order a coffee?", 0 },
                    { new Guid("44444444-4444-4444-4444-000000000006"), "[\"leche\",\"agua\",\"pan\"]", new Guid("33333333-3333-3333-3333-333333333333"), 2, "How do you say water?", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_LessonId",
                schema: "learning",
                table: "Exercises",
                column: "LessonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Exercises",
                schema: "learning");
        }
    }
}
