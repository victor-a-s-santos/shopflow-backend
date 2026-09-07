using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Vls.Shopflow.IdentityAccess.Infrastructure;

#nullable disable

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(IdentityAccessDbContext))]
    [Migration("20260907010000_AddCustomerAddresses")]
    public partial class AddCustomerAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_addresses",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Complement = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Neighborhood = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_addresses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_addresses_CustomerUserId",
                schema: "identity",
                table: "customer_addresses",
                column: "CustomerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_addresses_CustomerUserId_IsDefault",
                schema: "identity",
                table: "customer_addresses",
                columns: new[] { "CustomerUserId", "IsDefault" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_addresses",
                schema: "identity");
        }
    }
}
