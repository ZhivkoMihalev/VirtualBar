using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualBar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNewsPostFlatTextColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Content",
                table: "NewsPosts");

            migrationBuilder.DropColumn(
                name: "Excerpt",
                table: "NewsPosts");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "NewsPosts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "NewsPosts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Excerpt",
                table: "NewsPosts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "NewsPosts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
