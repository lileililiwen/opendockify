using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenDockify.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentLibraryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Documents",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OwnerId_IsArchived_CreatedAt",
                table: "Documents",
                columns: new[] { "OwnerId", "IsArchived", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OwnerId_Title",
                table: "Documents",
                columns: new[] { "OwnerId", "Title" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_OwnerId_IsArchived_CreatedAt",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_OwnerId_Title",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Documents");
        }
    }
}
