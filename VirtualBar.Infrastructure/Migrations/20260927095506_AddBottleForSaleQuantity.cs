using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualBar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBottleForSaleQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ForSaleQuantity",
                table: "Bottles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Listings that predate the column offered the whole entry, which is what every row held
            // before a stack could be listed in part — so carry the owned count over verbatim.
            // Unlisted rows keep 0, the value the entity itself starts from.
            migrationBuilder.Sql(
                """
                UPDATE [Bottles]
                SET [ForSaleQuantity] = [Quantity]
                WHERE [IsForSale] = 1 AND [IsDeleted] = 0
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForSaleQuantity",
                table: "Bottles");
        }
    }
}
