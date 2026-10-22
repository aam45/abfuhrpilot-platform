using System.Text.Json;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using AbfuhrPilot.Domain.Events;
using AbfuhrPilot.Infrastructure.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

public class DomainEventsAndOutboxTests
{
    private readonly FakeCurrentUser _me = new();

    private Route NewStartableRoute() => new()
    {
        CompanyId = _me.CompanyId, RouteNumber = "R1", Name = "R",
        DriverId = Guid.NewGuid(), VehicleId = Guid.NewGuid(), Status = RouteStatus.Planned,
    };

    // ---- domain events ----

    [Fact]
    public void Route_start_raises_a_domain_event()
    {
        var route = NewStartableRoute();
        route.Start(route.DriverId!.Value);

        route.DomainEvents.Should().ContainSingle(e => e is RouteStartedDomainEvent);
        var ev = (RouteStartedDomainEvent)route.DomainEvents.Single();
        ev.CompanyId.Should().Be(route.CompanyId);
        ev.RouteId.Should().Be(route.Id);
    }

    [Fact]
    public void Stop_complete_raises_a_domain_event_with_completion_time()
    {
        var stop = new Stop { CompanyId = _me.CompanyId, RouteId = Guid.NewGuid(), Address = "A" };
        stop.Complete(routeStarted: true);

        var ev = stop.DomainEvents.OfType<StopCompletedDomainEvent>().Single();
        ev.StopId.Should().Be(stop.Id);
        ev.CompletedAtUtc.Should().Be(stop.CompletedAt!.Value);
    }

    [Fact]
    public void Problem_report_raises_a_domain_event_with_the_reason()
    {
        var stop = new Stop { CompanyId = _me.CompanyId, RouteId = Guid.NewGuid(), Address = "A" };
        stop.ReportProblem(ProblemType.RoadClosed);

        stop.DomainEvents.OfType<ProblemReportedDomainEvent>().Single()
            .ProblemType.Should().Be(ProblemType.RoadClosed);
    }

    // ---- outbox ----

    [Fact]
    public async Task SaveChanges_writes_an_outbox_message_for_mapped_events()
    {
        using var db = TestDb.Create();
        var route = NewStartableRoute();
        db.Routes.Add(route);
        await db.SaveChangesAsync();

        route.Start(route.DriverId!.Value);
        await db.SaveChangesAsync();

        var message = await db.OutboxMessages.SingleAsync();
        message.Type.Should().Be("abfuhrpilot.operations.route-started");
        message.Status.Should().Be(OutboxStatus.Pending);
        route.DomainEvents.Should().BeEmpty(); // cleared after collection
    }

    [Fact]
    public async Task Integration_event_payload_contains_the_company_id()
    {
        using var db = TestDb.Create();
        var route = NewStartableRoute();
        db.Routes.Add(route);
        await db.SaveChangesAsync();
        route.Start(route.DriverId!.Value);
        await db.SaveChangesAsync();

        var payload = (await db.OutboxMessages.SingleAsync()).Payload;
        using var doc = JsonDocument.Parse(payload);
        doc.RootElement.GetProperty("companyId").GetGuid().Should().Be(_me.CompanyId);
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("eventType").GetString().Should().Be("abfuhrpilot.operations.route-started");
    }

    [Fact]
    public async Task Domain_only_events_are_not_published_to_the_outbox()
    {
        using var db = TestDb.Create();
        var stop = new Stop { CompanyId = _me.CompanyId, RouteId = Guid.NewGuid(), Address = "A", SequenceNumber = 1 };
        db.Stops.Add(stop);
        await db.SaveChangesAsync();

        stop.Skip(routeStarted: true); // StopSkipped is deliberately domain-only
        await db.SaveChangesAsync();

        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Outbox_dispatcher_marks_pending_messages_processed()
    {
        using var db = TestDb.Create();
        db.OutboxMessages.Add(new OutboxMessage { Type = "abfuhrpilot.test.event", Payload = "{}" });
        await db.SaveChangesAsync();

        var dispatcher = new OutboxDispatcher(NullLogger<OutboxDispatcher>.Instance);
        var handled = await dispatcher.DispatchPendingAsync(db, default);

        handled.Should().Be(1);
        var message = await db.OutboxMessages.SingleAsync();
        message.Status.Should().Be(OutboxStatus.Processed);
        message.ProcessedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Outbox_dispatcher_retries_and_eventually_fails_a_poison_message()
    {
        using var db = TestDb.Create();
        db.OutboxMessages.Add(new OutboxMessage { Type = "abfuhrpilot.test.poison", Payload = "{}" });
        await db.SaveChangesAsync();

        var dispatcher = new ThrowingDispatcher();
        for (var i = 0; i < OutboxDispatcher.MaxRetries; i++)
            await dispatcher.DispatchPendingAsync(db, default);

        var message = await db.OutboxMessages.SingleAsync();
        message.Status.Should().Be(OutboxStatus.Failed);
        message.RetryCount.Should().Be(OutboxDispatcher.MaxRetries);
        message.Error.Should().NotBeNullOrEmpty();
    }

    private sealed class ThrowingDispatcher : OutboxDispatcher
    {
        public ThrowingDispatcher() : base(NullLogger<OutboxDispatcher>.Instance) { }
        protected override void Publish(OutboxMessage message) => throw new InvalidOperationException("broker down");
    }
}
