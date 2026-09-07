using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Vls.Shopflow.Orders.Infrastructure;

#nullable disable

namespace Vls.Shopflow.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(OrdersDbContext))]
    [Migration("20260907021000_AddOrderStockConfirmation")]
    public partial class AddOrderStockConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StockConfirmedAt",
                schema: "orders",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StockConfirmedByAdminUserId",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StockConfirmationUpdatedAt",
                schema: "orders",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockConfirmationNote",
                schema: "orders",
                table: "orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            // Backfill already-separated/delivered orders so the new milestone stays consistent.
            // Paid + AwaitingShipment remain null and require admin confirmation.
            migrationBuilder.Sql(
                """
                UPDATE orders.orders
                SET "StockConfirmedAt" = COALESCE("ShippedAt", "FulfillmentUpdatedAt"),
                    "StockConfirmationUpdatedAt" = COALESCE("ShippedAt", "FulfillmentUpdatedAt")
                WHERE "FulfillmentStatus" IN ('Shipped', 'Delivered')
                  AND "StockConfirmedAt" IS NULL
                  AND COALESCE("ShippedAt", "FulfillmentUpdatedAt") IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StockConfirmationNote",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "StockConfirmationUpdatedAt",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "StockConfirmedByAdminUserId",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "StockConfirmedAt",
                schema: "orders",
                table: "orders");
        }
    }
}
