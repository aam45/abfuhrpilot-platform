using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.Application.Services;

public class OrderService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _me;
    public OrderService(IAppDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public async Task<PagedResult<OrderDto>> ListAsync(PageQuery q, OrderStatus? status, CancellationToken ct)
    {
        var query = _db.Orders.Where(o => o.CompanyId == _me.CompanyId);
        if (status is not null) query = query.Where(o => o.Status == status);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(o => o.OrderNumber.ToLower().Contains(s) || o.CustomerName.ToLower().Contains(s));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(o => o.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(o => o.ToDto()).ToListAsync(ct);
        return new PagedResult<OrderDto> { Items = items, Page = q.Page, PageSize = q.PageSize, TotalCount = total };
    }

    public async Task<OrderDto> GetAsync(Guid id, CancellationToken ct) => (await Find(id, ct)).ToDto();

    public async Task<OrderDto> CreateAsync(OrderUpsert dto, CancellationToken ct)
    {
        var e = new Order { CompanyId = _me.CompanyId, Status = OrderStatus.New };
        Apply(e, dto);
        _db.Orders.Add(e);
        await _db.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<OrderDto> UpdateAsync(Guid id, OrderUpsert dto, CancellationToken ct)
    {
        var e = await Find(id, ct);
        Apply(e, dto); e.Touch();
        await _db.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<OrderDto> ScheduleAsync(Guid id, ScheduleOrderRequest req, CancellationToken ct)
    {
        var e = await Find(id, ct);
        e.Schedule(req.ScheduledDate);
        await _db.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<OrderDto> AssignToRouteAsync(Guid id, AssignOrderToRouteRequest req, CancellationToken ct)
    {
        var e = await Find(id, ct);
        var route = await _db.Routes.FirstOrDefaultAsync(r => r.Id == req.RouteId && r.CompanyId == _me.CompanyId, ct)
            ?? throw new NotFoundException("Route not found.");
        e.AssignToRoute(route.Id);
        await _db.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<OrderDto> CompleteAsync(Guid id, CancellationToken ct)
    {
        var e = await Find(id, ct);
        e.Complete();
        await _db.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<OrderDto> RejectAsync(Guid id, RejectOrderRequest req, CancellationToken ct)
    {
        var e = await Find(id, ct);
        e.Reject(req.Reason);
        await _db.SaveChangesAsync(ct);
        return e.ToDto();
    }

    private static void Apply(Order e, OrderUpsert d)
    {
        e.OrderNumber = d.OrderNumber; e.OrderType = d.OrderType; e.WasteTypeId = d.WasteTypeId;
        e.PickupType = d.PickupType; e.ContainerId = d.ContainerId; e.CustomerName = d.CustomerName;
        e.CustomerPhone = d.CustomerPhone; e.Address = d.Address; e.Latitude = d.Latitude;
        e.Longitude = d.Longitude; e.RequestedDate = d.RequestedDate; e.Notes = d.Notes;
    }

    private async Task<Order> Find(Guid id, CancellationToken ct) =>
        await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.CompanyId == _me.CompanyId, ct)
        ?? throw new NotFoundException("Order not found.");
}
