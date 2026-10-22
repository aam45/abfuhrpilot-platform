using System.Text;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Application.Services;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using AbfuhrPilot.Infrastructure.Idempotency;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

public class IdempotencyTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private const string Key = "8f4a4d8e-7c27-44c9-a67b-8220815dd5e1";

    private static Stream Body(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));
    private static IdempotencyService Svc(AbfuhrPilot.Infrastructure.Persistence.AppDbContext db) =>
        new(db, NullLogger<IdempotencyService>.Instance);

    private static Task<string> Hash(string method, string path, string body = "", string? contentType = "application/json") =>
        IdempotencyService.ComputeRequestHashAsync(method, path, Body(body), contentType);

    [Fact]
    public async Task First_request_stores_and_repeat_replays_the_same_response()
    {
        using var db = TestDb.Create();
        var svc = Svc(db);
        var hash = await Hash("POST", "/api/v1/stops/x/complete");

        (await svc.LookupAsync(_companyId, _userId, Key, hash, default))
            .Outcome.Should().Be(IdempotencyOutcome.FirstRequest);

        await svc.StoreAsync(_companyId, _userId, Key, "POST", "/api/v1/stops/x/complete", hash,
            200, "{\"status\":\"Done\"}", default);

        var replay = await svc.LookupAsync(_companyId, _userId, Key, hash, default);
        replay.Outcome.Should().Be(IdempotencyOutcome.Replay);
        replay.StatusCode.Should().Be(200);
        replay.ResponseBody.Should().Be("{\"status\":\"Done\"}");
    }

    [Fact]
    public async Task Same_key_with_different_body_is_a_conflict()
    {
        using var db = TestDb.Create();
        var svc = Svc(db);
        var hash1 = await Hash("POST", "/api/v1/stops/x/report-problem", "{\"problemType\":\"RoadClosed\"}");
        await svc.StoreAsync(_companyId, _userId, Key, "POST", "/api/v1/stops/x/report-problem", hash1, 200, "{}", default);

        var hash2 = await Hash("POST", "/api/v1/stops/x/report-problem", "{\"problemType\":\"BlockedByCar\"}");
        (await svc.LookupAsync(_companyId, _userId, Key, hash2, default))
            .Outcome.Should().Be(IdempotencyOutcome.Conflict);
    }

    [Fact]
    public async Task Same_key_with_different_path_is_a_conflict()
    {
        using var db = TestDb.Create();
        var svc = Svc(db);
        var hash1 = await Hash("POST", "/api/v1/stops/aaa/complete");
        await svc.StoreAsync(_companyId, _userId, Key, "POST", "/api/v1/stops/aaa/complete", hash1, 200, "{}", default);

        var hash2 = await Hash("POST", "/api/v1/stops/bbb/complete");
        (await svc.LookupAsync(_companyId, _userId, Key, hash2, default))
            .Outcome.Should().Be(IdempotencyOutcome.Conflict);
    }

    [Fact]
    public async Task Same_key_from_a_different_user_does_not_collide()
    {
        using var db = TestDb.Create();
        var svc = Svc(db);
        var hash = await Hash("POST", "/api/v1/stops/x/complete");
        await svc.StoreAsync(_companyId, _userId, Key, "POST", "/api/v1/stops/x/complete", hash, 200, "{}", default);

        (await svc.LookupAsync(_companyId, Guid.NewGuid(), Key, hash, default))
            .Outcome.Should().Be(IdempotencyOutcome.FirstRequest);
    }

    [Fact]
    public async Task Expired_record_is_treated_as_first_request_and_cleanup_removes_it()
    {
        using var db = TestDb.Create();
        var svc = Svc(db);
        db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            CompanyId = _companyId, UserId = _userId, IdempotencyKey = Key,
            RequestMethod = "POST", RequestPath = "/p", RequestHash = "h",
            ResponseStatusCode = 200, ResponseBody = "{}",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1), // expired
        });
        await db.SaveChangesAsync();

        (await svc.LookupAsync(_companyId, _userId, Key, "h", default))
            .Outcome.Should().Be(IdempotencyOutcome.FirstRequest);

        (await IdempotencyCleanupService.CleanupAsync(db, default)).Should().Be(1);
        (await db.IdempotencyRecords.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// Pins the FALLBACK-BRANCH behavior of <see cref="IdempotencyService.ComputeRequestHashAsync"/>
    /// when no <c>hashMultipartBody</c> delegate is supplied (a path-level "multipart"
    /// marker is hashed instead of the body). This branch is NOT exercised by the real
    /// production call site anymore — <c>IdempotencyFilter</c> always passes
    /// <c>MultipartRequestHasher.ComputeAsync</c> (see
    /// <c>ProblemPhotosIdempotencyTests</c> for the real HTTP-level, content-aware
    /// behavior). This test only guards the defensive no-delegate default so a future
    /// change to it stays intentional, not documentation of current API behavior.
    /// </summary>
    [Fact]
    public async Task Multipart_hash_without_a_delegate_ignores_body_so_retried_uploads_replay()
    {
        var h1 = await IdempotencyService.ComputeRequestHashAsync(
            "POST", "/api/v1/problem-photos", Body("--boundaryA…"), "multipart/form-data; boundary=A");
        var h2 = await IdempotencyService.ComputeRequestHashAsync(
            "POST", "/api/v1/problem-photos", Body("--boundaryB…"), "multipart/form-data; boundary=B");
        h1.Should().Be(h2); // boundary noise must not break replay
    }

    /// <summary>
    /// Pins the FALLBACK-BRANCH behavior: when no <c>hashMultipartBody</c> delegate is supplied, multipart
    /// bodies hash to a path-level "multipart" marker (see
    /// <see cref="Multipart_hash_without_a_delegate_ignores_body_so_retried_uploads_replay"/>),
    /// so a same-key retry with a genuinely DIFFERENT file is indistinguishable from a
    /// legitimate same-file retry in THIS branch only. As of this fix, the real
    /// production call site (<c>IdempotencyFilter</c>) no longer takes this branch — it
    /// always supplies <c>MultipartRequestHasher.ComputeAsync</c>, which DOES reject a
    /// same-key different-file upload as a 409 Conflict (proved end-to-end by
    /// <c>ProblemPhotosIdempotencyTests.Reused_key_with_a_DIFFERENT_file_is_rejected_as_a_409_conflict</c>).
    /// This test remains only to pin the defensive fallback's behavior for any future
    /// caller that opts out of content-aware hashing, so a change to it is deliberate
    /// rather than silent drift.
    /// </summary>
    [Fact]
    public async Task Photo_upload_with_reused_key_and_different_file_via_fallback_branch_is_not_flagged_as_conflict()
    {
        using var db = TestDb.Create();
        var svc = Svc(db);
        const string path = "/api/v1/problem-photos";

        // First upload: "photo-one.jpg" content, boundary A.
        var hash1 = await IdempotencyService.ComputeRequestHashAsync(
            "POST", path, Body("--boundaryA\r\nContent: photo-one.jpg-bytes\r\n--boundaryA--"),
            "multipart/form-data; boundary=A");
        (await svc.LookupAsync(_companyId, _userId, Key, hash1, default))
            .Outcome.Should().Be(IdempotencyOutcome.FirstRequest);
        await svc.StoreAsync(_companyId, _userId, Key, "POST", path, hash1, 200,
            "{\"id\":\"photo-one-id\"}", default);

        // Second "upload" reuses the SAME key but sends a DIFFERENT file (different
        // bytes, different boundary — as a real retry with a new photo would).
        var hash2 = await IdempotencyService.ComputeRequestHashAsync(
            "POST", path, Body("--boundaryB\r\nContent: photo-TWO-completely-different-bytes\r\n--boundaryB--"),
            "multipart/form-data; boundary=B");

        var lookup = await svc.LookupAsync(_companyId, _userId, Key, hash2, default);

        // Fallback branch only (no delegate supplied): this is NOT flagged as a
        // Conflict here. It replays the ORIGINAL photo's response instead of
        // surfacing the new upload or a 409. The real production path (via
        // IdempotencyFilter + MultipartRequestHasher) DOES 409 in this scenario —
        // see ProblemPhotosIdempotencyTests for that end-to-end proof.
        lookup.Outcome.Should().Be(IdempotencyOutcome.Replay);
        lookup.ResponseBody.Should().Be("{\"id\":\"photo-one-id\"}");
    }

    [Fact]
    public async Task Retried_stop_complete_does_not_duplicate_state_or_outbox_events()
    {
        using var db = TestDb.Create();
        var me = new FakeCurrentUser();
        var route = new Route { CompanyId = me.CompanyId, RouteNumber = "R1", Name = "R",
            DriverId = Guid.NewGuid(), VehicleId = Guid.NewGuid(), Status = RouteStatus.InProgress };
        var stop = new Stop { CompanyId = me.CompanyId, RouteId = route.Id, Address = "A", SequenceNumber = 1 };
        db.AddRange(route, stop);
        await db.SaveChangesAsync();

        var stops = new StopService(db, me);
        var idem = Svc(db);
        var path = $"/api/v1/stops/{stop.Id}/complete";
        var hash = await Hash("POST", path);

        // First attempt: filter sees FirstRequest → handler executes → response stored.
        (await idem.LookupAsync(me.CompanyId, me.UserId, Key, hash, default))
            .Outcome.Should().Be(IdempotencyOutcome.FirstRequest);
        var dto = await stops.CompleteAsync(stop.Id, default);
        await idem.StoreAsync(me.CompanyId, me.UserId, Key, "POST", path, hash, 200, "{\"id\":\"" + dto.Id + "\"}", default);
        var completedAt = stop.CompletedAt;
        var outboxAfterFirst = await db.OutboxMessages.CountAsync(m => m.Type == "abfuhrpilot.operations.stop-completed");

        // Retry: filter replays the stored response — the handler NEVER runs again.
        var retry = await idem.LookupAsync(me.CompanyId, me.UserId, Key, hash, default);
        retry.Outcome.Should().Be(IdempotencyOutcome.Replay);

        stop.CompletedAt.Should().Be(completedAt); // unchanged
        (await db.OutboxMessages.CountAsync(m => m.Type == "abfuhrpilot.operations.stop-completed"))
            .Should().Be(outboxAfterFirst); // no duplicate event
        outboxAfterFirst.Should().Be(1);
    }

    [Fact]
    public async Task Retried_problem_report_does_not_duplicate_the_report()
    {
        using var db = TestDb.Create();
        var me = new FakeCurrentUser();
        var route = new Route { CompanyId = me.CompanyId, RouteNumber = "R1", Name = "R",
            DriverId = Guid.NewGuid(), VehicleId = Guid.NewGuid(), Status = RouteStatus.InProgress };
        var stop = new Stop { CompanyId = me.CompanyId, RouteId = route.Id, Address = "A", SequenceNumber = 1 };
        db.AddRange(route, stop);
        await db.SaveChangesAsync();

        var stops = new StopService(db, me);
        var idem = Svc(db);
        var path = $"/api/v1/stops/{stop.Id}/report-problem";
        var body = "{\"problemType\":\"RoadClosed\"}";
        var hash = await Hash("POST", path, body);

        await stops.ReportProblemAsync(stop.Id, new ReportProblemRequest(ProblemType.RoadClosed, null), default);
        await idem.StoreAsync(me.CompanyId, me.UserId, Key, "POST", path, hash, 200, "{}", default);

        // Retry is a Replay → ReportProblemAsync is not called again.
        (await idem.LookupAsync(me.CompanyId, me.UserId, Key, hash, default))
            .Outcome.Should().Be(IdempotencyOutcome.Replay);

        (await db.ProblemReports.CountAsync()).Should().Be(1);
        (await db.OutboxMessages.CountAsync(m => m.Type == "abfuhrpilot.operations.problem-reported")).Should().Be(1);
    }

    [Fact]
    public async Task Route_start_and_complete_are_idempotent_end_to_end()
    {
        using var db = TestDb.Create();
        var me = new FakeCurrentUser();
        var driverId = Guid.NewGuid();
        var route = new Route { CompanyId = me.CompanyId, RouteNumber = "R1", Name = "R",
            DriverId = driverId, VehicleId = Guid.NewGuid(), Status = RouteStatus.Planned };
        db.Add(route);
        await db.SaveChangesAsync();

        var routes = new RouteService(db, me);
        var idem = Svc(db);

        // ---- start ----
        var startPath = $"/api/v1/routes/{route.Id}/start";
        var startHash = await Hash("POST", startPath);

        (await idem.LookupAsync(me.CompanyId, me.UserId, Key, startHash, default))
            .Outcome.Should().Be(IdempotencyOutcome.FirstRequest);
        var startDto = await routes.StartAsync(route.Id, default);
        await idem.StoreAsync(me.CompanyId, me.UserId, Key, "POST", startPath, startHash, 200,
            "{\"id\":\"" + startDto.Id + "\"}", default);
        var actualStart = route.ActualStartTime;
        var startedOutboxAfterFirst = await db.OutboxMessages.CountAsync(m => m.Type == "abfuhrpilot.operations.route-started");

        var startRetry = await idem.LookupAsync(me.CompanyId, me.UserId, Key, startHash, default);
        startRetry.Outcome.Should().Be(IdempotencyOutcome.Replay);

        route.ActualStartTime.Should().Be(actualStart); // unchanged
        (await db.OutboxMessages.CountAsync(m => m.Type == "abfuhrpilot.operations.route-started"))
            .Should().Be(startedOutboxAfterFirst); // no duplicate event
        startedOutboxAfterFirst.Should().Be(1);

        // ---- complete (different key, since the key was already used for start) ----
        const string completeKey = "3b8f7b3a-7d4d-4a1a-9a8b-9a5d7d6a4c11";
        var completePath = $"/api/v1/routes/{route.Id}/complete";
        var completeHash = await Hash("POST", completePath);

        (await idem.LookupAsync(me.CompanyId, me.UserId, completeKey, completeHash, default))
            .Outcome.Should().Be(IdempotencyOutcome.FirstRequest);
        var completeDto = await routes.CompleteAsync(route.Id, default);
        await idem.StoreAsync(me.CompanyId, me.UserId, completeKey, "POST", completePath, completeHash, 200,
            "{\"id\":\"" + completeDto.Id + "\"}", default);
        var actualEnd = route.ActualEndTime;
        var completedOutboxAfterFirst = await db.OutboxMessages.CountAsync(m => m.Type == "abfuhrpilot.operations.route-completed");

        var completeRetry = await idem.LookupAsync(me.CompanyId, me.UserId, completeKey, completeHash, default);
        completeRetry.Outcome.Should().Be(IdempotencyOutcome.Replay);

        route.ActualEndTime.Should().Be(actualEnd); // unchanged
        (await db.OutboxMessages.CountAsync(m => m.Type == "abfuhrpilot.operations.route-completed"))
            .Should().Be(completedOutboxAfterFirst); // no duplicate event
        completedOutboxAfterFirst.Should().Be(1);
    }

    [Fact]
    public async Task Stop_start_and_skip_are_idempotent_end_to_end()
    {
        using var db = TestDb.Create();
        var me = new FakeCurrentUser();
        var route = new Route { CompanyId = me.CompanyId, RouteNumber = "R1", Name = "R",
            DriverId = Guid.NewGuid(), VehicleId = Guid.NewGuid(), Status = RouteStatus.InProgress };
        var stop1 = new Stop { CompanyId = me.CompanyId, RouteId = route.Id, Address = "A", SequenceNumber = 1 };
        var stop2 = new Stop { CompanyId = me.CompanyId, RouteId = route.Id, Address = "B", SequenceNumber = 2 };
        db.AddRange(route, stop1, stop2);
        await db.SaveChangesAsync();

        var stops = new StopService(db, me);
        var idem = Svc(db);

        // ---- start (stop1) ----
        var startPath = $"/api/v1/stops/{stop1.Id}/start";
        var startHash = await Hash("POST", startPath);

        (await idem.LookupAsync(me.CompanyId, me.UserId, Key, startHash, default))
            .Outcome.Should().Be(IdempotencyOutcome.FirstRequest);
        await stops.StartAsync(stop1.Id, default);
        await idem.StoreAsync(me.CompanyId, me.UserId, Key, "POST", startPath, startHash, 200,
            "{\"id\":\"" + stop1.Id + "\"}", default);
        var actualArrival = stop1.ActualArrivalTime;
        var stopStatusAfterStart = stop1.Status;

        var startRetry = await idem.LookupAsync(me.CompanyId, me.UserId, Key, startHash, default);
        startRetry.Outcome.Should().Be(IdempotencyOutcome.Replay);

        stop1.ActualArrivalTime.Should().Be(actualArrival); // unchanged
        stop1.Status.Should().Be(stopStatusAfterStart); // unchanged

        // ---- skip (stop2, different key) ----
        const string skipKey = "5c9a1e3f-2b6d-4f88-9a2c-6f1d4b8e7a22";
        var skipPath = $"/api/v1/stops/{stop2.Id}/skip";
        var skipHash = await Hash("POST", skipPath);

        (await idem.LookupAsync(me.CompanyId, me.UserId, skipKey, skipHash, default))
            .Outcome.Should().Be(IdempotencyOutcome.FirstRequest);
        await stops.SkipAsync(stop2.Id, default);
        await idem.StoreAsync(me.CompanyId, me.UserId, skipKey, "POST", skipPath, skipHash, 200,
            "{\"id\":\"" + stop2.Id + "\"}", default);
        stop2.Status.Should().Be(StopStatus.Skipped);

        var skipRetry = await idem.LookupAsync(me.CompanyId, me.UserId, skipKey, skipHash, default);
        skipRetry.Outcome.Should().Be(IdempotencyOutcome.Replay);

        stop2.Status.Should().Be(StopStatus.Skipped); // unchanged, not re-processed
    }
}
