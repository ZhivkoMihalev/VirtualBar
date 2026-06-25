using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualBar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDistilleryCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DistilleryCategories",
                columns: table => new
                {
                    DistilleryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistilleryCategories", x => new { x.DistilleryId, x.Category });
                    table.ForeignKey(
                        name: "FK_DistilleryCategories_Distilleries_DistilleryId",
                        column: x => x.DistilleryId,
                        principalTable: "Distilleries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DistilleryCategories");
        }
    }
}
