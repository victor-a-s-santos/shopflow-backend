using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSkuSalesRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allow_customer_to_choose_variants",
                schema: "catalog",
                table: "product_skus",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_wholesale_only",
                schema: "catalog",
                table: "product_skus",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "minimum_quantity",
                schema: "catalog",
                table: "product_skus",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "package_description",
                schema: "catalog",
                table: "product_skus",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "package_label",
                schema: "catalog",
                table: "product_skus",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "package_size",
                schema: "catalog",
                table: "product_skus",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quantity_step",
                schema: "catalog",
                table: "product_skus",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "quantity_unit_label",
                schema: "catalog",
                table: "product_skus",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sales_mode",
                schema: "catalog",
                table: "product_skus",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "show_total_pieces",
                schema: "catalog",
                table: "product_skus",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_product_skus_minimum_quantity",
                schema: "catalog",
                table: "product_skus",
                sql: "minimum_quantity >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_product_skus_package_size",
                schema: "catalog",
                table: "product_skus",
                sql: "package_size IS NULL OR package_size > 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_product_skus_quantity_step",
                schema: "catalog",
                table: "product_skus",
                sql: "quantity_step >= 1");

            // Explicit Unit backfill for any rows that somehow miss defaults.
            migrationBuilder.Sql("""
                UPDATE catalog.product_skus
                SET
                    sales_mode = COALESCE(sales_mode, 0),
                    minimum_quantity = COALESCE(minimum_quantity, 1),
                    quantity_step = COALESCE(quantity_step, 1),
                    allow_customer_to_choose_variants = COALESCE(allow_customer_to_choose_variants, TRUE),
                    show_total_pieces = COALESCE(show_total_pieces, FALSE),
                    is_wholesale_only = COALESCE(is_wholesale_only, FALSE)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_product_skus_minimum_quantity",
                schema: "catalog",
                table: "product_skus");

            migrationBuilder.DropCheckConstraint(
                name: "CK_product_skus_package_size",
                schema: "catalog",
                table: "product_skus");

            migrationBuilder.DropCheckConstraint(
                name: "CK_product_skus_quantity_step",
                schema: "catalog",
                table: "product_skus");

            migrationBuilder.DropColumn(
                name: "allow_customer_to_choose_variants",
                schema: "catalog",
                table: "product_skus");

            migrationBuilder.DropColumn(
                name: "is_wholesale_only",
                schema: "catalog",
                table: "product_skus");

            migrationBuilder.DropColumn(
                name: "minimum_quantity",
                schema: "catalog",
                table: "product_skus");

            migrationBuilder.DropColumn(
                name: "package_description",
                schema: "catalog",
                table: "product_skus");

            migrationBuilder.DropColumn(
                name: "package_label",
                schema: "catalog",
                table: "product_skus");

            migrationBuilder.DropColumn(
                name: "package_size",
                schema: "catalog",
                table: "product_skus");

            migrationBuilder.DropColumn(
                name: "quantity_step",
                schema: "catalog",
                table: "product_skus");

            migrationBuilder.DropColumn(
                name: "quantity_unit_label",
                schema: "catalog",
                table: "product_skus");

            migrationBuilder.DropColumn(
                name: "sales_mode",
                schema: "catalog",
                table: "product_skus");

            migrationBuilder.DropColumn(
                name: "show_total_pieces",
                schema: "catalog",
                table: "product_skus");
        }
    }
}
