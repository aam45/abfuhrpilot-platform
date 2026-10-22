using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.Application.Services;

public class VehicleService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _me;
    public VehicleService(IAppDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public async Task<PagedResult<VehicleDto>> ListAsync(PageQuery q, CancellationToken ct)
    {
        var query = _db.Vehicles.Where(v => v.CompanyId == _me.CompanyId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(v => v.Name.ToLower().Contains(s) || v.LicensePlate.ToLower().Contains(s));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(v => v.Name)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
        return new PagedResult<VehicleDto> { Items = items.Select(v => v.ToDto()).ToList(), Page = q.Page, PageSize = q.PageSize, TotalCount = total };
    }

    public async Task<VehicleDto> GetAsync(Guid id, CancellationToken ct) => (await Find(id, ct)).ToDto();

    public async Task<VehicleDto> CreateAsync(VehicleUpsert dto, CancellationToken ct)
    {
        var e = new Vehicle { CompanyId = _me.CompanyId };
        Apply(e, dto);
        _db.Vehicles.Add(e);
        await _db.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<VehicleDto> UpdateAsync(Guid id, VehicleUpsert dto, CancellationToken ct)
    {
        var e = await Find(id, ct);
        Apply(e, dto); e.Touch();
        await _db.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var e = await Find(id, ct);
        _db.Vehicles.Remove(e);
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(Vehicle e, VehicleUpsert d)
    {
        e.Name = d.Name; e.LicensePlate = d.LicensePlate; e.VehicleType = d.VehicleType;
        e.Capacity = d.Capacity; e.HeightMeters = d.HeightMeters; e.WidthMeters = d.WidthMeters;
        e.LengthMeters = d.LengthMeters; e.WeightTons = d.WeightTons; e.MaxLoadTons = d.MaxLoadTons;
        e.AllowedWasteCategories = d.AllowedWasteCategories ?? new(); e.IsActive = d.IsActive;
    }

    private async Task<Vehicle> Find(Guid id, CancellationToken ct) =>
        await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id && v.CompanyId == _me.CompanyId, ct)
        ?? throw new NotFoundException("Vehicle not found.");
}
