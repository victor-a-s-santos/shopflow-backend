using Vls.Shopflow.PaymentsPix.Application.Repositories;

namespace Vls.Shopflow.PaymentsPix.Infrastructure.UnitOfWork;

public sealed class PaymentsPixUnitOfWork(PaymentsPixDbContext db) : IPaymentsPixUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => db.SaveChangesAsync(cancellationToken);
}
