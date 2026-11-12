using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Granary.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDietTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DietaryPreference",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "DietaryPreference",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<bool>(
                name: "IsPantryStaple",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "DietId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Diets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Diets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DietTranslations",
                columns: table => new
                {
                    DietId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietTranslations", x => new { x.DietId, x.LanguageId });
                    table.ForeignKey(
                        name: "FK_DietTranslations_Diets_DietId",
                        column: x => x.DietId,
                        principalTable: "Diets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DietTranslations_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeDiets",
                columns: table => new
                {
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DietId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeDiets", x => new { x.RecipeId, x.DietId });
                    table.ForeignKey(
                        name: "FK_RecipeDiets_Diets_DietId",
                        column: x => x.DietId,
                        principalTable: "Diets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeDiets_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DietId",
                table: "AspNetUsers",
                column: "DietId");

            migrationBuilder.CreateIndex(
                name: "IX_DietTranslations_LanguageId",
                table: "DietTranslations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeDiets_DietId",
                table: "RecipeDiets",
                column: "DietId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Diets_DietId",
                table: "AspNetUsers",
                column: "DietId",
                principalTable: "Diets",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Diets_DietId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "DietTranslations");

            migrationBuilder.DropTable(
                name: "RecipeDiets");

            migrationBuilder.DropTable(
                name: "Diets");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DietId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsPantryStaple",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "DietId",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<int>(
                name: "DietaryPreference",
                table: "Recipes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DietaryPreference",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
