using FluentValidation;
using Vls.Shopflow.Inventory.Application.Commands;

namespace Vls.Shopflow.Inventory.Application.Validations;

public sealed class ConfirmStockReservationCommandValidator : AbstractValidator<ConfirmStockReservationCommand>
{
    public ConfirmStockReservationCommandValidator()
    {
        RuleFor(x => x.ReservationId).NotEmpty();
    }
}
