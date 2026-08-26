using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenDockify.Data.Migrations
{
    /// <summary>
    /// Adds the bounded JSON attempt log to webhook deliveries (diagnostic
    /// timeline; never contains bodies or secrets). Hand-written because the
    /// model snapshot already matched the full model when this change landed.
    /// </summary>
    [DbContext(typeof(OpenDockify.Data.AppDbContext))]
    [Migration("20260826030000_AddWebhookDeliveryAttemptLog")]
    public partial class AddWebhookDeliveryAttemptLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttemptLog",
                table: "WebhookDeliveries",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptLog",
                table: "WebhookDeliveries");
        }
    }
}
