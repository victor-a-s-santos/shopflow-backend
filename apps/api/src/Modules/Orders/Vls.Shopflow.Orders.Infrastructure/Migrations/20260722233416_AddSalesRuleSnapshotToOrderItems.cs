using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesRuleSnapshotToOrderItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EquivalentUnitPrice",
                schema: "orders",
                table: "order_items",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageDescription",
                schema: "orders",
                table: "order_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageLabel",
                schema: "orders",
                table: "order_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PackageSize",
                schema: "orders",
                table: "order_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuantityUnitLabel",
                schema: "orders",
                table: "order_items",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesDisplaySummary",
                schema: "orders",
                table: "order_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesMode",
                schema: "orders",
                table: "order_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowTotalPieces",
                schema: "orders",
                table: "order_items",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalPieces",
                schema: "orders",
                table: "order_items",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquivalentUnitPrice",
                schema: "orders",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "PackageDescription",
                schema: "orders",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "PackageLabel",
                schema: "orders",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "PackageSize",
                schema: "orders",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "QuantityUnitLabel",
                schema: "orders",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "SalesDisplaySummary",
                schema: "orders",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "SalesMode",
                schema: "orders",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "ShowTotalPieces",
                schema: "orders",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "TotalPieces",
                schema: "orders",
                table: "order_items");
        }
    }
}
