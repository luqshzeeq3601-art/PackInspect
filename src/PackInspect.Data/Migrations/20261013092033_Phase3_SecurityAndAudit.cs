using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackInspect.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase3_SecurityAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "Recipes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OperatorUserId",
                table: "Lots",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AlarmHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    RaisedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClearedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlarmHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<byte[]>(type: "BLOB", nullable: false),
                    PasswordSalt = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Iterations = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailedLogins = table.Column<int>(type: "INTEGER", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Entity = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Field = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_UpdatedByUserId",
                table: "Recipes",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Lots_OperatorUserId",
                table: "Lots",
                column: "OperatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_UserId",
                table: "AuditEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Lots_Users_OperatorUserId",
                table: "Lots",
                column: "OperatorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Users_UpdatedByUserId",
                table: "Recipes",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lots_Users_OperatorUserId",
                table: "Lots");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Users_UpdatedByUserId",
                table: "Recipes");

            migrationBuilder.DropTable(
                name: "AlarmHistory");

            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_UpdatedByUserId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Lots_OperatorUserId",
                table: "Lots");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "OperatorUserId",
                table: "Lots");
        }
    }
}
