using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Granary.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPantryItemResolvedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "PantryItems",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "PantryItems");
        }
    }
}
