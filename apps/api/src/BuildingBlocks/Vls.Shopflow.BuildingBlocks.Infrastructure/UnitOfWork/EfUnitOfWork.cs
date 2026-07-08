using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.BuildingBlocks.Application.Interfaces;

namespace Vls.Shopflow.BuildingBlocks.Infrastructure.UnitOfWork;

public class EfUnitOfWork<TDbContext> : IUnitOfWork where TDbContext : DbContext
{
    private readonly TDbContext _db;
    public EfUnitOfWork(TDbContext db) => _db = db;
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}