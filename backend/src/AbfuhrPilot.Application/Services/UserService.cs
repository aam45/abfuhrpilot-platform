using AbfuhrPilot.Application.Auth;
using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.Application.Services;

/// <summary>
/// Admin user-management within a company workspace. All operations are
/// company-scoped and protect the "last active administrator" invariant so a
/// workspace can never lock itself out.
/// </summary>
public class UserService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IPasswordHasher _hasher;

    public UserService(IAppDbContext db, ICurrentUser me, IPasswordHasher hasher)
    {
        _db = db;
        _me = me;
        _hasher = hasher;
    }

    public async Task<PagedResult<UserDto>> ListAsync(PageQuery q, CancellationToken ct)
    {
        var query = _db.Users.Where(u => u.CompanyId == _me.CompanyId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(s) || u.Email.ToLower().Contains(s));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(u => u.FullName)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(u => new UserDto(u.Id, u.FullName, u.Email, u.Role, u.CompanyId, null, u.IsActive))
            .ToListAsync(ct);
        return new PagedResult<UserDto> { Items = items, Page = q.Page, PageSize = q.PageSize, TotalCount = total };
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLower();
        PasswordPolicy.Validate(req.Password);
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new DomainException("A user with this email already exists.");

        var user = new User
        {
            CompanyId = _me.CompanyId,
            FullName = req.FullName.Trim(),
            Email = email,
            PasswordHash = _hasher.Hash(req.Password),
            Role = req.Role,
            IsActive = true
        };
        user.Raise(new Domain.Events.UserCreatedDomainEvent(user.CompanyId, user.Id, user.Role));
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct)
    {
        var user = await Find(id, ct);
        await EnsureNotRemovingLastAdmin(user, req.Role, req.IsActive, ct);

        var wasActive = user.IsActive;
        user.FullName = req.FullName.Trim();
        user.Role = req.Role;
        user.IsActive = req.IsActive;
        user.Touch();
        if (wasActive && !req.IsActive)
            user.Raise(new Domain.Events.UserDeactivatedDomainEvent(user.CompanyId, user.Id));
        await _db.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest req, CancellationToken ct)
    {
        var user = await Find(id, ct);
        PasswordPolicy.Validate(req.NewPassword);
        user.PasswordHash = _hasher.Hash(req.NewPassword);
        user.Touch();
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Prevents demoting/deactivating the workspace's last active admin.</summary>
    private async Task EnsureNotRemovingLastAdmin(User target, UserRole newRole, bool newActive, CancellationToken ct)
    {
        var wasActiveAdmin = target is { Role: UserRole.Admin, IsActive: true };
        var willBeActiveAdmin = newRole == UserRole.Admin && newActive;
        if (!wasActiveAdmin || willBeActiveAdmin) return;

        var otherActiveAdmins = await _db.Users.CountAsync(u =>
            u.CompanyId == _me.CompanyId && u.Role == UserRole.Admin && u.IsActive && u.Id != target.Id, ct);
        if (otherActiveAdmins == 0)
            throw new DomainException("The last active administrator cannot be demoted or deactivated.");
    }

    private async Task<User> Find(Guid id, CancellationToken ct) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == _me.CompanyId, ct)
        ?? throw new NotFoundException("User not found.");
}
