using MediatR;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.CartCheckout.Application.Commands;
using Vls.Shopflow.CartCheckout.Application.Interfaces;
using Vls.Shopflow.CartCheckout.Application.Mappers;
using Vls.Shopflow.CartCheckout.Application.Repositories;
using Vls.Shopflow.CartCheckout.Application.Services;
using Vls.Shopflow.CartCheckout.Application.DataTransferObjects;
using Vls.Shopflow.CartCheckout.Domain.Entities;
using Vls.Shopflow.CartCheckout.Domain.Exceptions;

namespace Vls.Shopflow.CartCheckout.Application.CommandHandlers;

public sealed class CreateCheckoutSessionCommandHandler(
    ICatalogSkuPricingService catalogSkuPricing,
    IInventoryReservationService inventoryReservation,
    ICheckoutSessionRepository repository,
    ICartCheckoutUnitOfWork unitOfWork,
    ILogger<CreateCheckoutSessionCommandHandler> logger)
    : IRequestHandler<CreateCheckoutSessionCommand, CheckoutSessionResponseDto>
{
    private static readonly TimeSpan DefaultReservationTtl = TimeSpan.FromMinutes(15);

    public async Task<CheckoutSessionResponseDto> Handle(
        CreateCheckoutSessionCommand command,
        CancellationToken cancellationToken)
    {
        var consolidatedItems = CheckoutItemConsolidator.Consolidate(command.Items);
        var expiresAt = DateTimeOffset.UtcNow.Add(DefaultReservationTtl);
        var reservationIds = new List<Guid>();
        var sessionItems = new List<CheckoutSessionItem>();

        try
        {
            foreach (var line in consolidatedItems)
            {
                var pricing = await catalogSkuPricing.GetBySkuIdAsync(line.SkuId, cancellationToken)
                              ?? throw new CatalogSkuNotFoundException(line.SkuId);

                if (!pricing.SkuIsActive || !pricing.ProductIsActive)
                    throw new InactiveSkuException(line.SkuId);

                // quantity = units of the sold SKU (packages or pieces). Never × packageSize.
                CheckoutSalesRuleValidator.EnsurePurchaseQuantityAllowed(
                    line.SkuId,
                    line.Quantity,
                    pricing.SalesRule);

                var reservationId = await inventoryReservation.ReserveAsync(
                    line.SkuId,
                    line.Quantity,
                    expiresAt,
                    cancellationToken);

                reservationIds.Add(reservationId);

                sessionItems.Add(CheckoutSessionItem.Create(
                    pricing.ProductId,
                    pricing.ProductName,
                    pricing.ProductSlug,
                    pricing.SkuId,
                    pricing.SkuCode,
                    line.Quantity,
                    pricing.UnitPrice,
                    reservationId));
            }

            var session = CheckoutSession.CreatePending(
                command.Customer.FullName,
                command.Customer.Email,
                command.Customer.Phone,
                command.Address.ZipCode,
                command.Address.Street,
                command.Address.Number,
                command.Address.Complement,
                command.Address.Neighborhood,
                command.Address.City,
                command.Address.State,
                expiresAt,
                sessionItems);

            await repository.AddAsync(session, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Created checkout session {CheckoutSessionId} with {ItemCount} items",
                session.Id,
                session.Items.Count);

            return CheckoutSessionMapper.ToResponseDto(session);
        }
        catch
        {
            await CompensateReservationsAsync(inventoryReservation, reservationIds, cancellationToken);
            throw;
        }
    }

    internal static async Task CompensateReservationsAsync(
        IInventoryReservationService inventoryReservation,
        IReadOnlyList<Guid> reservationIds,
        CancellationToken cancellationToken)
    {
        foreach (var reservationId in reservationIds)
        {
            try
            {
                await inventoryReservation.CancelReservationAsync(reservationId, cancellationToken);
            }
            catch
            {
                // Best-effort compensation; original error is rethrown by caller.
            }
        }
    }
}
