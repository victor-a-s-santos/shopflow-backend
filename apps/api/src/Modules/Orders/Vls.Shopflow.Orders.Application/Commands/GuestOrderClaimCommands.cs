using MediatR;
using Vls.Shopflow.Orders.Application.DataTransferObjects;

namespace Vls.Shopflow.Orders.Application.Commands;

public sealed record CreateAccountFromGuestOrderCommand(
    Guid OrderId,
    string? GuestAccessToken,
    string Password,
    string ConfirmPassword) : IRequest<CreateAccountFromGuestOrderResult>;

public sealed record ClaimGuestOrderCommand(
    Guid OrderId,
    string? GuestAccessToken,
    Guid CustomerUserId,
    string CustomerEmail) : IRequest<ClaimGuestOrderResult>;
