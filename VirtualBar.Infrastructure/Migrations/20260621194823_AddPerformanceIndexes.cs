using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualBar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bottles_UserId",
                table: "Bottles");

            migrationBuilder.CreateIndex(
                name: "IX_Bottles_UserId_IsDeleted",
                table: "Bottles",
                columns: new[] { "UserId", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bottles_UserId_IsDeleted",
                table: "Bottles");

            migrationBuilder.CreateIndex(
                name: "IX_Bottles_UserId",
                table: "Bottles",
                column: "UserId");
        }
    }
}
