using System.Data;
using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.Orders.Application.Repositories;

namespace Vls.Shopflow.Orders.Infrastructure.Services;

/// <summary>
/// Allocates friendly remessa numbers from <c>orders.delivery_batches_batch_number_seq</c> (starts at 30000).
/// </summary>
public sealed class PostgresDeliveryBatchNumberGenerator(OrdersDbContext db) : IDeliveryBatchNumberGenerator
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
            command.CommandText = "SELECT nextval('orders.delivery_batches_batch_number_seq')";
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
