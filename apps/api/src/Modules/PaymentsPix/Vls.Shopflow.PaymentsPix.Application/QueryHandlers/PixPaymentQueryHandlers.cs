using MediatR;
using Vls.Shopflow.PaymentsPix.Application.DataTransferObjects;
using Vls.Shopflow.PaymentsPix.Application.Mappers;
using Vls.Shopflow.PaymentsPix.Application.Queries;
using Vls.Shopflow.PaymentsPix.Application.Repositories;
using Vls.Shopflow.PaymentsPix.Domain.Exceptions;

namespace Vls.Shopflow.PaymentsPix.Application.QueryHandlers;

public sealed class GetPixPaymentByIdQueryHandler(IPixPaymentRepository paymentRepository)
    : IRequestHandler<GetPixPaymentByIdQuery, PixPaymentDto>
{
    public async Task<PixPaymentDto> Handle(
        GetPixPaymentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByIdAsync(query.PaymentId, cancellationToken)
                      ?? throw new PixPaymentNotFoundException(query.PaymentId);

        return PixPaymentMapper.ToDto(payment);
    }
}

public sealed class GetPixPaymentByOrderIdQueryHandler(IPixPaymentRepository paymentRepository)
    : IRequestHandler<GetPixPaymentByOrderIdQuery, PixPaymentDto>
{
    public async Task<PixPaymentDto> Handle(
        GetPixPaymentByOrderIdQuery query,
        CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetLatestByOrderIdAsync(query.OrderId, cancellationToken)
                      ?? throw new PixPaymentNotFoundForOrderException(query.OrderId);

        return PixPaymentMapper.ToDto(payment);
    }
}
