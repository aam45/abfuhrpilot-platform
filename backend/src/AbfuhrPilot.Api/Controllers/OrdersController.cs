using AbfuhrPilot.Api.Idempotency;
using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Application.Services;
using AbfuhrPilot.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbfuhrPilot.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orders;
    public OrdersController(OrderService orders) => _orders = orders;

    [HttpGet]
    public Task<PagedResult<OrderDto>> List([FromQuery] PageQuery q, [FromQuery] OrderStatus? status, CancellationToken ct)
        => _orders.ListAsync(q, status, ct);

    [HttpGet("{id:guid}")]
    public Task<OrderDto> Get(Guid id, CancellationToken ct) => _orders.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = "Admin,Dispatcher"), Idempotent]
    public Task<OrderDto> Create(OrderUpsert dto, CancellationToken ct) => _orders.CreateAsync(dto, ct);

    [HttpPut("{id:guid}"), Authorize(Roles = "Admin,Dispatcher"), Idempotent]
    public Task<OrderDto> Update(Guid id, OrderUpsert dto, CancellationToken ct) => _orders.UpdateAsync(id, dto, ct);

    [HttpPost("{id:guid}/schedule"), Authorize(Roles = "Admin,Dispatcher"), Idempotent]
    public Task<OrderDto> Schedule(Guid id, ScheduleOrderRequest req, CancellationToken ct) => _orders.ScheduleAsync(id, req, ct);

    [HttpPost("{id:guid}/assign-to-route"), Authorize(Roles = "Admin,Dispatcher"), Idempotent]
    public Task<OrderDto> Assign(Guid id, AssignOrderToRouteRequest req, CancellationToken ct) => _orders.AssignToRouteAsync(id, req, ct);

    [HttpPost("{id:guid}/complete"), Idempotent]
    public Task<OrderDto> Complete(Guid id, CancellationToken ct) => _orders.CompleteAsync(id, ct);

    [HttpPost("{id:guid}/reject"), Authorize(Roles = "Admin,Dispatcher"), Idempotent]
    public Task<OrderDto> Reject(Guid id, RejectOrderRequest req, CancellationToken ct) => _orders.RejectAsync(id, req, ct);
}

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboard;
    public DashboardController(DashboardService dashboard) => _dashboard = dashboard;

    [HttpGet("operations")]
    public Task<OperationsDashboardDto> Operations(CancellationToken ct) => _dashboard.GetOperationsAsync(ct);

    [HttpGet("driver")]
    public Task<DriverDashboardDto> Driver(CancellationToken ct) => _dashboard.GetDriverAsync(ct);
}
