using AbfuhrPilot.Api.Idempotency;
using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Application.Services;
using AbfuhrPilot.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbfuhrPilot.Api.Controllers;

[ApiController]
[Route("api/v1/routes")]
[Authorize]
public class RoutesController : ControllerBase
{
    private readonly RouteService _routes;
    public RoutesController(RouteService routes) => _routes = routes;

    [HttpGet]
    public Task<PagedResult<RouteDto>> List([FromQuery] PageQuery q, [FromQuery] DateOnly? date,
        [FromQuery] RouteStatus? status, CancellationToken ct) => _routes.ListAsync(q, date, status, ct);

    [HttpGet("{id:guid}")]
    public Task<RouteDto> Get(Guid id, CancellationToken ct) => _routes.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = "Admin,Dispatcher"), Idempotent]
    public Task<RouteDto> Create(RouteUpsert dto, CancellationToken ct) => _routes.CreateAsync(dto, ct);

    [HttpPut("{id:guid}"), Authorize(Roles = "Admin,Dispatcher"), Idempotent]
    public Task<RouteDto> Update(Guid id, RouteUpsert dto, CancellationToken ct) => _routes.UpdateAsync(id, dto, ct);

    [HttpPost("{id:guid}/assign"), Authorize(Roles = "Admin,Dispatcher"), Idempotent]
    public Task<RouteDto> Assign(Guid id, AssignRouteRequest req, CancellationToken ct) => _routes.AssignAsync(id, req, ct);

    // Driver workflow: key REQUIRED — the native app replays these offline.
    [HttpPost("{id:guid}/start"), Idempotent(Required = true)]
    public Task<RouteDto> Start(Guid id, CancellationToken ct) => _routes.StartAsync(id, ct);

    [HttpPost("{id:guid}/complete"), Idempotent(Required = true)]
    public Task<RouteDto> Complete(Guid id, CancellationToken ct) => _routes.CompleteAsync(id, ct);

    // ---- Stops nested under a route ----
    [HttpGet("{id:guid}/stops")]
    public Task<List<StopDto>> ListStops(Guid id, CancellationToken ct) => _routes.ListStopsAsync(id, ct);

    [HttpPost("{id:guid}/stops"), Authorize(Roles = "Admin,Dispatcher"), Idempotent]
    public Task<StopDto> AddStop(Guid id, StopUpsert dto, CancellationToken ct) => _routes.AddStopAsync(id, dto, ct);

    // Drivers may not reorder — only dispatchers/admins change planning data.
    [HttpPost("{id:guid}/stops/reorder"), Authorize(Roles = "Admin,Dispatcher"), Idempotent]
    public Task<List<StopDto>> ReorderStops(Guid id, ReorderStopsRequest req, CancellationToken ct) =>
        _routes.ReorderStopsAsync(id, req.StopIds, ct);
}

[ApiController]
[Route("api/v1/stops")]
[Authorize]
public class StopsController : ControllerBase
{
    private readonly StopService _stops;
    public StopsController(StopService stops) => _stops = stops;

    [HttpPut("{id:guid}"), Authorize(Roles = "Admin,Dispatcher"), Idempotent]
    public Task<StopDto> Update(Guid id, StopUpsert dto, CancellationToken ct) => _stops.UpdateAsync(id, dto, ct);

    // Driver workflow: key REQUIRED — offline replay must never double-complete
    // a stop or duplicate a problem report / outbox event.
    [HttpPost("{id:guid}/start"), Idempotent(Required = true)]
    public Task<StopDto> Start(Guid id, CancellationToken ct) => _stops.StartAsync(id, ct);

    [HttpPost("{id:guid}/complete"), Idempotent(Required = true)]
    public Task<StopDto> Complete(Guid id, CancellationToken ct) => _stops.CompleteAsync(id, ct);

    [HttpPost("{id:guid}/skip"), Idempotent(Required = true)]
    public Task<StopDto> Skip(Guid id, CancellationToken ct) => _stops.SkipAsync(id, ct);

    [HttpPost("{id:guid}/report-problem"), Idempotent(Required = true)]
    public Task<StopDto> ReportProblem(Guid id, ReportProblemRequest req, CancellationToken ct) => _stops.ReportProblemAsync(id, req, ct);
}
