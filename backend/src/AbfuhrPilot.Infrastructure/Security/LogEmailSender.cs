using System.Text.RegularExpressions;
using AbfuhrPilot.Application.Auth;
using Microsoft.Extensions.Logging;

namespace AbfuhrPilot.Infrastructure.Security;

/// <summary>
/// Dev/MVP email sender: writes the message to the application log instead of
/// sending real email. Swap for an SMTP/Postmark/SES implementation of
/// <see cref="IEmailSender"/> in production.
/// </summary>
/// <remarks>
/// Reset/verification links carry a single-use secret token in a
/// <c>token=</c> query value. That value is never logged (critical rule:
/// never log tokens, passwords, reset codes, or API keys) — <see cref="Redact"/>
/// strips it before the body reaches the logger. The raw token is only ever
/// returned to a caller through <c>Auth:ExposeTokensForDev</c>, which is
/// off in production.
/// </remarks>
public class LogEmailSender : IEmailSender
{
    private static readonly Regex TokenQueryValue =
        new(@"(token=)[^\s&]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ILogger<LogEmailSender> _logger;
    public LogEmailSender(ILogger<LogEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("[FAKE-EMAIL] To: {To} | Subject: {Subject} | Body: {Body}",
            toEmail, subject, Redact(body));
        return Task.CompletedTask;
    }

    /// <summary>Replaces any <c>token=...</c> query value with a fixed placeholder.</summary>
    internal static string Redact(string? body) =>
        TokenQueryValue.Replace(body ?? string.Empty, "$1[REDACTED]");
}
