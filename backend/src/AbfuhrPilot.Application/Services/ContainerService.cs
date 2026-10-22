using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.Application.Services;

public class ContainerService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _me;
    public ContainerService(IAppDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public async Task<PagedResult<ContainerDto>> ListAsync(PageQuery q, CancellationToken ct)
    {
        var query = _db.Containers.Where(c => c.CompanyId == _me.CompanyId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(c => c.ContainerNumber.ToLower().Contains(s));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.ContainerNumber)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(c => c.ToDto()).ToListAsync(ct);
        return new PagedResult<ContainerDto> { Items = items, Page = q.Page, PageSize = q.PageSize, TotalCount = total };
    }

    public async Task<ContainerDto> GetAsync(Guid id, CancellationToken ct) => (await Find(id, ct)).ToDto();

    public async Task<ContainerDto> CreateAsync(ContainerUpsert dto, CancellationToken ct)
    {
        await EnsureUniqueNumber(dto.ContainerNumber, null, ct);
        var e = new Container { CompanyId = _me.CompanyId };
        Apply(e, dto);
        _db.Containers.Add(e);
        await _db.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task<ContainerDto> UpdateAsync(Guid id, ContainerUpsert dto, CancellationToken ct)
    {
        var e = await Find(id, ct);
        await EnsureUniqueNumber(dto.ContainerNumber, id, ct);
        Apply(e, dto); e.Touch();
        await _db.SaveChangesAsync(ct);
        return e.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var e = await Find(id, ct);
        _db.Containers.Remove(e);
        await _db.SaveChangesAsync(ct);
    }

    // Rule #5: container numbers are unique per company.
    private async Task EnsureUniqueNumber(string number, Guid? excludeId, CancellationToken ct)
    {
        var exists = await _db.Containers.AnyAsync(c => c.CompanyId == _me.CompanyId
            && c.ContainerNumber == number && (excludeId == null || c.Id != excludeId), ct);
        if (exists)
            throw new DomainException($"Container number '{number}' is already in use.");
    }

    private static void Apply(Container e, ContainerUpsert d)
    {
        e.ContainerNumber = d.ContainerNumber; e.CurrentAddress = d.CurrentAddress;
        e.Latitude = d.Latitude; e.Longitude = d.Longitude; e.WasteTypeId = d.WasteTypeId;
        e.Size = d.Size; e.Status = d.Status; e.LastServicedAt = d.LastServicedAt;
    }

    private async Task<Container> Find(Guid id, CancellationToken ct) =>
        await _db.Containers.FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == _me.CompanyId, ct)
        ?? throw new NotFoundException("Container not found.");
}
