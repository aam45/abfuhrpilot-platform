using AbfuhrPilot.Api.Idempotency;
using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbfuhrPilot.Api.Controllers;

/// <summary>Management of the master-data catalogue. Editing is limited to Admin/Dispatcher.</summary>
[ApiController]
[Route("api/v1/waste-types")]
[Authorize]
public class WasteTypesController : ControllerBase
{
    private readonly WasteTypeService _svc;
    public WasteTypesController(WasteTypeService svc) => _svc = svc;

    [HttpGet] public Task<PagedResult<WasteTypeDto>> List([FromQuery] PageQuery q, CancellationToken ct) => _svc.ListAsync(q, ct);
    [HttpGet("{id:guid}")] public Task<WasteTypeDto> Get(Guid id, CancellationToken ct) => _svc.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = "Admin,Dispatcher")]
    public Task<WasteTypeDto> Create(WasteTypeUpsert dto, CancellationToken ct) => _svc.CreateAsync(dto, ct);

    [HttpPut("{id:guid}"), Authorize(Roles = "Admin,Dispatcher")]
    public Task<WasteTypeDto> Update(Guid id, WasteTypeUpsert dto, CancellationToken ct) => _svc.UpdateAsync(id, dto, ct);

    [HttpDelete("{id:guid}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/v1/vehicles")]
[Authorize]
public class VehiclesController : ControllerBase
{
    private readonly VehicleService _svc;
    public VehiclesController(VehicleService svc) => _svc = svc;

    [HttpGet] public Task<PagedResult<VehicleDto>> List([FromQuery] PageQuery q, CancellationToken ct) => _svc.ListAsync(q, ct);
    [HttpGet("{id:guid}")] public Task<VehicleDto> Get(Guid id, CancellationToken ct) => _svc.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = "Admin,Dispatcher")]
    public Task<VehicleDto> Create(VehicleUpsert dto, CancellationToken ct) => _svc.CreateAsync(dto, ct);

    [HttpPut("{id:guid}"), Authorize(Roles = "Admin,Dispatcher")]
    public Task<VehicleDto> Update(Guid id, VehicleUpsert dto, CancellationToken ct) => _svc.UpdateAsync(id, dto, ct);

    [HttpDelete("{id:guid}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/v1/drivers")]
[Authorize]
public class DriversController : ControllerBase
{
    private readonly DriverService _svc;
    public DriversController(DriverService svc) => _svc = svc;

    [HttpGet] public Task<PagedResult<DriverDto>> List([FromQuery] PageQuery q, CancellationToken ct) => _svc.ListAsync(q, ct);
    [HttpGet("{id:guid}")] public Task<DriverDto> Get(Guid id, CancellationToken ct) => _svc.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = "Admin,Dispatcher")]
    public Task<DriverDto> Create(DriverUpsert dto, CancellationToken ct) => _svc.CreateAsync(dto, ct);

    [HttpPut("{id:guid}"), Authorize(Roles = "Admin,Dispatcher")]
    public Task<DriverDto> Update(Guid id, DriverUpsert dto, CancellationToken ct) => _svc.UpdateAsync(id, dto, ct);

    [HttpDelete("{id:guid}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/v1/destinations")]
[Authorize]
public class DestinationsController : ControllerBase
{
    private readonly DestinationService _svc;
    public DestinationsController(DestinationService svc) => _svc = svc;

    [HttpGet] public Task<PagedResult<DestinationDto>> List([FromQuery] PageQuery q, CancellationToken ct) => _svc.ListAsync(q, ct);
    [HttpGet("{id:guid}")] public Task<DestinationDto> Get(Guid id, CancellationToken ct) => _svc.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = "Admin,Dispatcher")]
    public Task<DestinationDto> Create(DestinationUpsert dto, CancellationToken ct) => _svc.CreateAsync(dto, ct);

    [HttpPut("{id:guid}"), Authorize(Roles = "Admin,Dispatcher")]
    public Task<DestinationDto> Update(Guid id, DestinationUpsert dto, CancellationToken ct) => _svc.UpdateAsync(id, dto, ct);

    [HttpDelete("{id:guid}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/v1/containers")]
[Authorize]
public class ContainersController : ControllerBase
{
    private readonly ContainerService _svc;
    public ContainersController(ContainerService svc) => _svc = svc;

    [HttpGet] public Task<PagedResult<ContainerDto>> List([FromQuery] PageQuery q, CancellationToken ct) => _svc.ListAsync(q, ct);
    [HttpGet("{id:guid}")] public Task<ContainerDto> Get(Guid id, CancellationToken ct) => _svc.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = "Admin,Dispatcher"), Idempotent]
    public Task<ContainerDto> Create(ContainerUpsert dto, CancellationToken ct) => _svc.CreateAsync(dto, ct);

    [HttpPut("{id:guid}"), Authorize(Roles = "Admin,Dispatcher")]
    public Task<ContainerDto> Update(Guid id, ContainerUpsert dto, CancellationToken ct) => _svc.UpdateAsync(id, dto, ct);

    [HttpDelete("{id:guid}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.DeleteAsync(id, ct); return NoContent(); }
}
