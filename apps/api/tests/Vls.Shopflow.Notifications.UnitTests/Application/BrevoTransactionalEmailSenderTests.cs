using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vls.Shopflow.Notifications.Application.Models;
using Vls.Shopflow.Notifications.Application.Options;
using Vls.Shopflow.Notifications.Infrastructure.Services;

namespace Vls.Shopflow.Notifications.UnitTests.Application;

public sealed class BrevoTransactionalEmailSenderTests
{
    [Fact]
    public async Task SendAsync_AddsApiKeyAndSandboxHeader_WithoutLoggingKey()
    {
        var handler = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"messageId":"abc-123"}""")
            }
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") };
        var logger = new CollectingLogger();
        var sut = new BrevoTransactionalEmailSender(
            http,
            Options.Create(new BrevoOptions
            {
                Enabled = true,
                ApiKey = "secret-api-key-value",
                SenderName = "VIP",
                SenderEmail = "no-reply@test.com",
                ReplyToName = "Atendimento",
                ReplyToEmail = "atendimento@test.com",
                SandboxMode = true
            }),
            logger);

        var result = await sut.SendAsync(
            new TransactionalEmailMessage("ana@c.com", "Ana", "Assunto", "<p>oi</p>", "oi"));

        result.Succeeded.Should().BeTrue();
        result.ProviderMessageId.Should().Be("abc-123");
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.TryGetValues("api-key", out var apiKey).Should().BeTrue();
        apiKey!.Single().Should().Be("secret-api-key-value");
        handler.LastRequest.Headers.TryGetValues("X-Sib-Sandbox", out var sandbox).Should().BeTrue();
        sandbox!.Single().Should().Be("drop");
        logger.Messages.Should().NotContain(m => m.Contains("secret-api-key-value", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendAsync_DoesNotAddSandboxHeader_WhenSandboxOff()
    {
        var handler = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"messageId":"x"}""")
            }
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") };
        var sut = new BrevoTransactionalEmailSender(
            http,
            Options.Create(new BrevoOptions
            {
                Enabled = true,
                ApiKey = "k",
                SenderEmail = "no-reply@test.com",
                SandboxMode = false
            }),
            NullLogger<BrevoTransactionalEmailSender>.Instance);

        await sut.SendAsync(new TransactionalEmailMessage("ana@c.com", "Ana", "Assunto", "<p>oi</p>"));

        handler.LastRequest!.Headers.Contains("X-Sib-Sandbox").Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_Treats5xxAsTransient()
    {
        var handler = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("fail")
            }
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") };
        var sut = new BrevoTransactionalEmailSender(
            http,
            Options.Create(new BrevoOptions
            {
                Enabled = true,
                ApiKey = "k",
                SenderEmail = "no-reply@test.com"
            }),
            NullLogger<BrevoTransactionalEmailSender>.Instance);

        var result = await sut.SendAsync(new TransactionalEmailMessage("ana@c.com", "Ana", "Assunto", "<p>oi</p>"));

        result.Succeeded.Should().BeFalse();
        result.IsTransientFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain("500");
    }

    [Fact]
    public async Task SendAsync_Treats4xxAsPermanent()
    {
        var handler = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("no")
            }
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") };
        var sut = new BrevoTransactionalEmailSender(
            http,
            Options.Create(new BrevoOptions
            {
                Enabled = true,
                ApiKey = "k",
                SenderEmail = "no-reply@test.com"
            }),
            NullLogger<BrevoTransactionalEmailSender>.Instance);

        var result = await sut.SendAsync(new TransactionalEmailMessage("ana@c.com", "Ana", "Assunto", "<p>oi</p>"));

        result.Succeeded.Should().BeFalse();
        result.IsTransientFailure.Should().BeFalse();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public required HttpResponseMessage Response { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Response);
        }
    }

    private sealed class CollectingLogger : Microsoft.Extensions.Logging.ILogger<BrevoTransactionalEmailSender>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
