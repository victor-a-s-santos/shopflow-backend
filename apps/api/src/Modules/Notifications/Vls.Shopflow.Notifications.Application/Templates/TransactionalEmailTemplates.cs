using System.Globalization;
using System.Net;
using System.Text;
using Vls.Shopflow.Notifications.Application.Interfaces;
using Vls.Shopflow.Notifications.Application.Options;

namespace Vls.Shopflow.Notifications.Application.Templates;

public static class TransactionalEmailTemplates
{
    public static (string Subject, string Html, string Text) ConfirmEmail(
        PublicAppOptions app,
        string email,
        string fullName,
        string token)
    {
        var link = $"{TrimBase(app.BaseUrl)}/confirm-email?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
        var subject = "Confirme seu cadastro";
        var greeting = Greeting(fullName);
        var html = Layout(
            app.StoreName,
            subject,
            $"""
            <p>{greeting}</p>
            <p>Obrigado por se cadastrar. Confirme seu e-mail para ativar sua conta:</p>
            <p style="margin:24px 0"><a href="{Escape(link)}" style="background:#111;color:#fff;padding:12px 18px;text-decoration:none;border-radius:4px">Confirmar e-mail</a></p>
            <p style="color:#666;font-size:13px">Se o botão não funcionar, copie e cole este link:<br/>{Escape(link)}</p>
            """);
        var text = $"{greeting}\n\nConfirme seu e-mail: {link}\n";
        return (subject, html, text);
    }

    public static (string Subject, string Html, string Text) ResetPassword(
        PublicAppOptions app,
        string email,
        string? fullName,
        string token)
    {
        var link = $"{TrimBase(app.BaseUrl)}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
        var subject = "Redefinição de senha";
        var greeting = Greeting(fullName);
        var html = Layout(
            app.StoreName,
            subject,
            $"""
            <p>{greeting}</p>
            <p>Recebemos um pedido para redefinir sua senha. Use o botão abaixo:</p>
            <p style="margin:24px 0"><a href="{Escape(link)}" style="background:#111;color:#fff;padding:12px 18px;text-decoration:none;border-radius:4px">Redefinir senha</a></p>
            <p style="color:#666;font-size:13px">Se você não solicitou, ignore este e-mail.</p>
            <p style="color:#666;font-size:13px">Link: {Escape(link)}</p>
            """);
        var text = $"{greeting}\n\nRedefina sua senha: {link}\n";
        return (subject, html, text);
    }

    public static (string Subject, string Html, string Text) OrderCreated(
        PublicAppOptions app,
        OrderEmailNotificationRequest order)
    {
        var subject = $"Recebemos seu pedido #{order.OrderNumber}";
        var link = BuildOrderLink(app, order);
        var greeting = Greeting(order.CustomerName);
        var html = Layout(
            app.StoreName,
            subject,
            $"""
            <p>{greeting}</p>
            <p>Recebemos seu pedido <strong>#{order.OrderNumber}</strong>.</p>
            <p>Status: <strong>Aguardando pagamento</strong> (Pix).</p>
            <p>Total: <strong>{FormatBrl(order.Total)}</strong></p>
            {Cta(link, "Acompanhar pedido")}
            {LinkFallback(link)}
            """);
        var text = $"{greeting}\n\nPedido #{order.OrderNumber}\nTotal: {FormatBrl(order.Total)}\nAguardando pagamento.\n{link}\n";
        return (subject, html, text);
    }

    public static (string Subject, string Html, string Text) PaymentConfirmed(
        PublicAppOptions app,
        OrderEmailNotificationRequest order)
    {
        var subject = $"Pagamento confirmado — Pedido #{order.OrderNumber}";
        var link = BuildOrderLink(app, order);
        var greeting = Greeting(order.CustomerName);
        var delivery = BuildDeliveryPreferenceLine(order);
        var html = Layout(
            app.StoreName,
            subject,
            $"""
            <p>{greeting}</p>
            <p>Pagamento confirmado para o pedido <strong>#{order.OrderNumber}</strong>.</p>
            <p>Total: <strong>{FormatBrl(order.Total)}</strong></p>
            {delivery}
            {Cta(link, "Ver pedido")}
            {LinkFallback(link)}
            """);
        var text = $"{greeting}\n\nPagamento confirmado — Pedido #{order.OrderNumber}\nTotal: {FormatBrl(order.Total)}\n{link}\n";
        return (subject, html, text);
    }

    public static (string Subject, string Html, string Text) OrderShipped(
        PublicAppOptions app,
        OrderEmailNotificationRequest order)
    {
        var subject = $"Seu pedido #{order.OrderNumber} foi enviado";
        var link = BuildOrderLink(app, order);
        var greeting = Greeting(order.CustomerName);
        var method = string.IsNullOrWhiteSpace(order.FinalDeliveryMethod)
            ? null
            : $"<p>Método: <strong>{Escape(order.FinalDeliveryMethod)}</strong></p>";
        var tracking = string.IsNullOrWhiteSpace(order.TrackingCode)
            ? null
            : $"<p>Código de rastreio / referência: <strong>{Escape(order.TrackingCode)}</strong></p>";
        var html = Layout(
            app.StoreName,
            subject,
            $"""
            <p>{greeting}</p>
            <p>Seu pedido <strong>#{order.OrderNumber}</strong> foi enviado.</p>
            {method}
            {tracking}
            {Cta(link, "Acompanhar pedido")}
            {LinkFallback(link)}
            """);
        var text = new StringBuilder()
            .AppendLine(greeting)
            .AppendLine()
            .AppendLine($"Pedido #{order.OrderNumber} enviado.")
            .AppendLine(string.IsNullOrWhiteSpace(order.TrackingCode) ? "" : $"Rastreio: {order.TrackingCode}")
            .AppendLine(link)
            .ToString();
        return (subject, html, text);
    }

    public static (string Subject, string Html, string Text) OrderDelivered(
        PublicAppOptions app,
        OrderEmailNotificationRequest order)
    {
        var subject = $"Seu pedido #{order.OrderNumber} foi entregue";
        var link = BuildOrderLink(app, order);
        var greeting = Greeting(order.CustomerName);
        var html = Layout(
            app.StoreName,
            subject,
            $"""
            <p>{greeting}</p>
            <p>Seu pedido <strong>#{order.OrderNumber}</strong> foi entregue.</p>
            <p>Obrigado pela preferência!</p>
            {Cta(link, "Ver pedido")}
            {LinkFallback(link)}
            """);
        var text = $"{greeting}\n\nPedido #{order.OrderNumber} entregue.\n{link}\n";
        return (subject, html, text);
    }

    public static string BuildOrderLink(PublicAppOptions app, OrderEmailNotificationRequest order)
    {
        var baseUrl = TrimBase(app.BaseUrl);
        if (order.CustomerUserId is not null)
            return $"{baseUrl}/account/orders/{order.OrderId:D}";

        // Guest: public tracking by order number. Token in query for email deep-link
        // (FE hydrates ?t= per EMAIL-002; never log the token).
        var path = $"{baseUrl}/pedido/{Uri.EscapeDataString(order.OrderNumber.ToString(CultureInfo.InvariantCulture))}";
        if (string.IsNullOrWhiteSpace(order.GuestAccessToken))
            return path;

        return $"{path}?t={Uri.EscapeDataString(order.GuestAccessToken)}";
    }

    private static string BuildDeliveryPreferenceLine(OrderEmailNotificationRequest order)
    {
        if (string.IsNullOrWhiteSpace(order.PreferredDeliveryMethod) && order.PreferredDeliveryDate is null)
            return "";

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(order.PreferredDeliveryMethod))
            parts.Add(Escape(order.PreferredDeliveryMethod));
        if (order.PreferredDeliveryDate is { } d)
            parts.Add(d.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR")));

        return $"<p>Preferência de entrega: <strong>{string.Join(" — ", parts)}</strong></p>";
    }

    private static string Greeting(string? name)
    {
        var n = string.IsNullOrWhiteSpace(name) ? null : name.Trim().Split(' ', 2)[0];
        return n is null ? "Olá," : $"Olá, {Escape(n)},";
    }

    private static string FormatBrl(decimal value)
        => value.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));

    private static string Cta(string link, string label)
        => string.IsNullOrWhiteSpace(link)
            ? ""
            : $"""<p style="margin:24px 0"><a href="{Escape(link)}" style="background:#111;color:#fff;padding:12px 18px;text-decoration:none;border-radius:4px">{Escape(label)}</a></p>""";

    private static string LinkFallback(string link)
        => string.IsNullOrWhiteSpace(link)
            ? ""
            : $"""<p style="color:#666;font-size:13px">Link: {Escape(link)}</p>""";

    private static string Layout(string storeName, string title, string body)
        => $"""
           <!DOCTYPE html>
           <html lang="pt-BR"><body style="font-family:Arial,sans-serif;color:#222;line-height:1.5;max-width:560px;margin:0 auto;padding:24px">
           <h1 style="font-size:20px;margin:0 0 8px">{Escape(storeName)}</h1>
           <h2 style="font-size:16px;font-weight:600;margin:0 0 20px">{Escape(title)}</h2>
           {body}
           <hr style="border:none;border-top:1px solid #eee;margin:28px 0"/>
           <p style="color:#888;font-size:12px">Este é um e-mail transacional automático. Não responda a esta mensagem se o endereço for no-reply.</p>
           </body></html>
           """;

    private static string TrimBase(string baseUrl) => (baseUrl ?? "").Trim().TrimEnd('/');

    private static string Escape(string? value) => WebUtility.HtmlEncode(value ?? "");
}
