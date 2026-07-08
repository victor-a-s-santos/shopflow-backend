using Vls.Shopflow.BuildingBlocks.Infrastructure.UnitOfWork;
using Vls.Shopflow.Catalog.Application.Repositories;

namespace Vls.Shopflow.Catalog.Infrastructure.UnitOfWork;

public sealed class CatalogUnitOfWork : EfUnitOfWork<CatalogDbContext>, ICatalogUnitOfWork
{
    public CatalogUnitOfWork(CatalogDbContext db) : base(db) { }
}