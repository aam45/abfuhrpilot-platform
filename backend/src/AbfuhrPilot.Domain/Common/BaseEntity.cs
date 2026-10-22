namespace AbfuhrPilot.Domain.Common;

/// <summary>
/// A domain event: something that happened inside a bounded context.
/// Internal by default; selected events are mapped to integration events and
/// written to the outbox for future microservices (see Application/Events).
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
    Guid CompanyId { get; }
}

/// <summary>Convenience base for domain events (id + timestamp + tenant).</summary>
public abstract record DomainEvent(Guid CompanyId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}

/// <summary>
/// Base class for all persisted aggregate roots / entities.
/// Uses a Guid identity so IDs can be generated client-side and remain
/// stable across the future native mobile clients (offline-friendly).
/// Entities raise domain events; the DbContext converts them into outbox
/// messages in the same transaction (outbox pattern).
/// </summary>
public abstract class BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Events raised since the entity was loaded (not persisted directly).</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();

    public void Touch() => UpdatedAt = DateTime.UtcNow;
}

/// <summary>
/// Marker for entities that belong to a single company/workspace.
/// Every operational query must be scoped by <see cref="CompanyId"/> so that
/// one company can never read another company's data (multi-tenant isolation).
/// </summary>
public interface ICompanyOwned
{
    Guid CompanyId { get; set; }
}
