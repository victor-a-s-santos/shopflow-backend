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

        subject.Should().Be("Confirme seu e-mail");
        html.Should().Contain("independente");
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

    [Fact]
    public void CustomerApprovalRequestAdmin_ContainsAdminLinkAndNoSecrets()
    {
        var customer = SampleCustomer();
        var (subject, html, _) = TransactionalEmailTemplates.CustomerApprovalRequestAdmin(App, customer);

        subject.Should().Be("Novo cadastro aguardando aprovação");
        html.Should().Contain("/admin/customers/approvals");
        html.Should().Contain("Ana Lojista");
        html.Should().Contain("ana@example.com");
        html.Should().Contain("11988887777");
        html.Should().NotContain("password");
        html.Should().NotContain("hash");
        html.Should().NotContain("AccessDecisionReason");
    }

    [Fact]
    public void CustomerRegistrationReceived_MentionsPendingReview()
    {
        var (subject, html, _) = TransactionalEmailTemplates.CustomerRegistrationReceived(App, SampleCustomer());
        subject.Should().Contain("solicitação de cadastro");
        html.Should().Contain("em análise");
        html.Should().Contain("/account/pending-approval");
    }

    [Fact]
    public void CustomerApproved_ContainsLoginLink()
    {
        var (subject, html, _) = TransactionalEmailTemplates.CustomerApproved(App, SampleCustomer());
        subject.Should().Be("Seu acesso foi aprovado");
        html.Should().Contain("/login");
    }

    [Fact]
    public void CustomerRejected_DoesNotIncludeInternalReason()
    {
        var (_, html, text) = TransactionalEmailTemplates.CustomerRejected(App, SampleCustomer());
        html.Should().NotContain("fora do perfil");
        html.Should().NotContain("AccessDecisionReason");
        text.Should().Contain("não foi aprovado");
    }

    [Fact]
    public void CustomerSuspended_DoesNotIncludeInternalReason()
    {
        var (subject, html, _) = TransactionalEmailTemplates.CustomerSuspended(App, SampleCustomer());
        subject.Should().Contain("acesso");
        html.Should().Contain("temporariamente bloqueado");
        html.Should().NotContain("inadimplencia");
        html.Should().NotContain("AccessDecisionReason");
    }

    [Fact]
    public void OrderShipped_DoesNotExposeBatchId()
    {
        var (_, html, _) = TransactionalEmailTemplates.OrderShipped(App, SampleOrder());
        html.Should().NotContain("DeliveryBatch");
        html.Should().NotContain("batchId");
    }

    [Fact]
    public void OrderDelivered_DoesNotExposeProviderIds()
    {
        var (_, html, _) = TransactionalEmailTemplates.OrderDelivered(App, SampleOrder());
        html.Should().NotContain("ProviderOrderId");
        html.Should().NotContain("InternalOrderNote");
    }

    private static CustomerApprovalEmailRequest SampleCustomer()
        => new(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "ana@example.com",
            "Ana Lojista",
            "11988887777",
            DateTimeOffset.Parse("2026-08-16T13:00:00Z"));

    private static OrderEmailNotificationRequest SampleOrder()
        => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            10582,
            "cliente@example.com",
            "Cliente Teste",
            199.90m);
}
