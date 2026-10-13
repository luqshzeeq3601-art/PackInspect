using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackInspect.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CameraPositionMm = table.Column<double>(type: "REAL", nullable: false),
                    TestPositionMm = table.Column<double>(type: "REAL", nullable: false),
                    MinBodyRectangularity = table.Column<double>(type: "REAL", nullable: false),
                    ExpectedLeadCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MinMarkingPixels = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxMarkingPixels = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxDefectAreaPx = table.Column<int>(type: "INTEGER", nullable: false),
                    MeasurementKind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    LowLimit = table.Column<double>(type: "REAL", nullable: false),
                    HighLimit = table.Column<double>(type: "REAL", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LotNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RecipeId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipeVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lots_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LotId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    VisionVerdict = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    DefectType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    MeasuredValue = table.Column<double>(type: "REAL", nullable: false),
                    ElectricalVerdict = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    FinalVerdict = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    ImagePath = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    CycleTimeMs = table.Column<int>(type: "INTEGER", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitResults_Lots_LotId",
                        column: x => x.LotId,
                        principalTable: "Lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lots_LotNumber",
                table: "Lots",
                column: "LotNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lots_RecipeId",
                table: "Lots",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_Name",
                table: "Recipes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitResults_LotId_Sequence",
                table: "UnitResults",
                columns: new[] { "LotId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnitResults");

            migrationBuilder.DropTable(
                name: "Lots");

            migrationBuilder.DropTable(
                name: "Recipes");
        }
    }
}
