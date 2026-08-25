using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenDockify.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplatePortabilityVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentRevision",
                table: "Templates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceInstance",
                table: "Templates",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceStableId",
                table: "Templates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StableId",
                table: "Templates",
                type: "TEXT",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateRevisionId",
                table: "InterviewSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateRevisionId",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TemplateRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RiskNoticeText = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: false),
                    SourceInstance = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SourceStableId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateRevisions", x => x.Id);
                });

            migrationBuilder.Sql("UPDATE \"Templates\" SET \"StableId\" = \"Id\", \"CurrentRevision\" = 1;");
            migrationBuilder.Sql("""
                INSERT INTO "TemplateRevisions" ("Id", "TemplateId", "Revision", "Name", "Category", "Description", "RiskNoticeText", "Body", "DefinitionJson", "CreatedAt")
                SELECT "Id", "Id", 1, "Name", "Category", "Description", "RiskNoticeText", "Body", "DefinitionJson", "UpdatedAt" FROM "Templates";
                """);
            migrationBuilder.Sql("UPDATE \"Documents\" SET \"TemplateRevisionId\" = \"TemplateId\";");
            migrationBuilder.Sql("UPDATE \"InterviewSessions\" SET \"TemplateRevisionId\" = \"TemplateId\";");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_StableId",
                table: "Templates",
                column: "StableId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_TemplateRevisionId",
                table: "InterviewSessions",
                column: "TemplateRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TemplateRevisionId",
                table: "Documents",
                column: "TemplateRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateRevisions_TemplateId_Revision",
                table: "TemplateRevisions",
                columns: new[] { "TemplateId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemplateRevisions");

            migrationBuilder.DropIndex(
                name: "IX_Templates_StableId",
                table: "Templates");

            migrationBuilder.DropIndex(
                name: "IX_InterviewSessions_TemplateRevisionId",
                table: "InterviewSessions");

            migrationBuilder.DropIndex(
                name: "IX_Documents_TemplateRevisionId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CurrentRevision",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "SourceInstance",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "SourceStableId",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "StableId",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "TemplateRevisionId",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "TemplateRevisionId",
                table: "Documents");
        }
    }
}
