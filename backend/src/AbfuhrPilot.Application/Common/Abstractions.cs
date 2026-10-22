using AbfuhrPilot.Domain.Entities;

namespace AbfuhrPilot.Application.Common;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>A signed access token together with its expiry.</summary>
public record AccessToken(string Token, DateTime ExpiresAt);

public interface IJwtTokenService
{
    /// <summary>Issues a signed JWT for the given user (optionally linked driver).</summary>
    AccessToken CreateToken(User user, Guid? driverId);
}

/// <summary>Thrown by application services when a requested entity does not exist
/// (or is not visible to the current company). Maps to HTTP 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Thrown when the caller is authenticated but not allowed. Maps to HTTP 403.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
