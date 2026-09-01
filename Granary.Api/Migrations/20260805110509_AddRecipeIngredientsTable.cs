using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Granary.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeIngredientsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredient_Product_ProductId",
                table: "RecipeIngredient");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredient_Recipe_RecipeId",
                table: "RecipeIngredient");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RecipeIngredient",
                table: "RecipeIngredient");

            migrationBuilder.RenameTable(
                name: "RecipeIngredient",
                newName: "RecipeIngredients");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredient_RecipeId",
                table: "RecipeIngredients",
                newName: "IX_RecipeIngredients_RecipeId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredient_ProductId",
                table: "RecipeIngredients",
                newName: "IX_RecipeIngredients_ProductId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RecipeIngredients",
                table: "RecipeIngredients",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredients_Product_ProductId",
                table: "RecipeIngredients",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredients_Recipe_RecipeId",
                table: "RecipeIngredients",
                column: "RecipeId",
                principalTable: "Recipe",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredients_Product_ProductId",
                table: "RecipeIngredients");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredients_Recipe_RecipeId",
                table: "RecipeIngredients");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RecipeIngredients",
                table: "RecipeIngredients");

            migrationBuilder.RenameTable(
                name: "RecipeIngredients",
                newName: "RecipeIngredient");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredients_RecipeId",
                table: "RecipeIngredient",
                newName: "IX_RecipeIngredient_RecipeId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredients_ProductId",
                table: "RecipeIngredient",
                newName: "IX_RecipeIngredient_ProductId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RecipeIngredient",
                table: "RecipeIngredient",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredient_Product_ProductId",
                table: "RecipeIngredient",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredient_Recipe_RecipeId",
                table: "RecipeIngredient",
                column: "RecipeId",
                principalTable: "Recipe",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
