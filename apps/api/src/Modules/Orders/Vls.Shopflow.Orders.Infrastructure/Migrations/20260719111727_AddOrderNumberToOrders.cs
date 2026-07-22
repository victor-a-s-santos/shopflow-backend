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
            migrationBuilder.Sql("""
                CREATE SEQUENCE IF NOT EXISTS orders.orders_order_number_seq
                    AS bigint
                    START WITH 10000
                    INCREMENT BY 1
                    MINVALUE 10000
                    NO CYCLE;
                """);

            migrationBuilder.AddColumn<long>(
                name: "OrderNumber",
                schema: "orders",
                table: "orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT "Id",
                           9999 + ROW_NUMBER() OVER (ORDER BY "CreatedAt", "Id") AS n
                    FROM orders.orders
                )
                UPDATE orders.orders o
                SET "OrderNumber" = numbered.n
                FROM numbered
                WHERE o."Id" = numbered."Id";
                """);

            migrationBuilder.Sql("""
                SELECT setval(
                    'orders.orders_order_number_seq',
                    GREATEST(
                        9999,
                        COALESCE((SELECT MAX("OrderNumber") FROM orders.orders), 9999)
                    )
                );
                """);

            migrationBuilder.AlterColumn<long>(
                name: "OrderNumber",
                schema: "orders",
                table: "orders",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_OrderNumber",
                schema: "orders",
                table: "orders",
                column: "OrderNumber",
                unique: true);
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
