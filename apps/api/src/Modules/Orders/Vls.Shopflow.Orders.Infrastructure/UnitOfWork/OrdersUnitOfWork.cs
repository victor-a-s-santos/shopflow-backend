using Vls.Shopflow.BuildingBlocks.Infrastructure.UnitOfWork;
using Vls.Shopflow.Orders.Application.Repositories;

namespace Vls.Shopflow.Orders.Infrastructure.UnitOfWork;

public sealed class OrdersUnitOfWork(OrdersDbContext db)
    : EfUnitOfWork<OrdersDbContext>(db), IOrdersUnitOfWork;
