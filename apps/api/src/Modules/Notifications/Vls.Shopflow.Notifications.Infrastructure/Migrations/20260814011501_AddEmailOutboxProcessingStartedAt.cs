using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailOutboxProcessingStartedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessingStartedAt",
                schema: "notifications",
                table: "email_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_Status_ProcessingStartedAt",
                schema: "notifications",
                table: "email_outbox",
                columns: new[] { "Status", "ProcessingStartedAt" });

            // Pre-EMAIL-001 Processing rows have NULL lease; without this they would never reclaim.
            migrationBuilder.Sql("""
                UPDATE notifications.email_outbox
                SET "ProcessingStartedAt" = COALESCE("CreatedAt", NOW())
                WHERE "Status" = 'Processing'
                  AND "ProcessingStartedAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_outbox_Status_ProcessingStartedAt",
                schema: "notifications",
                table: "email_outbox");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAt",
                schema: "notifications",
                table: "email_outbox");
        }
    }
}
