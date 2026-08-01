using FluentValidation;
using Vls.Shopflow.Orders.Application.Commands;
using Vls.Shopflow.Orders.Application.Queries;
using Vls.Shopflow.Orders.Domain.Entities;
using Vls.Shopflow.Orders.Domain.Enums;

namespace Vls.Shopflow.Orders.Application.Validators;

public sealed class CreateDeliveryBatchCommandValidator : AbstractValidator<CreateDeliveryBatchCommand>
{
    public CreateDeliveryBatchCommandValidator()
    {
        RuleFor(x => x.OrderIds)
            .NotEmpty()
            .WithErrorCode("DELIVERY_BATCH_ORDER_IDS_REQUIRED")
            .WithMessage("Informe os pedidos para criar uma entrega agrupada.");

        RuleFor(x => x.OrderIds)
            .Must(ids => ids is not null && ids.Distinct().Count() >= DeliveryBatch.MinOrders)
            .When(x => x.OrderIds is { Count: > 0 })
            .WithErrorCode("DELIVERY_BATCH_MIN_ORDERS_REQUIRED")
            .WithMessage("Selecione pelo menos dois pedidos para criar uma entrega agrupada.");

        RuleFor(x => x.DeliveryMethod)
            .Must(s => Enum.TryParse<DeliveryMethod>(s!.Trim(), ignoreCase: true, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.DeliveryMethod))
            .WithErrorCode("INVALID_DELIVERY_METHOD")
            .WithMessage("deliveryMethod must be Carrier, ExcursionBus, or Correios.");

        RuleFor(x => x.TrackingCode)
            .MaximumLength(DeliveryBatch.TrackingCodeMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.TrackingCode))
            .WithErrorCode("TRACKING_CODE_TOO_LONG")
            .WithMessage("O código/rastreamento deve ter no máximo 120 caracteres.");

        RuleFor(x => x.InternalNote)
            .MaximumLength(DeliveryBatch.InternalNoteMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.InternalNote))
            .WithErrorCode("INTERNAL_NOTE_TOO_LONG")
            .WithMessage("A observação interna deve ter no máximo 2000 caracteres.");
    }
}

public sealed class ShipDeliveryBatchCommandValidator : AbstractValidator<ShipDeliveryBatchCommand>
{
    public ShipDeliveryBatchCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();

        RuleFor(x => x.DeliveryMethod)
            .Must(s => Enum.TryParse<DeliveryMethod>(s!.Trim(), ignoreCase: true, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.DeliveryMethod))
            .WithErrorCode("INVALID_DELIVERY_METHOD")
            .WithMessage("deliveryMethod must be Carrier, ExcursionBus, or Correios.");

        RuleFor(x => x.TrackingCode)
            .MaximumLength(DeliveryBatch.TrackingCodeMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.TrackingCode))
            .WithErrorCode("TRACKING_CODE_TOO_LONG")
            .WithMessage("O código/rastreamento deve ter no máximo 120 caracteres.");

        RuleFor(x => x.InternalNote)
            .MaximumLength(DeliveryBatch.InternalNoteMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.InternalNote))
            .WithErrorCode("INTERNAL_NOTE_TOO_LONG")
            .WithMessage("A observação interna deve ter no máximo 2000 caracteres.");
    }
}

public sealed class DeliverDeliveryBatchCommandValidator : AbstractValidator<DeliverDeliveryBatchCommand>
{
    public DeliverDeliveryBatchCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.InternalNote)
            .MaximumLength(DeliveryBatch.InternalNoteMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.InternalNote))
            .WithErrorCode("INTERNAL_NOTE_TOO_LONG")
            .WithMessage("A observação interna deve ter no máximo 2000 caracteres.");
    }
}

public sealed class UpdateDeliveryBatchInternalNoteCommandValidator
    : AbstractValidator<UpdateDeliveryBatchInternalNoteCommand>
{
    public UpdateDeliveryBatchInternalNoteCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.InternalNote)
            .MaximumLength(DeliveryBatch.InternalNoteMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.InternalNote))
            .WithErrorCode("INTERNAL_NOTE_TOO_LONG")
            .WithMessage("A observação interna deve ter no máximo 2000 caracteres.");
    }
}

public sealed class GetDeliveryBatchesQueryValidator : AbstractValidator<GetDeliveryBatchesQuery>
{
    public const int MaxPageSize = 100;

    private static readonly HashSet<string> AllowedSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAt_desc",
        "createdAt_asc"
    };

    public GetDeliveryBatchesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);

        RuleFor(x => x.Status)
            .Must(s => Enum.TryParse<DeliveryBatchStatus>(s!.Trim(), ignoreCase: true, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("status must be AwaitingShipment, Shipped, or Delivered.");

        RuleFor(x => x.Sort)
            .Must(s => AllowedSorts.Contains(s!.Trim()))
            .When(x => !string.IsNullOrWhiteSpace(x.Sort))
            .WithMessage("sort must be createdAt_desc or createdAt_asc.");

        RuleFor(x => x)
            .Must(x => x.CreatedFrom is null || x.CreatedTo is null || x.CreatedFrom <= x.CreatedTo)
            .WithMessage("createdFrom must be less than or equal to createdTo.");
    }
}

public sealed class GetDeliveryBatchByIdQueryValidator : AbstractValidator<GetDeliveryBatchByIdQuery>
{
    public GetDeliveryBatchByIdQueryValidator()
        => RuleFor(x => x.BatchId).NotEmpty();
}

public sealed class GetDeliveryBatchCandidatesQueryValidator : AbstractValidator<GetDeliveryBatchCandidatesQuery>
{
    public GetDeliveryBatchCandidatesQueryValidator()
        => RuleFor(x => x.OrderId).NotEmpty();
}
