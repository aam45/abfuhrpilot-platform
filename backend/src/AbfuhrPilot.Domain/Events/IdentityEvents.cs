using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Domain.Events;

// IdentityContext domain events — raised by the User aggregate.
// NOTE: events never carry passwords, hashes or tokens.

public sealed record UserCreatedDomainEvent(Guid CompanyId, Guid UserId, UserRole Role)
    : DomainEvent(CompanyId);

public sealed record UserDeactivatedDomainEvent(Guid CompanyId, Guid UserId)
    : DomainEvent(CompanyId);
