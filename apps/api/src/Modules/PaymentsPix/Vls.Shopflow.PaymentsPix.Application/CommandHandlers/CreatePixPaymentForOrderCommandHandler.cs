using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.PaymentsPix.Application.Commands;
using Vls.Shopflow.PaymentsPix.Application.DataTransferObjects;
using Vls.Shopflow.PaymentsPix.Application.Interfaces;
using Vls.Shopflow.PaymentsPix.Application.Mappers;
using Vls.Shopflow.PaymentsPix.Application.Options;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Domain.Entities;
using Vls.Shopflow.PaymentsPix.Domain.Exceptions;

namespace Vls.Shopflow.PaymentsPix.Application.CommandHandlers;

public sealed class CreatePixPaymentForOrderCommandHandler(
    IOrderPaymentReader orderPaymentReader,
    IPixPaymentRepository paymentRepository,
    IPixPaymentProvider pixPaymentProvider,
    IPaymentsPixUnitOfWork unitOfWork,
    IOptions<MercadoPagoOptions> mercadoPagoOptions,
    ILogger<CreatePixPaymentForOrderCommandHandler> logger)
    : IRequestHandler<CreatePixPaymentForOrderCommand, CreatePixPaymentResult>
{
    public async Task<CreatePixPaymentResult> Handle(
        CreatePixPaymentForOrderCommand command,
        CancellationToken cancellationToken)
    {
        var existingPending = await paymentRepository.GetPendingByOrderIdAsync(
            command.OrderId,
            cancellationToken);

        if (existingPending is not null)
        {
            logger.LogInformation(
                "Returning existing pending Pix payment {PaymentId} for order {OrderId}",
                existingPending.Id,
                command.OrderId);

            return new CreatePixPaymentResult(PixPaymentMapper.ToDto(existingPending), WasCreated: false);
        }

        var order = await orderPaymentReader.GetByIdAsync(command.OrderId, cancellationToken)
                    ?? throw new OrderNotFoundForPixPaymentException(command.OrderId);

        PixPaymentMapper.EnsureOrderCanReceivePixPayment(order.Status, order.OrderId);

        if (order.Total <= 0)
            throw new InvalidOrderTotalForPixPaymentException(order.OrderId, order.Total);

        var expirationMinutes = Math.Max(1, mercadoPagoOptions.Value.PixExpirationMinutes);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes);

        var charge = await pixPaymentProvider.CreatePixChargeAsync(
            new PixChargeRequest(
                order.OrderId,
                order.Total,
                order.CustomerFullName,
                order.CustomerEmail,
                expiresAt),
            cancellationToken);

        var payment = PixPayment.CreatePending(
            order.OrderId,
            order.Total,
            charge.Provider,
            charge.ProviderOrderId,
            charge.ProviderTransactionId,
            charge.QrCode,
            charge.QrCodeImageUrl,
            charge.CopyPasteCode,
            charge.TicketUrl,
            charge.ProviderStatus,
            charge.ProviderStatusDetail,
            charge.ProviderTransactionStatus,
            charge.ProviderTransactionStatusDetail,
            charge.ExternalReference ?? order.OrderId.ToString("D"),
            charge.IdempotencyKey ?? order.OrderId.ToString("D"),
            charge.ExpiresAt ?? expiresAt);

        await paymentRepository.AddAsync(payment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created Pix payment {PaymentId} for order {OrderId} with status Pending. ProviderOrderId={ProviderOrderId}",
            payment.Id,
            order.OrderId,
            payment.ProviderOrderId);

        return new CreatePixPaymentResult(PixPaymentMapper.ToDto(payment), WasCreated: true);
    }
}
