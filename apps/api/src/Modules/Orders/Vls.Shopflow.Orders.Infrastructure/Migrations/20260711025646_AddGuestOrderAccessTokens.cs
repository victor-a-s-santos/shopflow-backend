using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestOrderAccessTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guest_order_access_tokens",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TokenHashAlgorithm = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UsageCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_order_access_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guest_order_access_tokens_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "orders",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_guest_order_access_tokens_ExpiresAt",
                schema: "orders",
                table: "guest_order_access_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_guest_order_access_tokens_OrderId",
                schema: "orders",
                table: "guest_order_access_tokens",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_guest_order_access_tokens_OrderId_TokenHash",
                schema: "orders",
                table: "guest_order_access_tokens",
                columns: new[] { "OrderId", "TokenHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guest_order_access_tokens_TokenHash",
                schema: "orders",
                table: "guest_order_access_tokens",
                column: "TokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guest_order_access_tokens",
                schema: "orders");
        }
    }
}
