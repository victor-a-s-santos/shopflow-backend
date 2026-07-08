using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.PaymentsPix.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPaymentsPix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payments_pix");

            migrationBuilder.CreateTable(
                name: "pix_payments",
                schema: "payments_pix",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProviderPaymentId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    QrCode = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    QrCodeImageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CopyPasteCode = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CanceledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pix_payments", x => x.Id);
                    table.CheckConstraint("CK_pix_payments_amount_positive", "\"Amount\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_pix_payments_OrderId",
                schema: "payments_pix",
                table: "pix_payments",
                column: "OrderId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_pix_payments_ProviderPaymentId",
                schema: "payments_pix",
                table: "pix_payments",
                column: "ProviderPaymentId",
                filter: "\"ProviderPaymentId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pix_payments",
                schema: "payments_pix");
        }
    }
}
