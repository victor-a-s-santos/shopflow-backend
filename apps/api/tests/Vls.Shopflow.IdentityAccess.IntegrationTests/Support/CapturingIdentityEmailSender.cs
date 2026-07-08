using System.Collections.Concurrent;
using Vls.Shopflow.IdentityAccess.Application.Interfaces;

namespace Vls.Shopflow.IdentityAccess.IntegrationTests.Support;

public sealed class CapturingIdentityEmailSender : IIdentityEmailSender
{
    private readonly ConcurrentDictionary<string, string> _confirmTokens = new();
    private readonly ConcurrentDictionary<string, string> _resetTokens = new();

    public Task SendEmailConfirmationAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        _confirmTokens[email.Trim().ToUpperInvariant()] = token;
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        _resetTokens[email.Trim().ToUpperInvariant()] = token;
        return Task.CompletedTask;
    }

    public string? GetConfirmToken(string email)
        => _confirmTokens.TryGetValue(email.Trim().ToUpperInvariant(), out var t) ? t : null;

    public string? GetResetToken(string email)
        => _resetTokens.TryGetValue(email.Trim().ToUpperInvariant(), out var t) ? t : null;

    public void Clear()
    {
        _confirmTokens.Clear();
        _resetTokens.Clear();
    }
}
