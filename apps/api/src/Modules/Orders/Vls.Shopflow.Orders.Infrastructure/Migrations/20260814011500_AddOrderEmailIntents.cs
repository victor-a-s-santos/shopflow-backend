using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderEmailIntents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_intents",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_intents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_intents_IdempotencyKey",
                schema: "orders",
                table: "email_intents",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_intents_OrderId_Type",
                schema: "orders",
                table: "email_intents",
                columns: new[] { "OrderId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_email_intents_Status_CreatedAt",
                schema: "orders",
                table: "email_intents",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_intents",
                schema: "orders");
        }
    }
}
