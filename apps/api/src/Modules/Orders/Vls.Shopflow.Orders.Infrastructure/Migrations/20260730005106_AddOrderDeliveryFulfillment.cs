using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderDeliveryFulfillment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerOrderNote",
                schema: "orders",
                table: "orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredAt",
                schema: "orders",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalDeliveryMethod",
                schema: "orders",
                table: "orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentStatus",
                schema: "orders",
                table: "orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "AwaitingShipment");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FulfillmentUpdatedAt",
                schema: "orders",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FulfillmentUpdatedByAdminId",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalOrderNote",
                schema: "orders",
                table: "orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PreferredDeliveryDate",
                schema: "orders",
                table: "orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredDeliveryMethod",
                schema: "orders",
                table: "orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ShippedAt",
                schema: "orders",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingCode",
                schema: "orders",
                table: "orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_CustomerUserId_FulfillmentStatus_CreatedAt",
                schema: "orders",
                table: "orders",
                columns: new[] { "CustomerUserId", "FulfillmentStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_FulfillmentStatus_CreatedAt",
                schema: "orders",
                table: "orders",
                columns: new[] { "FulfillmentStatus", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_CustomerUserId_FulfillmentStatus_CreatedAt",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_FulfillmentStatus_CreatedAt",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "CustomerOrderNote",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FinalDeliveryMethod",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentStatus",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentUpdatedAt",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentUpdatedByAdminId",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "InternalOrderNote",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PreferredDeliveryDate",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PreferredDeliveryMethod",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShippedAt",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "TrackingCode",
                schema: "orders",
                table: "orders");
        }
    }
}
