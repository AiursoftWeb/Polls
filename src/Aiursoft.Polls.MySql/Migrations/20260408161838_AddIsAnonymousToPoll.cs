using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.Polls.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddIsAnonymousToPoll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymous",
                table: "Polls",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAnonymous",
                table: "Polls");
        }
    }
}
