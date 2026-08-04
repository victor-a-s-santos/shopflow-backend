using FluentAssertions;
using Xunit;
using Vls.Shopflow.Notifications.Application.Interfaces;
using Vls.Shopflow.Notifications.Application.Options;
using Vls.Shopflow.Notifications.Application.Templates;

namespace Vls.Shopflow.Notifications.UnitTests.Application;

public sealed class TransactionalEmailTemplatesTests
{
    private static readonly PublicAppOptions App = new()
    {
        BaseUrl = "https://loja.example.com",
        StoreName = "Vip Assessoria"
    };

    [Fact]
    public void ConfirmEmail_RendersLinkWithoutLoggingTokenInSubject()
    {
        var (subject, html, text) = TransactionalEmailTemplates.ConfirmEmail(
            App, "a@b.com", "Ana Silva", "raw-token-value");

        subject.Should().Be("Confirme seu cadastro");
        html.Should().Contain("confirm-email?");
        html.Should().Contain("token=");
        html.Should().NotContain("InternalOrderNote");
        text.Should().Contain("raw-token-value");
    }

    [Fact]
    public void ResetPassword_RendersLink()
    {
        var (subject, html, _) = TransactionalEmailTemplates.ResetPassword(
            App, "a@b.com", "Ana", "reset-token");

        subject.Should().Be("Redefinição de senha");
        html.Should().Contain("reset-password?");
        html.Should().Contain("reset-token");
    }

    [Fact]
    public void OrderCreated_IncludesOrderNumberAndTotal()
    {
        var order = SampleOrder();
        var (subject, html, _) = TransactionalEmailTemplates.OrderCreated(App, order);

        subject.Should().Contain("#10582");
        html.Should().Contain("10582");
        html.Should().Contain("R$");
        html.Should().Contain("Aguardando pagamento");
        html.Should().NotContain("nota interna");
        html.Should().NotContain("Internal");
    }

    [Fact]
    public void PaymentConfirmed_IncludesOrderNumber()
    {
        var (subject, html, _) = TransactionalEmailTemplates.PaymentConfirmed(App, SampleOrder());
        subject.Should().Contain("Pagamento confirmado");
        subject.Should().Contain("#10582");
        html.Should().Contain("10582");
    }

    [Fact]
    public void OrderShipped_IncludesTrackingWhenPresent()
    {
        var order = SampleOrder() with { TrackingCode = "BR123", FinalDeliveryMethod = "Carrier" };
        var (_, html, text) = TransactionalEmailTemplates.OrderShipped(App, order);

        html.Should().Contain("BR123");
        html.Should().Contain("Carrier");
        text.Should().Contain("BR123");
        html.Should().NotContain("Guid");
    }

    [Fact]
    public void OrderDelivered_Renders()
    {
        var (subject, html, _) = TransactionalEmailTemplates.OrderDelivered(App, SampleOrder());
        subject.Should().Contain("entregue");
        html.Should().Contain("10582");
    }

    [Fact]
    public void GuestOrderLink_IncludesTokenQuery()
    {
        var order = SampleOrder() with { CustomerUserId = null, GuestAccessToken = "secret-token" };
        var link = TransactionalEmailTemplates.BuildOrderLink(App, order);
        link.Should().StartWith("https://loja.example.com/pedido/10582");
        link.Should().Contain("t=secret-token");
    }

    [Fact]
    public void CustomerOrderLink_UsesAccountPath()
    {
        var order = SampleOrder() with { CustomerUserId = Guid.NewGuid(), GuestAccessToken = "should-not-appear" };
        var link = TransactionalEmailTemplates.BuildOrderLink(App, order);
        link.Should().Contain($"/account/orders/{order.OrderId:D}");
        link.Should().NotContain("should-not-appear");
    }

    private static OrderEmailNotificationRequest SampleOrder()
        => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            10582,
            "cliente@example.com",
            "Cliente Teste",
            199.90m);
}
