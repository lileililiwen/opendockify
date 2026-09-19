using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenDockify.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(OpenDockify.Data.AppDbContext))]
    [Migration("20260919090000_AddShareLinkPasswords")]
    public partial class AddShareLinkPasswords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "PasswordHash",
                table: "ExternalShareLinks",
                type: "BLOB",
                maxLength: 32,
                nullable: true);
            migrationBuilder.AddColumn<byte[]>(
                name: "PasswordSalt",
                table: "ExternalShareLinks",
                type: "BLOB",
                maxLength: 16,
                nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "PasswordFailedAttempts",
                table: "ExternalShareLinks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PasswordLockedUntil",
                table: "ExternalShareLinks",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PasswordHash", table: "ExternalShareLinks");
            migrationBuilder.DropColumn(name: "PasswordSalt", table: "ExternalShareLinks");
            migrationBuilder.DropColumn(name: "PasswordFailedAttempts", table: "ExternalShareLinks");
            migrationBuilder.DropColumn(name: "PasswordLockedUntil", table: "ExternalShareLinks");
        }
    }
}
