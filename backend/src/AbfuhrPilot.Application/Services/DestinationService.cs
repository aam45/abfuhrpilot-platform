using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.Application.Services;

public class DestinationService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _me;
    public DestinationService(IAppDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public async Task<PagedResult<DestinationDto>> ListAsync(PageQuery q, CancellationToken ct)
    {
        var query = _db.Destinations.Where(d => d.CompanyId == _me.CompanyId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(d => d.Name.ToLower().Contains(s));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(d => d.Name)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
        return new PagedResult<DestinationDto> { Items = items.Select(d => d.ToDto()).ToList(), Page = q.Page, PageSize = q.PageSize, TotalCount = total };
    }

    public async Task<DestinationDto> GetAsync(Guid id, CancellationToken ct) => (await Find(id, ct)).ToDto();

    public async Task<DestinationDto> CreateAsync(DestinationUpsert dto, CancellationToken ct)
    {
        var e = new Destination { CompanyId = _me.CompanyId };
        Apply(e, dto);
        _db.Destinations.Add(e);
        await _db.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<DestinationDto> UpdateAsync(Guid id, DestinationUpsert dto, CancellationToken ct)
    {
        var e = await Find(id, ct);
        Apply(e, dto); e.Touch();
        await _db.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var e = await Find(id, ct);
        _db.Destinations.Remove(e);
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(Destination e, DestinationUpsert d)
    {
        e.Name = d.Name; e.Type = d.Type; e.Address = d.Address; e.Latitude = d.Latitude;
        e.Longitude = d.Longitude; e.OpeningHours = d.OpeningHours;
        e.AcceptedWasteCategories = d.AcceptedWasteCategories ?? new(); e.IsActive = d.IsActive;
    }

    private async Task<Destination> Find(Guid id, CancellationToken ct) =>
        await _db.Destinations.FirstOrDefaultAsync(d => d.Id == id && d.CompanyId == _me.CompanyId, ct)
        ?? throw new NotFoundException("Destination not found.");
}
