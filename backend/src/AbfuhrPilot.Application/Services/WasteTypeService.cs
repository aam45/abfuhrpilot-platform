using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.Application.Services;

public class WasteTypeService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _me;
    public WasteTypeService(IAppDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public async Task<PagedResult<WasteTypeDto>> ListAsync(PageQuery q, CancellationToken ct)
    {
        var query = _db.WasteTypes.Where(w => w.CompanyId == _me.CompanyId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(w => w.Name.ToLower().Contains(s) || w.Code.ToLower().Contains(s));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(w => w.Name)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(w => w.ToDto()).ToListAsync(ct);
        return new PagedResult<WasteTypeDto> { Items = items, Page = q.Page, PageSize = q.PageSize, TotalCount = total };
    }

    public async Task<WasteTypeDto> GetAsync(Guid id, CancellationToken ct) => (await Find(id, ct)).ToDto();

    public async Task<WasteTypeDto> CreateAsync(WasteTypeUpsert dto, CancellationToken ct)
    {
        var entity = new WasteType { CompanyId = _me.CompanyId };
        Apply(entity, dto);
        _db.WasteTypes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<WasteTypeDto> UpdateAsync(Guid id, WasteTypeUpsert dto, CancellationToken ct)
    {
        var entity = await Find(id, ct);
        Apply(entity, dto);
        entity.Touch();
        await _db.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await Find(id, ct);
        _db.WasteTypes.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(WasteType e, WasteTypeUpsert d)
    {
        e.Name = d.Name; e.Code = d.Code; e.Category = d.Category; e.Description = d.Description;
        e.RequiresSeparateCollection = d.RequiresSeparateCollection;
        e.RequiresSpecialVehicle = d.RequiresSpecialVehicle;
        e.DefaultDestinationId = d.DefaultDestinationId; e.IsActive = d.IsActive;
    }

    private async Task<WasteType> Find(Guid id, CancellationToken ct) =>
        await _db.WasteTypes.FirstOrDefaultAsync(w => w.Id == id && w.CompanyId == _me.CompanyId, ct)
        ?? throw new NotFoundException("Waste type not found.");
}
