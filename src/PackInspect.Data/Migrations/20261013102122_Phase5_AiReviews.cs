using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackInspect.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase5_AiReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UnitResultId = table.Column<int>(type: "INTEGER", nullable: false),
                    AiLabel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AiConfidence = table.Column<double>(type: "REAL", nullable: false),
                    ModelVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OperatorVerdict = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    OperatorLabel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiReviews_UnitResults_UnitResultId",
                        column: x => x.UnitResultId,
                        principalTable: "UnitResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiReviews_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiReviews_ReviewedByUserId",
                table: "AiReviews",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiReviews_UnitResultId",
                table: "AiReviews",
                column: "UnitResultId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiReviews");
        }
    }
}
