using System.Text.Json;
using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Events;
using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using AbfuhrPilot.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AbfuhrPilot.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly ICorrelationIdProvider? _correlation;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICorrelationIdProvider? correlation = null)
        : base(options)
    {
        _correlation = correlation;
    }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AbfuhrPilot.Infrastructure.Idempotency.IdempotencyRecord> IdempotencyRecords
        => Set<AbfuhrPilot.Infrastructure.Idempotency.IdempotencyRecord>();

    /// <summary>
    /// Outbox pattern: domain events raised by tracked entities are mapped to
    /// integration events and written as outbox rows in the SAME SaveChanges,
    /// so the business change and its event are atomic.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        CollectDomainEventsIntoOutbox();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void CollectDomainEventsIntoOutbox()
    {
        var entities = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var entity in entities)
        {
            foreach (var domainEvent in entity.DomainEvents)
            {
                var integration = IntegrationEventMapper.Map(domainEvent, _correlation?.CorrelationId);
                if (integration is null) continue; // domain-only event

                OutboxMessages.Add(new OutboxMessage
                {
                    Id = integration.EventId,
                    OccurredAtUtc = integration.OccurredAtUtc,
                    Type = integration.EventType,
                    Payload = JsonSerializer.Serialize(integration, integration.GetType(), JsonOpts),
                });
            }
            entity.ClearDomainEvents();
        }
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<WasteType> WasteTypes => Set<WasteType>();
    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<Stop> Stops => Set<Stop>();
    public DbSet<Container> Containers => Set<Container>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<ProblemReport> ProblemReports => Set<ProblemReport>();
    public DbSet<ProblemPhoto> ProblemPhotos => Set<ProblemPhoto>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Portable converter for the enum collections so the model works on both
        // PostgreSQL (production) and the EF InMemory provider (tests).
        var categoryListConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<List<WasteCategory>, string>(
            v => string.Join(',', v.Select(x => (int)x)),
            v => string.IsNullOrEmpty(v)
                ? new List<WasteCategory>()
                : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => (WasteCategory)int.Parse(x)).ToList());

        var categoryListComparer = new ValueComparer<List<WasteCategory>>(
            (a, c) => a!.SequenceEqual(c!),
            v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x.GetHashCode())),
            v => v.ToList());

        b.Entity<Company>();

        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        });

        b.Entity<Driver>(e => e.Property(x => x.FullName).HasMaxLength(200).IsRequired());

        b.Entity<Vehicle>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.LicensePlate).HasMaxLength(32).IsRequired();
            e.Property(x => x.AllowedWasteCategories).HasConversion(categoryListConverter).Metadata.SetValueComparer(categoryListComparer);
        });

        b.Entity<WasteType>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Code).HasMaxLength(32).IsRequired();
            e.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        });

        b.Entity<Destination>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.AcceptedWasteCategories).HasConversion(categoryListConverter).Metadata.SetValueComparer(categoryListComparer);
        });

        b.Entity<Route>(e =>
        {
            e.Property(x => x.RouteNumber).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasMany(x => x.Stops).WithOne().HasForeignKey(s => s.RouteId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.CompanyId, x.Date });
        });

        b.Entity<Stop>(e =>
        {
            e.Property(x => x.Address).HasMaxLength(400).IsRequired();
            e.HasIndex(x => x.RouteId);
        });

        b.Entity<Container>(e =>
        {
            e.Property(x => x.ContainerNumber).HasMaxLength(64).IsRequired();
            // Rule #5: unique container number per company.
            e.HasIndex(x => new { x.CompanyId, x.ContainerNumber }).IsUnique();
        });

        b.Entity<Order>(e =>
        {
            e.Property(x => x.OrderNumber).HasMaxLength(64).IsRequired();
            e.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.CompanyId, x.OrderNumber }).IsUnique();
        });

        b.Entity<ProblemReport>(e => e.HasIndex(x => x.StopId));

        b.Entity<ProblemPhoto>(e =>
        {
            e.Property(x => x.StorageKey).HasMaxLength(300).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            e.Property(x => x.FileName).HasMaxLength(120);
            e.HasIndex(x => x.ProblemReportId);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
        });

        b.Entity<UserToken>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => new { x.TokenHash, x.Purpose });
            e.HasIndex(x => x.UserId);
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.Property(x => x.Type).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.Status, x.OccurredAtUtc }); // processor scan
        });

        b.Entity<AbfuhrPilot.Infrastructure.Idempotency.IdempotencyRecord>(e =>
        {
            e.Property(x => x.IdempotencyKey).HasMaxLength(64).IsRequired();
            e.Property(x => x.RequestMethod).HasMaxLength(10).IsRequired();
            e.Property(x => x.RequestPath).HasMaxLength(400).IsRequired();
            e.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
            // A key is unique per company+user; the same key from another user never collides.
            e.HasIndex(x => new { x.CompanyId, x.UserId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => x.ExpiresAtUtc); // cleanup scan
        });

        // Every company-owned entity is indexed by CompanyId for tenant-scoped queries.
        foreach (var entity in b.Model.GetEntityTypes()
                     .Where(t => typeof(Domain.Common.ICompanyOwned).IsAssignableFrom(t.ClrType)))
        {
            b.Entity(entity.ClrType).HasIndex(nameof(Domain.Common.ICompanyOwned.CompanyId));
        }

        // Domain events live in memory only — never persisted on the entity itself.
        foreach (var entity in b.Model.GetEntityTypes()
                     .Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)))
        {
            b.Entity(entity.ClrType).Ignore(nameof(BaseEntity.DomainEvents));
        }
    }
}
