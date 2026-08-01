using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE SEQUENCE IF NOT EXISTS orders.delivery_batches_batch_number_seq
                    AS bigint
                    INCREMENT BY 1
                    MINVALUE 1
                    START WITH 30000
                    NO CYCLE;
                """);

            migrationBuilder.CreateTable(
                name: "delivery_batches",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchNumber = table.Column<long>(type: "bigint", nullable: false),
                    CustomerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    CustomerEmailNormalized = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CustomerPhoneNormalized = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DeliveryMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TrackingCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    InternalNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ShippedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    HasDifferentDeliveryAddresses = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "delivery_batch_orders",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_batch_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_delivery_batch_orders_delivery_batches_DeliveryBatchId",
                        column: x => x.DeliveryBatchId,
                        principalSchema: "orders",
                        principalTable: "delivery_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_batch_orders_DeliveryBatchId",
                schema: "orders",
                table: "delivery_batch_orders",
                column: "DeliveryBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_batch_orders_OrderId",
                schema: "orders",
                table: "delivery_batch_orders",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_batches_BatchNumber",
                schema: "orders",
                table: "delivery_batches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_batches_CustomerEmailNormalized_Status",
                schema: "orders",
                table: "delivery_batches",
                columns: new[] { "CustomerEmailNormalized", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_batches_CustomerUserId_Status",
                schema: "orders",
                table: "delivery_batches",
                columns: new[] { "CustomerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_batches_Status_CreatedAt",
                schema: "orders",
                table: "delivery_batches",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_batch_orders",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "delivery_batches",
                schema: "orders");

            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS orders.delivery_batches_batch_number_seq;");
        }
    }
}
