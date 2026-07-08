using Vls.Shopflow.BuildingBlocks.Infrastructure.UnitOfWork;
using Vls.Shopflow.Inventory.Application.Repositories;

namespace Vls.Shopflow.Inventory.Infrastructure.UnitOfWork;

public sealed class InventoryUnitOfWork : EfUnitOfWork<InventoryDbContext>, IInventoryUnitOfWork
{
    public InventoryUnitOfWork(InventoryDbContext db) : base(db) { }
}
