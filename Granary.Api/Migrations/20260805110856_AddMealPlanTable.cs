using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Granary.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMealPlanTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MealPlan_AspNetUsers_UserId1",
                table: "MealPlan");

            migrationBuilder.DropForeignKey(
                name: "FK_MealPlanRecipes_MealPlan_MealPlanId",
                table: "MealPlanRecipes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MealPlan",
                table: "MealPlan");

            migrationBuilder.RenameTable(
                name: "MealPlan",
                newName: "MealPlans");

            migrationBuilder.RenameIndex(
                name: "IX_MealPlan_UserId1",
                table: "MealPlans",
                newName: "IX_MealPlans_UserId1");

            migrationBuilder.AlterColumn<string>(
                name: "MealType",
                table: "MealPlanRecipes",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "UserId1",
                table: "MealPlans",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MealPlans",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MealPlans",
                table: "MealPlans",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlanRecipes_MealPlans_MealPlanId",
                table: "MealPlanRecipes",
                column: "MealPlanId",
                principalTable: "MealPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlans_AspNetUsers_UserId1",
                table: "MealPlans",
                column: "UserId1",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MealPlanRecipes_MealPlans_MealPlanId",
                table: "MealPlanRecipes");

            migrationBuilder.DropForeignKey(
                name: "FK_MealPlans_AspNetUsers_UserId1",
                table: "MealPlans");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MealPlans",
                table: "MealPlans");

            migrationBuilder.RenameTable(
                name: "MealPlans",
                newName: "MealPlan");

            migrationBuilder.RenameIndex(
                name: "IX_MealPlans_UserId1",
                table: "MealPlan",
                newName: "IX_MealPlan_UserId1");

            migrationBuilder.AlterColumn<int>(
                name: "MealType",
                table: "MealPlanRecipes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "UserId1",
                table: "MealPlan",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "MealPlan",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MealPlan",
                table: "MealPlan",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlan_AspNetUsers_UserId1",
                table: "MealPlan",
                column: "UserId1",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlanRecipes_MealPlan_MealPlanId",
                table: "MealPlanRecipes",
                column: "MealPlanId",
                principalTable: "MealPlan",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
