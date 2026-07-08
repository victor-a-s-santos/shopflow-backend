using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.CartCheckout.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCartCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cartcheckout");

            migrationBuilder.CreateTable(
                name: "checkout_sessions",
                schema: "cartcheckout",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    CustomerPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AddressZipCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AddressStreet = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AddressNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AddressComplement = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AddressNeighborhood = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AddressCity = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AddressState = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    ShippingAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    Total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    ReservationExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanceledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkout_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "checkout_session_items",
                schema: "cartcheckout",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckoutSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductSlug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SkuId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkuCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    InventoryReservationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkout_session_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_checkout_session_items_checkout_sessions_CheckoutSessionId",
                        column: x => x.CheckoutSessionId,
                        principalSchema: "cartcheckout",
                        principalTable: "checkout_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_checkout_session_items_CheckoutSessionId",
                schema: "cartcheckout",
                table: "checkout_session_items",
                column: "CheckoutSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_checkout_session_items_InventoryReservationId",
                schema: "cartcheckout",
                table: "checkout_session_items",
                column: "InventoryReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_checkout_session_items_SkuId",
                schema: "cartcheckout",
                table: "checkout_session_items",
                column: "SkuId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checkout_session_items",
                schema: "cartcheckout");

            migrationBuilder.DropTable(
                name: "checkout_sessions",
                schema: "cartcheckout");
        }
    }
}
