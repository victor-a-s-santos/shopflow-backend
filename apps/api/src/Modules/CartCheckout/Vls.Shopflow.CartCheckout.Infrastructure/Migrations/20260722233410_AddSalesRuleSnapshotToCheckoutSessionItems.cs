using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.CartCheckout.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesRuleSnapshotToCheckoutSessionItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EquivalentUnitPrice",
                schema: "cartcheckout",
                table: "checkout_session_items",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageDescription",
                schema: "cartcheckout",
                table: "checkout_session_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageLabel",
                schema: "cartcheckout",
                table: "checkout_session_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PackageSize",
                schema: "cartcheckout",
                table: "checkout_session_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuantityUnitLabel",
                schema: "cartcheckout",
                table: "checkout_session_items",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesDisplaySummary",
                schema: "cartcheckout",
                table: "checkout_session_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesMode",
                schema: "cartcheckout",
                table: "checkout_session_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowTotalPieces",
                schema: "cartcheckout",
                table: "checkout_session_items",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalPieces",
                schema: "cartcheckout",
                table: "checkout_session_items",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquivalentUnitPrice",
                schema: "cartcheckout",
                table: "checkout_session_items");

            migrationBuilder.DropColumn(
                name: "PackageDescription",
                schema: "cartcheckout",
                table: "checkout_session_items");

            migrationBuilder.DropColumn(
                name: "PackageLabel",
                schema: "cartcheckout",
                table: "checkout_session_items");

            migrationBuilder.DropColumn(
                name: "PackageSize",
                schema: "cartcheckout",
                table: "checkout_session_items");

            migrationBuilder.DropColumn(
                name: "QuantityUnitLabel",
                schema: "cartcheckout",
                table: "checkout_session_items");

            migrationBuilder.DropColumn(
                name: "SalesDisplaySummary",
                schema: "cartcheckout",
                table: "checkout_session_items");

            migrationBuilder.DropColumn(
                name: "SalesMode",
                schema: "cartcheckout",
                table: "checkout_session_items");

            migrationBuilder.DropColumn(
                name: "ShowTotalPieces",
                schema: "cartcheckout",
                table: "checkout_session_items");

            migrationBuilder.DropColumn(
                name: "TotalPieces",
                schema: "cartcheckout",
                table: "checkout_session_items");
        }
    }
}
