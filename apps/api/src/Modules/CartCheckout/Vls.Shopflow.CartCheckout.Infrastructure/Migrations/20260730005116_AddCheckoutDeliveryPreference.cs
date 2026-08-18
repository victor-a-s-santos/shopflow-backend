using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.CartCheckout.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutDeliveryPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerOrderNote",
                schema: "cartcheckout",
                table: "checkout_sessions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PreferredDeliveryDate",
                schema: "cartcheckout",
                table: "checkout_sessions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredDeliveryMethod",
                schema: "cartcheckout",
                table: "checkout_sessions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerOrderNote",
                schema: "cartcheckout",
                table: "checkout_sessions");

            migrationBuilder.DropColumn(
                name: "PreferredDeliveryDate",
                schema: "cartcheckout",
                table: "checkout_sessions");

            migrationBuilder.DropColumn(
                name: "PreferredDeliveryMethod",
                schema: "cartcheckout",
                table: "checkout_sessions");
        }
    }
}
