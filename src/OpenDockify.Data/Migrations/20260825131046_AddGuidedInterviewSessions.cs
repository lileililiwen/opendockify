using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenDockify.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidedInterviewSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    OwnerId = table.Column<Guid>(nullable: false),
                    TemplateId = table.Column<Guid>(nullable: false),
                    TemplateRevisionStamp = table.Column<DateTimeOffset>(nullable: false),
                    CurrentStepId = table.Column<string>(maxLength: 100, nullable: false),
                    AnswersJson = table.Column<string>(maxLength: 100000, nullable: false),
                    SelectedClauseIdsJson = table.Column<string>(maxLength: 10000, nullable: false),
                    Version = table.Column<int>(nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_OwnerId_ExpiresAt",
                table: "InterviewSessions",
                columns: new[] { "OwnerId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_TemplateId",
                table: "InterviewSessions",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewSessions");
        }
    }
}
