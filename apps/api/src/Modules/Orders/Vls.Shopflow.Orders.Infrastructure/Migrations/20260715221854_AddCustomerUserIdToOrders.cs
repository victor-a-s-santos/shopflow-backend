using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerUserIdToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerUserId",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_CustomerUserId_CreatedAt",
                schema: "orders",
                table: "orders",
                columns: new[] { "CustomerUserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_CustomerUserId_CreatedAt",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "CustomerUserId",
                schema: "orders",
                table: "orders");
        }
    }
}
