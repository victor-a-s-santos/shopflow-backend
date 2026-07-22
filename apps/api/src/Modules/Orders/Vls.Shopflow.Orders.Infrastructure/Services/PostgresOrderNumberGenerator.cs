using System.Data;
using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Application.Interfaces;

namespace Vls.Shopflow.Orders.Infrastructure.Services;

/// <summary>
/// Allocates unique friendly order numbers from PostgreSQL sequence <c>orders.orders_order_number_seq</c>.
/// </summary>
public sealed class PostgresOrderNumberGenerator(OrdersDbContext db) : IOrderNumberGenerator
{
    public async Task<long> NextAsync(CancellationToken cancellationToken = default)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await db.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT nextval('orders.orders_order_number_seq')";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result);
        }
        finally
        {
            if (shouldClose)
                await db.Database.CloseConnectionAsync();
        }
    }
}
