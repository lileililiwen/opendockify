using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenDockify.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupRestoreIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentSha256",
                table: "Documents",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BackupOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    BundleDigest = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupOperations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupOperations_StartedAtUtc",
                table: "BackupOperations",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupOperations");

            migrationBuilder.DropColumn(
                name: "ContentSha256",
                table: "Documents");
        }
    }
}
