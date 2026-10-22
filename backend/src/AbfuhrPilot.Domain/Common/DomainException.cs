namespace AbfuhrPilot.Domain.Common;

/// <summary>
/// Thrown when a business rule / invariant is violated inside the domain layer.
/// The application layer translates this into a 409/422 API response.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
