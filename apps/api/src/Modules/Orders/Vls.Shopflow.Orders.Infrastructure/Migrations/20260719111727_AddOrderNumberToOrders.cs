using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderNumberToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Friendly order numbers start at 10000 (nextval on a fresh sequence).
            // Idempotent so a retry after a failed setval on an empty database can finish.
            migrationBuilder.Sql("""
                CREATE SEQUENCE IF NOT EXISTS orders.orders_order_number_seq
                    AS bigint
                    START WITH 10000
                    INCREMENT BY 1
                    MINVALUE 10000
                    NO CYCLE;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE orders.orders
                    ADD COLUMN IF NOT EXISTS "OrderNumber" bigint;
                """);

            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT "Id",
                           9999 + ROW_NUMBER() OVER (ORDER BY "CreatedAt", "Id") AS n
                    FROM orders.orders
                    WHERE "OrderNumber" IS NULL
                )
                UPDATE orders.orders o
                SET "OrderNumber" = numbered.n
                FROM numbered
                WHERE o."Id" = numbered."Id";
                """);

            // setval(seq, v) is setval(seq, v, true) → next nextval() is v+1.
            // Empty DB used to call setval(..., 9999), which is below MINVALUE 10000.
            // Empty: setval(10000, false) → first nextval() = 10000.
            // Existing rows: setval(MAX, true) → first nextval() = MAX+1.
            migrationBuilder.Sql("""
                SELECT setval(
                    'orders.orders_order_number_seq',
                    COALESCE((SELECT MAX("OrderNumber") FROM orders.orders), 10000),
                    EXISTS (SELECT 1 FROM orders.orders WHERE "OrderNumber" IS NOT NULL)
                );
                """);

            migrationBuilder.Sql("""
                ALTER TABLE orders.orders
                    ALTER COLUMN "OrderNumber" SET NOT NULL;
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_orders_OrderNumber"
                    ON orders.orders ("OrderNumber");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_OrderNumber",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "OrderNumber",
                schema: "orders",
                table: "orders");

            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS orders.orders_order_number_seq;");
        }
    }
}
