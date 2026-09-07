using FluentAssertions;
using Vls.Shopflow.IdentityAccess.Application.Services;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests;

public sealed class CustomerAddressRulesTests
{
    [Fact]
    public void Validate_AcceptsBrazilianCepAndUf()
    {
        var errors = CustomerAddressRules.Validate(
            "Casa",
            "Ana Silva",
            "01310-100",
            "Av. Paulista",
            "1000",
            null,
            "Bela Vista",
            "São Paulo",
            "sp");

        errors.Should().BeEmpty();
        CustomerAddressRules.TryNormalizePostalCode("01310-100").Should().Be("01310100");
        CustomerAddressRules.FormatPostalCode("01310100").Should().Be("01310-100");
    }

    [Fact]
    public void Validate_RejectsInvalidCepAndUf()
    {
        var errors = CustomerAddressRules.Validate(
            null,
            null,
            "123",
            "",
            "",
            null,
            "",
            "",
            "SPP");

        errors.Should().ContainKey("postalCode");
        errors.Should().ContainKey("recipientName");
        errors.Should().ContainKey("street");
        errors.Should().ContainKey("number");
        errors.Should().ContainKey("city");
        errors.Should().ContainKey("state");
    }
}
