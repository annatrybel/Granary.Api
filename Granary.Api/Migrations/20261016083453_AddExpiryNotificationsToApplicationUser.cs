using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Granary.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExpiryNotificationsToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExpiryNotifications",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiryNotifications",
                table: "AspNetUsers");
        }
    }
}
