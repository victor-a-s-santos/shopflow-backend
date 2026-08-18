using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImageObjectKeyAndMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StoragePath",
                schema: "catalog",
                table: "product_images",
                newName: "ObjectKey");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                schema: "catalog",
                table: "product_images",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                schema: "catalog",
                table: "product_images",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_images_ObjectKey",
                schema: "catalog",
                table: "product_images",
                column: "ObjectKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_product_images_ObjectKey",
                schema: "catalog",
                table: "product_images");

            migrationBuilder.DropColumn(
                name: "ContentType",
                schema: "catalog",
                table: "product_images");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                schema: "catalog",
                table: "product_images");

            migrationBuilder.RenameColumn(
                name: "ObjectKey",
                schema: "catalog",
                table: "product_images",
                newName: "StoragePath");
        }
    }
}
