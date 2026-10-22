using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.Application.Services;

public class DriverService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _me;
    public DriverService(IAppDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public async Task<PagedResult<DriverDto>> ListAsync(PageQuery q, CancellationToken ct)
    {
        var query = _db.Drivers.Where(d => d.CompanyId == _me.CompanyId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(d => d.FullName.ToLower().Contains(s));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(d => d.FullName)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(d => d.ToDto()).ToListAsync(ct);
        return new PagedResult<DriverDto> { Items = items, Page = q.Page, PageSize = q.PageSize, TotalCount = total };
    }

    public async Task<DriverDto> GetAsync(Guid id, CancellationToken ct) => (await Find(id, ct)).ToDto();

    public async Task<DriverDto> CreateAsync(DriverUpsert dto, CancellationToken ct)
    {
        var e = new Driver { CompanyId = _me.CompanyId };
        Apply(e, dto);
        _db.Drivers.Add(e);
        await _db.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<DriverDto> UpdateAsync(Guid id, DriverUpsert dto, CancellationToken ct)
    {
        var e = await Find(id, ct);
        Apply(e, dto); e.Touch();
        await _db.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var e = await Find(id, ct);
        _db.Drivers.Remove(e);
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(Driver e, DriverUpsert d)
    {
        e.FullName = d.FullName; e.PhoneNumber = d.PhoneNumber;
        e.LicenseNumber = d.LicenseNumber; e.UserId = d.UserId; e.IsActive = d.IsActive;
    }

    private async Task<Driver> Find(Guid id, CancellationToken ct) =>
        await _db.Drivers.FirstOrDefaultAsync(d => d.Id == id && d.CompanyId == _me.CompanyId, ct)
        ?? throw new NotFoundException("Driver not found.");
}
