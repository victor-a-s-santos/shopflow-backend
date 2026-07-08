using Microsoft.EntityFrameworkCore;
using Vls.Shopflow.CartCheckout.Infrastructure;
using Vls.Shopflow.Orders.Application.Interfaces;

namespace Vls.Shopflow.Orders.Infrastructure.Services;

public sealed class CheckoutSessionReader(CartCheckoutDbContext cartCheckoutDb) : ICheckoutSessionReader
{
    public async Task<CheckoutSessionSnapshot?> GetByIdAsync(
        Guid checkoutSessionId,
        CancellationToken cancellationToken)
    {
        var session = await cartCheckoutDb.CheckoutSessions
            .AsNoTracking()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == checkoutSessionId, cancellationToken);

        if (session is null)
            return null;

        return new CheckoutSessionSnapshot(
            session.Id,
            session.Status.ToString(),
            session.CustomerName,
            session.CustomerEmail,
            session.CustomerPhone,
            session.AddressZipCode,
            session.AddressStreet,
            session.AddressNumber,
            session.AddressComplement,
            session.AddressNeighborhood,
            session.AddressCity,
            session.AddressState,
            session.Subtotal,
            session.ShippingAmount,
            session.Total,
            session.Items.Select(i => new CheckoutSessionItemSnapshot(
                i.SkuId,
                i.ProductName,
                i.SkuCode,
                i.Quantity,
                i.UnitPrice,
                i.Subtotal)).ToList());
    }
}
