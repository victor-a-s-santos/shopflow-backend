using MediatR;
using Vls.Shopflow.PaymentsPix.Application.DataTransferObjects;

namespace Vls.Shopflow.PaymentsPix.Application.Commands;

public sealed record CreatePixPaymentForOrderCommand(Guid OrderId) : IRequest<CreatePixPaymentResult>;
