using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualBar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductRequestYears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReleaseYear",
                table: "ProductRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VintageYear",
                table: "ProductRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductRequests_OneYearKind",
                table: "ProductRequests",
                sql: "[VintageYear] IS NULL OR [ReleaseYear] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductRequests_OneYearKind",
                table: "ProductRequests");

            migrationBuilder.DropColumn(
                name: "ReleaseYear",
                table: "ProductRequests");

            migrationBuilder.DropColumn(
                name: "VintageYear",
                table: "ProductRequests");
        }
    }
}
