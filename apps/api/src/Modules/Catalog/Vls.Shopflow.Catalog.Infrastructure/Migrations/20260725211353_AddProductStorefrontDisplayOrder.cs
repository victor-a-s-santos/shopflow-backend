using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductStorefrontDisplayOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill existing rows with a stable epoch; new products set CreatedAt in domain.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "catalog",
                table: "products",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                schema: "catalog",
                table: "products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                schema: "catalog",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_products_storefront_list",
                schema: "catalog",
                table: "products",
                columns: new[] { "IsActive", "IsFeatured", "DisplayOrder", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_storefront_list",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                schema: "catalog",
                table: "products");
        }
    }
}
