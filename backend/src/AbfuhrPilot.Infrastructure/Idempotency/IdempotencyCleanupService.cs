using AbfuhrPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AbfuhrPilot.Infrastructure.Idempotency;

/// <summary>Hourly background cleanup of expired idempotency records.</summary>
public class IdempotencyCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdempotencyCleanupService> _logger;

    public IdempotencyCleanupService(IServiceScopeFactory scopeFactory, ILogger<IdempotencyCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var removed = await CleanupAsync(db, stoppingToken);
                if (removed > 0)
                    _logger.LogInformation("Idempotency cleanup removed {Count} expired record(s).", removed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Idempotency cleanup cycle failed; will retry next interval.");
            }
            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Deletes expired records; extracted for unit testing.</summary>
    public static async Task<int> CleanupAsync(AppDbContext db, CancellationToken ct)
    {
        var expired = await db.IdempotencyRecords
            .Where(r => r.ExpiresAtUtc <= DateTime.UtcNow)
            .ToListAsync(ct);
        if (expired.Count == 0) return 0;
        db.IdempotencyRecords.RemoveRange(expired);
        await db.SaveChangesAsync(ct);
        return expired.Count;
    }
}
