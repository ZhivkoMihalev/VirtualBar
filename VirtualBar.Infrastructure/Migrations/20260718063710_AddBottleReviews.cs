using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualBar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBottleReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BottleReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BottleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Nose = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Palate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Finish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BottleReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BottleReviews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BottleReviews_Bottles_BottleId",
                        column: x => x.BottleId,
                        principalTable: "Bottles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BottleReviewFlavors",
                columns: table => new
                {
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Flavor = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BottleReviewFlavors", x => new { x.ReviewId, x.Flavor });
                    table.ForeignKey(
                        name: "FK_BottleReviewFlavors_BottleReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "BottleReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BottleReviews_BottleId_IsDeleted",
                table: "BottleReviews",
                columns: new[] { "BottleId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_BottleReviews_BottleId_UserId",
                table: "BottleReviews",
                columns: new[] { "BottleId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BottleReviews_UserId",
                table: "BottleReviews",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BottleReviewFlavors");

            migrationBuilder.DropTable(
                name: "BottleReviews");
        }
    }
}
