using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vls.Shopflow.Orders.Infrastructure;
using Vls.Shopflow.Orders.Infrastructure.Services;

namespace Vls.Shopflow.Orders.IntegrationTests;

public sealed class OrderNumberSequenceMigrationTests
{
    private static readonly string BaseConnectionString =
        Environment.GetEnvironmentVariable("SHOPFLOW_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=shopflow;Username=postgres;Password=postgres";

    [Fact]
    public async Task EmptyDatabase_FirstNextval_Is10000()
    {
        var adminCs = new NpgsqlConnectionStringBuilder(BaseConnectionString)
        {
            Database = "postgres"
        }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminCs);
        try
        {
            await admin.OpenAsync();
        }
        catch
        {
            return;
        }

        var dbName = $"shopflow_ordernumber_seq_{Guid.NewGuid():N}";
        await using (var create = admin.CreateCommand())
        {
            create.CommandText = $"CREATE DATABASE \"{dbName}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var cs = new NpgsqlConnectionStringBuilder(BaseConnectionString)
            {
                Database = dbName
            }.ConnectionString;

            var options = new DbContextOptionsBuilder<OrdersDbContext>()
                .UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "orders"))
                .Options;

            await using (var db = new OrdersDbContext(options))
            {
                await db.Database.MigrateAsync();
            }

            await using var db2 = new OrdersDbContext(options);
            var generator = new PostgresOrderNumberGenerator(db2);
            var first = await generator.NextAsync();
            first.Should().Be(10000);
        }
        finally
        {
            await using (var terminate = admin.CreateCommand())
            {
                terminate.CommandText = $"""
                    SELECT pg_terminate_backend(pid)
                    FROM pg_stat_activity
                    WHERE datname = '{dbName}' AND pid <> pg_backend_pid();
                    """;
                await terminate.ExecuteNonQueryAsync();
            }

            await using (var drop = admin.CreateCommand())
            {
                drop.CommandText = $"DROP DATABASE IF EXISTS \"{dbName}\"";
                await drop.ExecuteNonQueryAsync();
            }
        }
    }
}
