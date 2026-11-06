using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualBar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductYears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReleaseYear",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VintageYear",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_OneYearKind",
                table: "Products",
                sql: "[VintageYear] IS NULL OR [ReleaseYear] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_OneYearKind",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ReleaseYear",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "VintageYear",
                table: "Products");
        }
    }
}
