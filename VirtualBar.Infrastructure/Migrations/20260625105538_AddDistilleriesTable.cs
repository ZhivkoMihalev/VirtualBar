using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualBar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDistilleriesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Distillery",
                table: "WishListItems");

            migrationBuilder.DropColumn(
                name: "Distillery",
                table: "Bottles");

            migrationBuilder.AddColumn<Guid>(
                name: "DistilleryId",
                table: "WishListItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DistilleryId",
                table: "Bottles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Distilleries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Region = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Distilleries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WishListItems_DistilleryId",
                table: "WishListItems",
                column: "DistilleryId");

            migrationBuilder.CreateIndex(
                name: "IX_Bottles_DistilleryId",
                table: "Bottles",
                column: "DistilleryId");

            migrationBuilder.CreateIndex(
                name: "IX_Distilleries_Name",
                table: "Distilleries",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bottles_Distilleries_DistilleryId",
                table: "Bottles",
                column: "DistilleryId",
                principalTable: "Distilleries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WishListItems_Distilleries_DistilleryId",
                table: "WishListItems",
                column: "DistilleryId",
                principalTable: "Distilleries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bottles_Distilleries_DistilleryId",
                table: "Bottles");

            migrationBuilder.DropForeignKey(
                name: "FK_WishListItems_Distilleries_DistilleryId",
                table: "WishListItems");

            migrationBuilder.DropTable(
                name: "Distilleries");

            migrationBuilder.DropIndex(
                name: "IX_WishListItems_DistilleryId",
                table: "WishListItems");

            migrationBuilder.DropIndex(
                name: "IX_Bottles_DistilleryId",
                table: "Bottles");

            migrationBuilder.DropColumn(
                name: "DistilleryId",
                table: "WishListItems");

            migrationBuilder.DropColumn(
                name: "DistilleryId",
                table: "Bottles");

            migrationBuilder.AddColumn<string>(
                name: "Distillery",
                table: "WishListItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Distillery",
                table: "Bottles",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
