using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.DataTransferObjects;
using Vls.Shopflow.Orders.Application.Interfaces;
using Vls.Shopflow.Orders.Application.Repositories;
using Vls.Shopflow.Orders.Domain.Constants;
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
            if (register.Errors.Count == 0)
            {
                throw new PasswordRequirementsNotMetException(
                [
                    ("password", "A senha não atende aos requisitos.")
                ]);
            }

            var passwordOnly = register.Errors.All(e =>
                string.Equals(e.Field, "password", StringComparison.OrdinalIgnoreCase));

            if (passwordOnly)
            {
                throw new PasswordRequirementsNotMetException(
                    register.Errors.Select(e => (e.Field, e.Message)).ToList());
            }

            var failures = register.Errors
                .Select(e => new ValidationFailure(e.Field, e.Message))
                .ToList();
            throw new ValidationException(failures);
        }

        order.LinkToCustomerUser(register.CustomerUserId.Value);
        token.MarkUsed(DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await customerAccountPort.SignInAsync(register.CustomerUserId.Value, cancellationToken);

        logger.LogInformation(
            "Guest order {OrderId} (#{OrderNumber}) linked to new customer {CustomerUserId}.",
            order.Id,
            order.OrderNumber,
            register.CustomerUserId.Value);

        return new CreateAccountFromGuestOrderResult(
            GuestOrderErrorCodes.AccountCreatedAndOrderLinked,
            order.Id,
            order.FormatOrderNumber(),
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
                "Guest order {OrderId} (#{OrderNumber}) claimed by customer {CustomerUserId}.",
                order.Id,
                order.OrderNumber,
                request.CustomerUserId);
        }

        return new ClaimGuestOrderResult(
            GuestOrderErrorCodes.OrderLinked,
            order.Id,
            order.FormatOrderNumber(),
            OrderLinked: true,
            RedirectTo: $"/account/orders/{order.Id}");
    }

    internal static bool EmailsMatch(string orderEmail, string customerEmail)
        => string.Equals(
            orderEmail.Trim(),
            customerEmail.Trim(),
            StringComparison.OrdinalIgnoreCase);
}
