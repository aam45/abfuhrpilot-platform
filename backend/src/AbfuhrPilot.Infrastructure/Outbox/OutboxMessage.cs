namespace AbfuhrPilot.Infrastructure.Outbox;

/// <summary>
/// Transactional outbox row. Written in the same transaction as the business
/// change (from domain events via the DbContext), then processed by the hosted
/// <see cref="OutboxProcessor"/>. Today processing dispatches in-process (log +
/// internal handlers); later the same table feeds RabbitMQ/Kafka/Service Bus.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Stable integration event type, e.g. "abfuhrpilot.operations.route-started".</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>Serialized integration event (JSON). Never contains secrets.</summary>
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = OutboxStatus.Pending;
    public DateTime? ProcessedAtUtc { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
}

public static class OutboxStatus
{
    public const string Pending = "Pending";
    public const string Processed = "Processed";
    public const string Failed = "Failed";
}
