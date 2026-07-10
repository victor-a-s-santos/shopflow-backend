using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.PaymentsPix.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPixPaymentOrdersApiFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProviderPaymentId",
                schema: "payments_pix",
                table: "mercado_pago_webhook_events",
                newName: "ProviderOrderId");

            migrationBuilder.RenameIndex(
                name: "IX_mercado_pago_webhook_events_ProviderPaymentId",
                schema: "payments_pix",
                table: "mercado_pago_webhook_events",
                newName: "IX_mercado_pago_webhook_events_ProviderOrderId");

            migrationBuilder.AlterColumn<string>(
                name: "QrCode",
                schema: "payments_pix",
                table: "pix_payments",
                type: "character varying(20000)",
                maxLength: 20000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                schema: "payments_pix",
                table: "pix_payments",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "payments_pix",
                table: "pix_payments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderOrderId",
                schema: "payments_pix",
                table: "pix_payments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderTransactionId",
                schema: "payments_pix",
                table: "pix_payments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderTransactionStatus",
                schema: "payments_pix",
                table: "pix_payments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderTransactionStatusDetail",
                schema: "payments_pix",
                table: "pix_payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProviderUpdatedAt",
                schema: "payments_pix",
                table: "pix_payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TicketUrl",
                schema: "payments_pix",
                table: "pix_payments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pix_payments_ProviderOrderId",
                schema: "payments_pix",
                table: "pix_payments",
                column: "ProviderOrderId",
                filter: "\"ProviderOrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_pix_payments_ProviderTransactionId",
                schema: "payments_pix",
                table: "pix_payments",
                column: "ProviderTransactionId",
                filter: "\"ProviderTransactionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pix_payments_ProviderOrderId",
                schema: "payments_pix",
                table: "pix_payments");

            migrationBuilder.DropIndex(
                name: "IX_pix_payments_ProviderTransactionId",
                schema: "payments_pix",
                table: "pix_payments");

            migrationBuilder.DropColumn(
                name: "ExternalReference",
                schema: "payments_pix",
                table: "pix_payments");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "payments_pix",
                table: "pix_payments");

            migrationBuilder.DropColumn(
                name: "ProviderOrderId",
                schema: "payments_pix",
                table: "pix_payments");

            migrationBuilder.DropColumn(
                name: "ProviderTransactionId",
                schema: "payments_pix",
                table: "pix_payments");

            migrationBuilder.DropColumn(
                name: "ProviderTransactionStatus",
                schema: "payments_pix",
                table: "pix_payments");

            migrationBuilder.DropColumn(
                name: "ProviderTransactionStatusDetail",
                schema: "payments_pix",
                table: "pix_payments");

            migrationBuilder.DropColumn(
                name: "ProviderUpdatedAt",
                schema: "payments_pix",
                table: "pix_payments");

            migrationBuilder.DropColumn(
                name: "TicketUrl",
                schema: "payments_pix",
                table: "pix_payments");

            migrationBuilder.RenameColumn(
                name: "ProviderOrderId",
                schema: "payments_pix",
                table: "mercado_pago_webhook_events",
                newName: "ProviderPaymentId");

            migrationBuilder.RenameIndex(
                name: "IX_mercado_pago_webhook_events_ProviderOrderId",
                schema: "payments_pix",
                table: "mercado_pago_webhook_events",
                newName: "IX_mercado_pago_webhook_events_ProviderPaymentId");

            migrationBuilder.AlterColumn<string>(
                name: "QrCode",
                schema: "payments_pix",
                table: "pix_payments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20000)",
                oldMaxLength: 20000,
                oldNullable: true);
        }
    }
}
