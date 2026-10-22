using AbfuhrPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AbfuhrPilot.Infrastructure.Outbox;

/// <summary>
/// Dispatches pending outbox messages. Today "dispatching" means structured
/// logging of the integration event (in-process); later this is the single
/// place that publishes to RabbitMQ/Kafka/Azure Service Bus. Separated from the
/// hosted service so it is unit-testable without a host.
/// </summary>
public class OutboxDispatcher
{
    public const int BatchSize = 20;
    public const int MaxRetries = 5;

    private readonly ILogger<OutboxDispatcher> _logger;
    public OutboxDispatcher(ILogger<OutboxDispatcher> logger) => _logger = logger;

    /// <summary>Processes one batch; returns the number of messages handled.</summary>
    public async Task<int> DispatchPendingAsync(AppDbContext db, CancellationToken ct)
    {
        var pending = await db.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Pending)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in pending)
        {
            try
            {
                Publish(message);
                message.Status = OutboxStatus.Processed;
                message.ProcessedAtUtc = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;
                if (message.RetryCount >= MaxRetries)
                    message.Status = OutboxStatus.Failed;
                _logger.LogError(ex, "Outbox message {MessageId} ({Type}) failed (retry {Retry}/{Max}).",
                    message.Id, message.Type, message.RetryCount, MaxRetries);
            }
        }

        if (pending.Count > 0)
            await db.SaveChangesAsync(ct);
        return pending.Count;
    }

    /// <summary>In-process publication: structured log (payload contains no secrets).
    /// Replace with a broker publisher when a message bus is introduced.</summary>
    protected virtual void Publish(OutboxMessage message)
    {
        _logger.LogInformation("Integration event dispatched (in-process): {EventType} {EventId} occurred {OccurredAt:o}",
            message.Type, message.Id, message.OccurredAtUtc);
    }
}

/// <summary>Background worker that drains the outbox on a short interval.</summary>
public class OutboxProcessor : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor started (interval {Interval}s).", Interval.TotalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
                // Drain fully in batches, then sleep.
                while (await dispatcher.DispatchPendingAsync(db, stoppingToken) >= OutboxDispatcher.BatchSize) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox processing cycle failed; will retry next interval.");
            }
            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
