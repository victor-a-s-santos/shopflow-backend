using FluentValidation;
using Vls.Shopflow.Inventory.Application.Commands;

namespace Vls.Shopflow.Inventory.Application.Validations;

public sealed class CancelStockReservationCommandValidator : AbstractValidator<CancelStockReservationCommand>
{
    public CancelStockReservationCommandValidator()
    {
        RuleFor(x => x.ReservationId).NotEmpty();
    }
}
