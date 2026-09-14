using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenDockify.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDisabled",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "IdentityAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SubjectId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecoveryTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ChallengeId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CodeHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoveryTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FamilyId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IssuedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RevokeReason = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TwoFactorChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChallengeId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwoFactorChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TwoFactorSecrets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Secret = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RecoveryCodesHash = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnabledAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwoFactorSecrets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityAuditEvents_OccurredAt",
                table: "IdentityAuditEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityAuditEvents_SubjectId_OccurredAt",
                table: "IdentityAuditEvents",
                columns: new[] { "SubjectId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempts_At",
                table: "LoginAttempts",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempts_IpAddress_At",
                table: "LoginAttempts",
                columns: new[] { "IpAddress", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempts_Username_At",
                table: "LoginAttempts",
                columns: new[] { "Username", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryTokens_ChallengeId",
                table: "RecoveryTokens",
                column: "ChallengeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryTokens_ExpiresAt",
                table: "RecoveryTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ExpiresAt",
                table: "RefreshTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyId",
                table: "RefreshTokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorChallenges_ChallengeId",
                table: "TwoFactorChallenges",
                column: "ChallengeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorChallenges_ExpiresAt",
                table: "TwoFactorChallenges",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorSecrets_UserId",
                table: "TwoFactorSecrets",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdentityAuditEvents");

            migrationBuilder.DropTable(
                name: "LoginAttempts");

            migrationBuilder.DropTable(
                name: "RecoveryTokens");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "TwoFactorChallenges");

            migrationBuilder.DropTable(
                name: "TwoFactorSecrets");

            migrationBuilder.DropColumn(
                name: "IsDisabled",
                table: "Users");
        }
    }
}
