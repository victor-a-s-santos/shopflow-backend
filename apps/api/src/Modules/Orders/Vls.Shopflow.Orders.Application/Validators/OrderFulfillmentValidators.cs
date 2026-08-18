using FluentValidation;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Application.Validators;

public sealed class ShipOrderFulfillmentCommandValidator : AbstractValidator<ShipOrderFulfillmentCommand>
{
    public ShipOrderFulfillmentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();

        RuleFor(x => x.FinalDeliveryMethod)
            .Must(s => Enum.TryParse<DeliveryMethod>(s!.Trim(), ignoreCase: true, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.FinalDeliveryMethod))
            .WithErrorCode("INVALID_DELIVERY_METHOD")
            .WithMessage("finalDeliveryMethod must be Carrier, ExcursionBus, or Correios.");

        RuleFor(x => x.TrackingCode)
            .MaximumLength(Order.TrackingCodeMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.TrackingCode))
            .WithErrorCode("TRACKING_CODE_TOO_LONG")
            .WithMessage("O código de rastreio deve ter no máximo 120 caracteres.");

        RuleFor(x => x.InternalNote)
            .MaximumLength(Order.InternalOrderNoteMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.InternalNote))
            .WithErrorCode("INTERNAL_NOTE_TOO_LONG")
            .WithMessage("A observação interna deve ter no máximo 2000 caracteres.");
    }
}

public sealed class DeliverOrderFulfillmentCommandValidator : AbstractValidator<DeliverOrderFulfillmentCommand>
{
    public DeliverOrderFulfillmentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();

        RuleFor(x => x.InternalNote)
            .MaximumLength(Order.InternalOrderNoteMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.InternalNote))
            .WithErrorCode("INTERNAL_NOTE_TOO_LONG")
            .WithMessage("A observação interna deve ter no máximo 2000 caracteres.");
    }
}

public sealed class UpdateOrderInternalNoteCommandValidator : AbstractValidator<UpdateOrderInternalNoteCommand>
{
    public UpdateOrderInternalNoteCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();

        RuleFor(x => x.InternalNote)
            .MaximumLength(Order.InternalOrderNoteMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.InternalNote))
            .WithErrorCode("INTERNAL_NOTE_TOO_LONG")
            .WithMessage("A observação interna deve ter no máximo 2000 caracteres.");
    }
}
