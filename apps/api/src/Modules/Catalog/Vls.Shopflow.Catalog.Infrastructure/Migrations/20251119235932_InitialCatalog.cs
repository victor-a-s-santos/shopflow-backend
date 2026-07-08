using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "attribute_definitions",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    AllowCustomValues = table.Column<bool>(type: "boolean", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attribute_definitions_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    HasSkus = table.Column<bool>(type: "boolean", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    base_regular_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    base_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    base_promo_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    base_promo_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    base_promo_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    base_promo_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                    table.CheckConstraint("CK_products_base_price_nonnegative", "(base_regular_price >= 0)\n            AND (base_promo_price IS NULL OR base_promo_price >= 0)");
                    table.CheckConstraint("CK_products_base_promo_le_regular", "(base_promo_price IS NULL OR base_promo_price <= base_regular_price)");
                    table.CheckConstraint("CK_products_base_promo_window", "(base_promo_start IS NULL OR base_promo_end IS NULL OR base_promo_start <= base_promo_end)");
                    table.ForeignKey(
                        name: "FK_products_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "attribute_value_definitions",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    HexColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_value_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attribute_value_definitions_attribute_definitions_Attribute~",
                        column: x => x.AttributeDefinitionId,
                        principalSchema: "catalog",
                        principalTable: "attribute_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_skus",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regular_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    promo_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    promo_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    promo_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    promo_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_skus", x => x.Id);
                    table.CheckConstraint("CK_product_skus_price_nonnegative", "(regular_price >= 0)\n            AND (promo_price IS NULL OR promo_price >= 0)");
                    table.CheckConstraint("CK_product_skus_promo_le_regular", "(promo_price IS NULL OR promo_price <= regular_price)");
                    table.CheckConstraint("CK_product_skus_promo_window", "(promo_start IS NULL OR promo_end IS NULL OR promo_start <= promo_end)");
                    table.ForeignKey(
                        name: "FK_product_skus_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sku_attributes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SkuId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttributeValueDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CustomValue = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sku_attributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sku_attributes_attribute_definitions_AttributeDefinitionId",
                        column: x => x.AttributeDefinitionId,
                        principalSchema: "catalog",
                        principalTable: "attribute_definitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_sku_attributes_attribute_value_definitions_AttributeValueDe~",
                        column: x => x.AttributeValueDefinitionId,
                        principalSchema: "catalog",
                        principalTable: "attribute_value_definitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_sku_attributes_product_skus_SkuId",
                        column: x => x.SkuId,
                        principalSchema: "catalog",
                        principalTable: "product_skus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attribute_definitions_CategoryId",
                schema: "catalog",
                table: "attribute_definitions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_attribute_value_definitions_AttributeDefinitionId",
                schema: "catalog",
                table: "attribute_value_definitions",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_product_skus_ProductId_Code",
                schema: "catalog",
                table: "product_skus",
                columns: new[] { "ProductId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_CategoryId",
                schema: "catalog",
                table: "products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_products_slug",
                schema: "catalog",
                table: "products",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sku_attributes_AttributeDefinitionId",
                schema: "catalog",
                table: "sku_attributes",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_sku_attributes_AttributeValueDefinitionId",
                schema: "catalog",
                table: "sku_attributes",
                column: "AttributeValueDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_sku_attributes_SkuId",
                schema: "catalog",
                table: "sku_attributes",
                column: "SkuId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sku_attributes",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "attribute_value_definitions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product_skus",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "attribute_definitions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "products",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "catalog");
        }
    }
}
