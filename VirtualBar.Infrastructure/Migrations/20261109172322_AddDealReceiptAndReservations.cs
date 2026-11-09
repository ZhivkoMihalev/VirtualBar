using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualBar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDealReceiptAndReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Offers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Offers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AcquiredFromOfferId",
                table: "Bottles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReservedQuantity",
                table: "Bottles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Bottles_AcquiredFromOfferId",
                table: "Bottles",
                column: "AcquiredFromOfferId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bottles_Offers_AcquiredFromOfferId",
                table: "Bottles",
                column: "AcquiredFromOfferId",
                principalTable: "Offers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bottles_Offers_AcquiredFromOfferId",
                table: "Bottles");

            migrationBuilder.DropIndex(
                name: "IX_Bottles_AcquiredFromOfferId",
                table: "Bottles");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "AcquiredFromOfferId",
                table: "Bottles");

            migrationBuilder.DropColumn(
                name: "ReservedQuantity",
                table: "Bottles");
        }
    }
}
