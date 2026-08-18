using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.PaymentsPix.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPixPaymentProviderStatusAndWebhookEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProviderApprovedAt",
                schema: "payments_pix",
                table: "pix_payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderStatus",
                schema: "payments_pix",
                table: "pix_payments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderStatusDetail",
                schema: "payments_pix",
                table: "pix_payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mercado_pago_webhook_events",
                schema: "payments_pix",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderPaymentId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LiveMode = table.Column<bool>(type: "boolean", nullable: false),
                    SignatureValid = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessingStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mercado_pago_webhook_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mercado_pago_webhook_events_ProviderEventId",
                schema: "payments_pix",
                table: "mercado_pago_webhook_events",
                column: "ProviderEventId",
                unique: true,
                filter: "\"ProviderEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_mercado_pago_webhook_events_ProviderPaymentId",
                schema: "payments_pix",
                table: "mercado_pago_webhook_events",
                column: "ProviderPaymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mercado_pago_webhook_events",
                schema: "payments_pix");

            migrationBuilder.DropColumn(
                name: "ProviderApprovedAt",
                schema: "payments_pix",
                table: "pix_payments");

            migrationBuilder.DropColumn(
                name: "ProviderStatus",
                schema: "payments_pix",
                table: "pix_payments");

            migrationBuilder.DropColumn(
                name: "ProviderStatusDetail",
                schema: "payments_pix",
                table: "pix_payments");
        }
    }
}
