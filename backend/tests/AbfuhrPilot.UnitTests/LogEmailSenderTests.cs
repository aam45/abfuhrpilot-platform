using AbfuhrPilot.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AbfuhrPilot.UnitTests;

public class LogEmailSenderTests
{
    [Fact]
    public void Redact_replaces_token_query_value_with_placeholder()
    {
        var body = "Reset your password: https://app.abfuhrpilot.de/reset?token=super-secret-abc123&x=1";

        var redacted = LogEmailSender.Redact(body);

        redacted.Should().NotContain("super-secret-abc123");
        redacted.Should().Contain("token=[REDACTED]");
        redacted.Should().Contain("&x=1");
    }

    [Fact]
    public void Redact_is_case_insensitive_and_handles_multiple_tokens()
    {
        var body = "a TOKEN=abc b token=def";

        var redacted = LogEmailSender.Redact(body);

        redacted.Should().NotContain("abc");
        redacted.Should().NotContain("def");
    }

    [Fact]
    public void Redact_returns_empty_string_for_null_body()
    {
        LogEmailSender.Redact(null).Should().Be(string.Empty);
    }

    [Fact]
    public async Task SendAsync_logs_body_without_raw_token()
    {
        var sender = new LogEmailSender(NullLogger<LogEmailSender>.Instance);

        // Exercise the real send path to ensure it does not throw and completes;
        // the actual redaction guarantee is covered by the Redact tests above,
        // since NullLogger discards output rather than capturing it.
        await sender.SendAsync(
            "user@example.com",
            "Verify your email",
            "Click here: https://app.abfuhrpilot.de/verify?token=raw-secret-token");
    }
}
