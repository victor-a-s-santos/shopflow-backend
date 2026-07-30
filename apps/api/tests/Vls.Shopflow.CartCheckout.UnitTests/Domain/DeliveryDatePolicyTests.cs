using FluentAssertions;
using Vls.Shopflow.CartCheckout.Domain.Services;

namespace Vls.Shopflow.CartCheckout.UnitTests.Domain;

public sealed class DeliveryDatePolicyTests
{
    [Theory]
    [InlineData(2026, 7, 6, 2026, 7, 8)]   // Monday → Wednesday
    [InlineData(2026, 7, 7, 2026, 7, 9)]   // Tuesday → Thursday
    [InlineData(2026, 7, 8, 2026, 7, 10)]  // Wednesday → Friday
    [InlineData(2026, 7, 9, 2026, 7, 13)]  // Thursday → Monday
    [InlineData(2026, 7, 10, 2026, 7, 14)] // Friday → Tuesday
    [InlineData(2026, 7, 11, 2026, 7, 14)] // Saturday → Tuesday
    [InlineData(2026, 7, 12, 2026, 7, 14)] // Sunday → Tuesday
    public void GetMinimumPreferredDeliveryDate_AddsTwoBusinessDays(
        int y1, int m1, int d1, int y2, int m2, int d2)
    {
        var purchase = new DateOnly(y1, m1, d1);
        var expected = new DateOnly(y2, m2, d2);

        DeliveryDatePolicy.GetMinimumPreferredDeliveryDate(purchase).Should().Be(expected);
    }

    [Fact]
    public void IsValidPreferredDeliveryDate_RejectsBeforeMinimum()
    {
        var purchase = new DateOnly(2026, 7, 6); // Monday
        var tooSoon = new DateOnly(2026, 7, 7);  // Tuesday

        DeliveryDatePolicy.IsValidPreferredDeliveryDate(purchase, tooSoon).Should().BeFalse();
    }

    [Fact]
    public void IsValidPreferredDeliveryDate_AcceptsMinimumAndLater()
    {
        var purchase = new DateOnly(2026, 7, 6); // Monday
        var minimum = new DateOnly(2026, 7, 8);  // Wednesday
        var later = new DateOnly(2026, 7, 15);

        DeliveryDatePolicy.IsValidPreferredDeliveryDate(purchase, minimum).Should().BeTrue();
        DeliveryDatePolicy.IsValidPreferredDeliveryDate(purchase, later).Should().BeTrue();
    }
}
