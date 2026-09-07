using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Vls.Shopflow.CartCheckout.Infrastructure;

#nullable disable

namespace Vls.Shopflow.CartCheckout.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CartCheckoutDbContext))]
    [Migration("20260907011000_AddProductImageUrlToCheckoutSessionItems")]
    public partial class AddProductImageUrlToCheckoutSessionItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductImageUrl",
                schema: "cartcheckout",
                table: "checkout_session_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductImageUrl",
                schema: "cartcheckout",
                table: "checkout_session_items");
        }
    }
}
