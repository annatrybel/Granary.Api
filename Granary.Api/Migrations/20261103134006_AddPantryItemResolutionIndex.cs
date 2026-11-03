using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Granary.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPantryItemResolutionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PantryItems_UserId_ResolvedAt",
                table: "PantryItems",
                columns: new[] { "UserId", "ResolvedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PantryItems_UserId_ResolvedAt",
                table: "PantryItems");
        }
    }
}
