using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InventoryIntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_items_on_hand_nonneg",
                schema: "inventory",
                table: "inventory_items",
                sql: "\"QuantityOnHand\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_items_reserved_lte_on_hand",
                schema: "inventory",
                table: "inventory_items",
                sql: "\"QuantityReserved\" <= \"QuantityOnHand\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_items_reserved_nonneg",
                schema: "inventory",
                table: "inventory_items",
                sql: "\"QuantityReserved\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_items_on_hand_nonneg",
                schema: "inventory",
                table: "inventory_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_items_reserved_lte_on_hand",
                schema: "inventory",
                table: "inventory_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_items_reserved_nonneg",
                schema: "inventory",
                table: "inventory_items");
        }
    }
}
