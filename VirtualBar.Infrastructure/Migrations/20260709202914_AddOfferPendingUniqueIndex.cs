using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualBar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferPendingUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Offers_BottleId",
                table: "Offers");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_BottleId_BuyerId",
                table: "Offers",
                columns: new[] { "BottleId", "BuyerId" },
                unique: true,
                filter: "[Status] = 0 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Offers_BottleId_BuyerId",
                table: "Offers");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_BottleId",
                table: "Offers",
                column: "BottleId");
        }
    }
}
