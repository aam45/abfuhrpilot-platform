using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbfuhrPilot.Api.Controllers;

/// <summary>Company user management. Admin only.</summary>
[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly UserService _users;
    public UsersController(UserService users) => _users = users;

    [HttpGet]
    public Task<PagedResult<UserDto>> List([FromQuery] PageQuery q, CancellationToken ct) => _users.ListAsync(q, ct);

    [HttpPost]
    public Task<UserDto> Create(CreateUserRequest req, CancellationToken ct) => _users.CreateAsync(req, ct);

    [HttpPut("{id:guid}")]
    public Task<UserDto> Update(Guid id, UpdateUserRequest req, CancellationToken ct) => _users.UpdateAsync(id, req, ct);

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest req, CancellationToken ct)
    {
        await _users.ResetPasswordAsync(id, req, ct);
        return NoContent();
    }
}
