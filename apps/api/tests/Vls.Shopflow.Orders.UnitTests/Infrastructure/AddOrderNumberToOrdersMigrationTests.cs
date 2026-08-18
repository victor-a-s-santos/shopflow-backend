using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Vls.Shopflow.Orders.Infrastructure.Migrations;

namespace Vls.Shopflow.Orders.UnitTests.Infrastructure;

public sealed class AddOrderNumberToOrdersMigrationTests
{
    [Fact]
    public void Up_EmptyDatabaseSetval_Uses10000WithIsCalledFalse()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var migration = new AddOrderNumberToOrders();
        InvokeUp(migration, builder);

        var sql = string.Join("\n\n", builder.Operations.OfType<SqlOperation>().Select(o => o.Sql));
        var setvalSql = sql[sql.IndexOf("setval(", StringComparison.Ordinal)..];

        setvalSql.Should().Contain("10000");
        setvalSql.Should().Contain("EXISTS");
        setvalSql.Should().NotContain("9999");
        setvalSql.Should().NotContain("GREATEST");
    }

    private static void InvokeUp(Migration migration, MigrationBuilder builder)
    {
        var method = typeof(AddOrderNumberToOrders).GetMethod(
            "Up",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        method.Should().NotBeNull();
        method!.Invoke(migration, [builder]);
    }
}
