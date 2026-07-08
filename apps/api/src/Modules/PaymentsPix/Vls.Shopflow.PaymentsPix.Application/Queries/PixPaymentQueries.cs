using MediatR;
using Vls.Shopflow.PaymentsPix.Application.DataTransferObjects;

namespace Vls.Shopflow.PaymentsPix.Application.Queries;

public sealed record GetPixPaymentByIdQuery(Guid PaymentId) : IRequest<PixPaymentDto>;

public sealed record GetPixPaymentByOrderIdQuery(Guid OrderId) : IRequest<PixPaymentDto>;
