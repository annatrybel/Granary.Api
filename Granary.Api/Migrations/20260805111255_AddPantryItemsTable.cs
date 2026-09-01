using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Granary.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPantryItemsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PantryItem_AspNetUsers_UserId1",
                table: "PantryItem");

            migrationBuilder.DropForeignKey(
                name: "FK_PantryItem_Product_ProductId",
                table: "PantryItem");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PantryItem",
                table: "PantryItem");

            migrationBuilder.RenameTable(
                name: "PantryItem",
                newName: "PantryItems");

            migrationBuilder.RenameIndex(
                name: "IX_PantryItem_UserId1",
                table: "PantryItems",
                newName: "IX_PantryItems_UserId1");

            migrationBuilder.RenameIndex(
                name: "IX_PantryItem_ProductId",
                table: "PantryItems",
                newName: "IX_PantryItems_ProductId");

            migrationBuilder.AlterColumn<string>(
                name: "UserId1",
                table: "PantryItems",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PantryItems",
                table: "PantryItems",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PantryItems_AspNetUsers_UserId1",
                table: "PantryItems",
                column: "UserId1",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PantryItems_Product_ProductId",
                table: "PantryItems",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PantryItems_AspNetUsers_UserId1",
                table: "PantryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PantryItems_Product_ProductId",
                table: "PantryItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PantryItems",
                table: "PantryItems");

            migrationBuilder.RenameTable(
                name: "PantryItems",
                newName: "PantryItem");

            migrationBuilder.RenameIndex(
                name: "IX_PantryItems_UserId1",
                table: "PantryItem",
                newName: "IX_PantryItem_UserId1");

            migrationBuilder.RenameIndex(
                name: "IX_PantryItems_ProductId",
                table: "PantryItem",
                newName: "IX_PantryItem_ProductId");

            migrationBuilder.AlterColumn<string>(
                name: "UserId1",
                table: "PantryItem",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PantryItem",
                table: "PantryItem",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PantryItem_AspNetUsers_UserId1",
                table: "PantryItem",
                column: "UserId1",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PantryItem_Product_ProductId",
                table: "PantryItem",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
