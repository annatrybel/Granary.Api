using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Granary.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMealPlanRecipesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MealPlanRecipe_MealPlan_MealPlanId",
                table: "MealPlanRecipe");

            migrationBuilder.DropForeignKey(
                name: "FK_MealPlanRecipe_Recipe_RecipeId",
                table: "MealPlanRecipe");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MealPlanRecipe",
                table: "MealPlanRecipe");

            migrationBuilder.RenameTable(
                name: "MealPlanRecipe",
                newName: "MealPlanRecipes");

            migrationBuilder.RenameIndex(
                name: "IX_MealPlanRecipe_RecipeId",
                table: "MealPlanRecipes",
                newName: "IX_MealPlanRecipes_RecipeId");

            migrationBuilder.RenameIndex(
                name: "IX_MealPlanRecipe_MealPlanId",
                table: "MealPlanRecipes",
                newName: "IX_MealPlanRecipes_MealPlanId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MealPlanRecipes",
                table: "MealPlanRecipes",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlanRecipes_MealPlan_MealPlanId",
                table: "MealPlanRecipes",
                column: "MealPlanId",
                principalTable: "MealPlan",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlanRecipes_Recipe_RecipeId",
                table: "MealPlanRecipes",
                column: "RecipeId",
                principalTable: "Recipe",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MealPlanRecipes_MealPlan_MealPlanId",
                table: "MealPlanRecipes");

            migrationBuilder.DropForeignKey(
                name: "FK_MealPlanRecipes_Recipe_RecipeId",
                table: "MealPlanRecipes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MealPlanRecipes",
                table: "MealPlanRecipes");

            migrationBuilder.RenameTable(
                name: "MealPlanRecipes",
                newName: "MealPlanRecipe");

            migrationBuilder.RenameIndex(
                name: "IX_MealPlanRecipes_RecipeId",
                table: "MealPlanRecipe",
                newName: "IX_MealPlanRecipe_RecipeId");

            migrationBuilder.RenameIndex(
                name: "IX_MealPlanRecipes_MealPlanId",
                table: "MealPlanRecipe",
                newName: "IX_MealPlanRecipe_MealPlanId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MealPlanRecipe",
                table: "MealPlanRecipe",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlanRecipe_MealPlan_MealPlanId",
                table: "MealPlanRecipe",
                column: "MealPlanId",
                principalTable: "MealPlan",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlanRecipe_Recipe_RecipeId",
                table: "MealPlanRecipe",
                column: "RecipeId",
                principalTable: "Recipe",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
