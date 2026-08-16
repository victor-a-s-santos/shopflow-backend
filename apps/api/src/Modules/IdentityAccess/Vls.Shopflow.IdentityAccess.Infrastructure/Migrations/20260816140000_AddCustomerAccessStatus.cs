using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Vls.Shopflow.IdentityAccess.Infrastructure;

#nullable disable

namespace Vls.Shopflow.IdentityAccess.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(IdentityAccessDbContext))]
    [Migration("20260816140000_AddCustomerAccessStatus")]
    public partial class AddCustomerAccessStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AccessDecidedAt",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccessDecidedByAdminUserId",
                schema: "identity",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessDecisionReason",
                schema: "identity",
                table: "users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AccessRequestedAt",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccessStatus",
                schema: "identity",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE identity.users
                SET "ApprovedAt" = "CreatedAt",
                    "AccessRequestedAt" = "CreatedAt"
                WHERE "AccessStatus" = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_users_IsStaff_AccessStatus_AccessRequestedAt",
                schema: "identity",
                table: "users",
                columns: new[] { "IsStaff", "AccessStatus", "AccessRequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_IsStaff_AccessStatus_AccessRequestedAt",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AccessDecidedAt",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AccessDecidedByAdminUserId",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AccessDecisionReason",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AccessRequestedAt",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AccessStatus",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                schema: "identity",
                table: "users");
        }
    }
}
