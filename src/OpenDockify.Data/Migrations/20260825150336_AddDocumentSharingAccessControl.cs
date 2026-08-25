using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenDockify.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentSharingAccessControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GranteeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccessLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalShareLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SecretHash = table.Column<byte[]>(type: "BLOB", maxLength: 32, nullable: false),
                    AllowDownload = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalShareLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShareAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ActorCategory = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ActorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CoarseClient = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAtUtcTicks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentGrants_DocumentId_GranteeId_RevokedAt",
                table: "DocumentGrants",
                columns: new[] { "DocumentId", "GranteeId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentGrants_OwnerId",
                table: "DocumentGrants",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalShareLinks_DocumentId_RevokedAt",
                table: "ExternalShareLinks",
                columns: new[] { "DocumentId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalShareLinks_ExpiresAt",
                table: "ExternalShareLinks",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalShareLinks_OwnerId",
                table: "ExternalShareLinks",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareAuditEvents_CreatedAtUtcTicks",
                table: "ShareAuditEvents",
                column: "CreatedAtUtcTicks");

            migrationBuilder.CreateIndex(
                name: "IX_ShareAuditEvents_DocumentId_CreatedAtUtcTicks",
                table: "ShareAuditEvents",
                columns: new[] { "DocumentId", "CreatedAtUtcTicks" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentGrants");

            migrationBuilder.DropTable(
                name: "ExternalShareLinks");

            migrationBuilder.DropTable(
                name: "ShareAuditEvents");
        }
    }
}
