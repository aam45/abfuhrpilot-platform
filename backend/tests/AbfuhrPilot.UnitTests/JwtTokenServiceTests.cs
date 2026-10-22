using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using AbfuhrPilot.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

public class JwtTokenServiceTests
{
    [Fact]
    public void Access_token_is_short_lived_per_configuration()
    {
        var svc = new JwtTokenService(new JwtOptions
        {
            SecretKey = new string('k', 40),
            AccessTokenMinutes = 45,
        });

        var user = new User { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), Email = "a@b.de", Role = UserRole.Admin };
        var token = svc.CreateToken(user, null);

        token.Token.Should().NotBeNullOrEmpty();
        token.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(45), TimeSpan.FromMinutes(1));
        token.ExpiresAt.Should().BeBefore(DateTime.UtcNow.AddHours(2)); // no longer a 12h token
    }
}
