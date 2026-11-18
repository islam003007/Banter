using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Banter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBotUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBot",
                schema: "identity",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBot",
                schema: "identity",
                table: "Users");
        }
    }
}
