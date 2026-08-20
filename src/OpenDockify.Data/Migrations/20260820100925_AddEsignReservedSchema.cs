using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenDockify.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEsignReservedSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SigningStatus",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Signers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SigningOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Signers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SigningAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SigningAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Signers_DocumentId",
                table: "Signers",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SigningAuditLogs_DocumentId",
                table: "SigningAuditLogs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SigningAuditLogs_Timestamp",
                table: "SigningAuditLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Signers");

            migrationBuilder.DropTable(
                name: "SigningAuditLogs");

            migrationBuilder.DropColumn(
                name: "SigningStatus",
                table: "Documents");
        }
    }
}
