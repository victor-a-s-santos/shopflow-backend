using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Exceptions;

namespace Vls.Shopflow.Orders.Application.CommandHandlers;

public sealed class CreateAccountFromGuestOrderCommandHandler(
    IGuestOrderAccessGate guestOrderAccessGate,
    ICustomerAccountPort customerAccountPort,
    IOrdersUnitOfWork unitOfWork,
    ILogger<CreateAccountFromGuestOrderCommandHandler> logger)
    : IRequestHandler<CreateAccountFromGuestOrderCommand, CreateAccountFromGuestOrderResult>
{
    public async Task<CreateAccountFromGuestOrderResult> Handle(
        CreateAccountFromGuestOrderCommand request,
        CancellationToken cancellationToken)
    {
        var (token, order) = await guestOrderAccessGate.ValidateAsync(
            request.OrderId,
            request.GuestAccessToken,
            cancellationToken);

        if (order.CustomerUserId is not null)
            throw new OrderAlreadyLinkedToAnotherCustomerException(order.Id);

        if (await customerAccountPort.EmailExistsAsync(order.CustomerEmail, cancellationToken))
            throw new GuestOrderAccountAlreadyExistsException();

        var register = await customerAccountPort.RegisterAsync(
            order.CustomerEmail,
            request.Password,
            order.CustomerFullName,
            order.CustomerPhone,
            cancellationToken);

        if (register.IsDuplicateEmail)
            throw new GuestOrderAccountAlreadyExistsException();

        if (!register.Succeeded || register.CustomerUserId is null)
        {
            var failures = register.Errors.Count > 0
                ? register.Errors.Select(e => new ValidationFailure(e.Field, e.Message)).ToList()
                : [new ValidationFailure("password", "Unable to complete registration.")];
            throw new ValidationException(failures);
        }

        order.LinkToCustomerUser(register.CustomerUserId.Value);
        token.MarkUsed(DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await customerAccountPort.SignInAsync(register.CustomerUserId.Value, cancellationToken);

        logger.LogInformation(
            "Guest order {OrderId} linked to new customer {CustomerUserId}.",
            order.Id,
            register.CustomerUserId.Value);

        return new CreateAccountFromGuestOrderResult(
            order.Id,
            CustomerCreated: true,
            OrderLinked: true,
            RedirectTo: $"/account/orders/{order.Id}");
    }
}

public sealed class ClaimGuestOrderCommandHandler(
    IGuestOrderAccessGate guestOrderAccessGate,
    IOrdersUnitOfWork unitOfWork,
    ILogger<ClaimGuestOrderCommandHandler> logger)
    : IRequestHandler<ClaimGuestOrderCommand, ClaimGuestOrderResult>
{
    public async Task<ClaimGuestOrderResult> Handle(
        ClaimGuestOrderCommand request,
        CancellationToken cancellationToken)
    {
        var (token, order) = await guestOrderAccessGate.ValidateAsync(
            request.OrderId,
            request.GuestAccessToken,
            cancellationToken);

        if (!EmailsMatch(order.CustomerEmail, request.CustomerEmail))
            throw new GuestOrderClaimForbiddenException();

        if (order.CustomerUserId is { } existing && existing != request.CustomerUserId)
            throw new OrderAlreadyLinkedToAnotherCustomerException(order.Id);

        var alreadyLinked = order.CustomerUserId == request.CustomerUserId;
        order.LinkToCustomerUser(request.CustomerUserId);

        if (!alreadyLinked)
        {
            token.MarkUsed(DateTimeOffset.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Guest order {OrderId} claimed by customer {CustomerUserId}.",
                order.Id,
                request.CustomerUserId);
        }

        return new ClaimGuestOrderResult(
            order.Id,
            OrderLinked: true,
            RedirectTo: $"/account/orders/{order.Id}");
    }

    internal static bool EmailsMatch(string orderEmail, string customerEmail)
        => string.Equals(
            orderEmail.Trim(),
            customerEmail.Trim(),
            StringComparison.OrdinalIgnoreCase);
}
