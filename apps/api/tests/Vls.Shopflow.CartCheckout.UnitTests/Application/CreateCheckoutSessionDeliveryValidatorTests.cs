using FluentValidation.TestHelper;
using Vls.Shopflow.CartCheckout.Application.Commands;
using Vls.Shopflow.CartCheckout.Application.Validators;
using Vls.Shopflow.CartCheckout.Domain.Services;

namespace Vls.Shopflow.CartCheckout.UnitTests.Application;

public sealed class CreateCheckoutSessionDeliveryValidatorTests
{
    private static CreateCheckoutSessionCommand ValidBase(DateOnly? preferredDate = null, string? method = null)
        => new(
            new CustomerInput("João", "joao@test.com", "11999999999"),
            new AddressInput("01001000", "Rua A", "1", null, "Centro", "São Paulo", "SP"),
            [new CheckoutItemInput(Guid.NewGuid(), 1)],
            method,
            preferredDate,
            null);

    [Fact]
    public void PreferredDeliveryDate_TooSoon_FailsWithCode()
    {
        var validator = new CreateCheckoutSessionCommandValidator();
        var tooSoon = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var result = validator.TestValidate(ValidBase(tooSoon));

        result.ShouldHaveValidationErrorFor(x => x.PreferredDeliveryDate)
            .WithErrorCode(DeliveryDatePolicy.DeliveryDateTooSoonCode);
    }

    [Fact]
    public void PreferredDeliveryMethod_Invalid_Fails()
    {
        var validator = new CreateCheckoutSessionCommandValidator();
        var result = validator.TestValidate(ValidBase(method: "Bike"));
        result.ShouldHaveValidationErrorFor(x => x.PreferredDeliveryMethod);
    }

    [Fact]
    public void PreferredDeliveryFields_Omitted_Pass()
    {
        var validator = new CreateCheckoutSessionCommandValidator();
        var result = validator.TestValidate(ValidBase());
        result.ShouldNotHaveAnyValidationErrors();
    }
}
