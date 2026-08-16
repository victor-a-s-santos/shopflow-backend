using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Vls.Shopflow.IdentityAccess.Infrastructure;

#nullable disable

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(IdentityAccessDbContext))]
    [Migration("20260816153000_ExpandCustomerAccessDecisionReason")]
    public partial class ExpandCustomerAccessDecisionReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AccessDecisionReason",
                schema: "identity",
                table: "users",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AccessDecisionReason",
                schema: "identity",
                table: "users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
