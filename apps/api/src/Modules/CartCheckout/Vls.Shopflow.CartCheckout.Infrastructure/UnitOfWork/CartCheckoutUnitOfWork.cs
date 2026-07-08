using Vls.Shopflow.BuildingBlocks.Infrastructure.UnitOfWork;
using Vls.Shopflow.CartCheckout.Application.Repositories;

namespace Vls.Shopflow.CartCheckout.Infrastructure.UnitOfWork;

public sealed class CartCheckoutUnitOfWork(CartCheckoutDbContext db)
    : EfUnitOfWork<CartCheckoutDbContext>(db), ICartCheckoutUnitOfWork;
