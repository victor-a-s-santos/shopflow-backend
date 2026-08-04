using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vls.Shopflow.Notifications.Application.Interfaces;
using Vls.Shopflow.Notifications.Application.Models;
using Vls.Shopflow.Notifications.Application.Options;

namespace Vls.Shopflow.Notifications.Infrastructure.Services;

public sealed class BrevoTransactionalEmailSender(
    HttpClient httpClient,
    IOptions<BrevoOptions> options,
    ILogger<BrevoTransactionalEmailSender> logger) : ITransactionalEmailSender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<TransactionalEmailSendResult> SendAsync(
        TransactionalEmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return new TransactionalEmailSendResult(false, null, "Brevo disabled.", IsTransientFailure: false);
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.SenderEmail))
        {
            return new TransactionalEmailSendResult(
                false, null, "Brevo ApiKey/SenderEmail not configured.", IsTransientFailure: false);
        }

        var payload = new BrevoSendRequest
        {
            Sender = new BrevoSender { Name = settings.SenderName, Email = settings.SenderEmail },
            To = [new BrevoTo { Email = message.ToEmail, Name = message.ToName }],
            Subject = message.Subject,
            HtmlContent = message.HtmlContent,
            TextContent = message.TextContent,
            ReplyTo = string.IsNullOrWhiteSpace(settings.ReplyToEmail)
                ? null
                : new BrevoSender { Email = settings.ReplyToEmail }
        };

        if (settings.SandboxMode)
        {
            logger.LogInformation(
                "Brevo SandboxMode=true — sending transactional email (use a Brevo test sender/domain in non-prod)");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email");
        request.Headers.TryAddWithoutValidation("api-key", settings.ApiKey);
        request.Content = JsonContent.Create(payload, options: JsonOptions);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Brevo HTTP transport failure for recipient domain send");
            return new TransactionalEmailSendResult(false, null, ex.Message, IsTransientFailure: true);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            string? messageId = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("messageId", out var idEl))
                    messageId = idEl.GetString();
            }
            catch (JsonException)
            {
                // ignore parse issues — send succeeded
            }

            logger.LogInformation("Brevo accepted transactional email ProviderMessageId={ProviderMessageId}", messageId);
            return new TransactionalEmailSendResult(true, messageId, null, IsTransientFailure: false);
        }

        var status = (int)response.StatusCode;
        var transient = status is >= 500 or 429;
        logger.LogWarning(
            "Brevo send failed StatusCode={StatusCode} Transient={Transient}",
            status,
            transient);

        return new TransactionalEmailSendResult(
            false,
            null,
            $"Brevo HTTP {status}",
            IsTransientFailure: transient);
    }

    private sealed class BrevoSendRequest
    {
        public BrevoSender Sender { get; set; } = default!;
        public List<BrevoTo> To { get; set; } = [];
        public string Subject { get; set; } = default!;
        public string HtmlContent { get; set; } = default!;
        public string? TextContent { get; set; }
        public BrevoSender? ReplyTo { get; set; }
    }

    private sealed class BrevoSender
    {
        public string? Name { get; set; }
        public string Email { get; set; } = default!;
    }

    private sealed class BrevoTo
    {
        public string Email { get; set; } = default!;
        public string? Name { get; set; }
    }
}
