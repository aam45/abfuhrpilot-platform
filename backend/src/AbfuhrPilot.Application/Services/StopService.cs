using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.Application.Services;

public class StopService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _me;
    public StopService(IAppDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public async Task<StopDto> UpdateAsync(Guid id, StopUpsert dto, CancellationToken ct)
    {
        var (stop, route) = await FindStop(id, ct);
        // Rule #11/#12: drivers cannot edit planning data.
        if (_me.Role == UserRole.Driver)
            throw new ForbiddenException("Drivers cannot modify stop planning data.");
        stop.SequenceNumber = dto.SequenceNumber; stop.Address = dto.Address; stop.Latitude = dto.Latitude;
        stop.Longitude = dto.Longitude; stop.WasteTypeId = dto.WasteTypeId; stop.PickupType = dto.PickupType;
        stop.ContainerId = dto.ContainerId; stop.CustomerName = dto.CustomerName; stop.Notes = dto.Notes;
        stop.PlannedArrivalTime = dto.PlannedArrivalTime; stop.Touch();
        await _db.SaveChangesAsync(ct);
        return stop.ToDto();
    }

    public async Task<StopDto> StartAsync(Guid id, CancellationToken ct)
    {
        var (stop, route) = await FindStop(id, ct);
        EnsureDriverOwnsRoute(route);
        stop.Start(route.IsStarted);
        await _db.SaveChangesAsync(ct);
        return stop.ToDto();
    }

    public async Task<StopDto> CompleteAsync(Guid id, CancellationToken ct)
    {
        var (stop, route) = await FindStop(id, ct);
        EnsureDriverOwnsRoute(route);
        stop.Complete(route.IsStarted); // Rules #7/#8
        await _db.SaveChangesAsync(ct);
        return stop.ToDto();
    }

    public async Task<StopDto> SkipAsync(Guid id, CancellationToken ct)
    {
        var (stop, route) = await FindStop(id, ct);
        EnsureDriverOwnsRoute(route);
        stop.Skip(route.IsStarted);
        await _db.SaveChangesAsync(ct);
        return stop.ToDto();
    }

    public async Task<StopDto> ReportProblemAsync(Guid id, ReportProblemRequest req, CancellationToken ct)
    {
        var (stop, route) = await FindStop(id, ct);
        EnsureDriverOwnsRoute(route);
        stop.ReportProblem(req.ProblemType); // Rule #9

        var report = new ProblemReport
        {
            CompanyId = _me.CompanyId,
            StopId = stop.Id,
            DriverId = _me.DriverId ?? route.DriverId ?? Guid.Empty,
            ProblemType = req.ProblemType,
            Description = req.Description
        };
        _db.ProblemReports.Add(report);

        // Link any pre-uploaded photos (company-scoped) to this report.
        if (req.PhotoIds is { Count: > 0 })
        {
            var ids = req.PhotoIds.Distinct().ToList();
            var photos = await _db.ProblemPhotos
                .Where(p => ids.Contains(p.Id) && p.CompanyId == _me.CompanyId && p.ProblemReportId == null)
                .ToListAsync(ct);
            foreach (var photo in photos) photo.ProblemReportId = report.Id;
            if (photos.Count > 0)
                report.PhotoUrl = $"/api/v1/problem-photos/{photos[0].Id}/content"; // first photo for back-compat display
        }

        await _db.SaveChangesAsync(ct);
        return stop.ToDto();
    }

    private void EnsureDriverOwnsRoute(Route route)
    {
        // Rule #12: drivers may only update operational status for their own assigned work.
        if (_me.Role == UserRole.Driver && route.DriverId != _me.DriverId)
            throw new ForbiddenException("You can only update stops on routes assigned to you.");
    }

    private async Task<(Stop stop, Route route)> FindStop(Guid id, CancellationToken ct)
    {
        var stop = await _db.Stops.FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == _me.CompanyId, ct)
            ?? throw new NotFoundException("Stop not found.");
        var route = await _db.Routes.FirstOrDefaultAsync(r => r.Id == stop.RouteId && r.CompanyId == _me.CompanyId, ct)
            ?? throw new NotFoundException("Route not found.");
        return (stop, route);
    }
}
