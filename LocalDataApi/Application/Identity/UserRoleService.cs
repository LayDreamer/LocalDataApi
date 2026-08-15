using LocalDataApi.Application.Common;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using LocalDataApi.Utils;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Identity;

public interface IUserRoleService
{
    Task<PagedResult<UserListItemDto>> QueryUsersAsync(UserQueryDto query, CancellationToken ct = default);
    Task<UserDetailDto> GetUserDetailAsync(long userId, CancellationToken ct = default);
    Task AssignRolesAsync(long userId, AssignRolesRequestDto dto, long? operatorId = null, CancellationToken ct = default);
    Task EnsureUserHasRoleAsync(long userId, string roleCode, long? operatorId = null, CancellationToken ct = default);
    Task<MeResultDto?> GetCurrentUserInfoAsync(long userId, CancellationToken ct = default);
}

public sealed class UserRoleService(AppDbContext context, IPermissionCacheService permissionCache, IAuditLogService auditLog, AuthorizationService authorization) : IUserRoleService
{
    public async Task<PagedResult<UserListItemDto>> QueryUsersAsync(UserQueryDto query, CancellationToken ct = default)
    {
        var source = context.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            source = source.Where(item => item.UserName.Contains(keyword) || item.DisplayName.Contains(keyword));
        }
        if (query.DepartmentId.HasValue)
        {
            var userIds = context.Employees.Where(item => item.DepartmentId == query.DepartmentId && item.UserId.HasValue).Select(item => item.UserId!.Value);
            source = source.Where(item => userIds.Contains(item.Id));
        }
        var total = await source.CountAsync(ct);
        var users = await source.OrderByDescending(item => item.CreatedAtUtc).ToPageItemsAsync(query, ct);
        var ids = users.Select(item => item.Id).ToList();
        var roleMap = await GetRoleMapAsync(ids, ct);
        var employees = await (from employee in context.Employees.AsNoTracking()
                               join department in context.Departments.AsNoTracking() on employee.DepartmentId equals department.Id
                               join position in context.Positions.AsNoTracking() on employee.PositionId equals position.Id
                               where employee.UserId.HasValue && ids.Contains(employee.UserId.Value)
                               select new { UserId = employee.UserId!.Value, Department = department.Name, Position = position.Name }).ToDictionaryAsync(item => item.UserId, ct);
        return new PagedResult<UserListItemDto>
        {
            Total = total, Page = query.Page, PageSize = query.PageSize,
            Items = users.Select(user => new UserListItemDto
            {
                Id = user.Id, UserName = user.UserName, DisplayName = user.DisplayName, Email = user.Email,
                PhoneNumber = user.PhoneNumber, IsActive = user.IsActive,
                PrimaryDepartmentName = employees.TryGetValue(user.Id, out var employee) ? employee.Department : null,
                Position = employee?.Position, Roles = roleMap.TryGetValue(user.Id, out var roles) ? roles : new(),
                CreateDate = user.CreatedAtUtc
            }).ToList()
        };
    }

    public async Task<UserDetailDto> GetUserDetailAsync(long userId, CancellationToken ct = default)
    {
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId, ct)
            ?? throw new NotFoundException("用户不存在");
        var roles = await (from assignment in context.UserRoles
                           join role in context.Roles on assignment.RoleId equals role.Id
                           where assignment.UserId == userId && assignment.IsActive && role.Enabled
                           select new { role.Id, role.Code }).ToListAsync(ct);
        var employee = await (from item in context.Employees.AsNoTracking()
                              join department in context.Departments.AsNoTracking() on item.DepartmentId equals department.Id
                              join position in context.Positions.AsNoTracking() on item.PositionId equals position.Id
                              where item.UserId == userId
                              select new { item.DepartmentId, Department = department.Name, Position = position.Name }).FirstOrDefaultAsync(ct);
        return new UserDetailDto
        {
            Id = user.Id, UserName = user.UserName, DisplayName = user.DisplayName, Email = user.Email,
            PhoneNumber = user.PhoneNumber, IsActive = user.IsActive, PermissionVersion = user.PermissionVersion,
            PrimaryDepartmentId = employee?.DepartmentId, PrimaryDepartmentName = employee?.Department,
            Position = employee?.Position, RoleIds = roles.Select(item => item.Id).ToList(), Roles = roles.Select(item => item.Code).ToList(),
            Permissions = (await authorization.GetUserPermissionsAsync(userId, ct)).ToList()
        };
    }

    public async Task AssignRolesAsync(long userId, AssignRolesRequestDto dto, long? operatorId = null, CancellationToken ct = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(item => item.Id == userId, ct) ?? throw new NotFoundException("用户不存在");
        var wanted = dto.RoleIds.Distinct().ToHashSet();
        if (wanted.Count > 0 && await context.Roles.CountAsync(item => wanted.Contains(item.Id) && item.Enabled, ct) != wanted.Count)
            throw new ValidationException("存在无效或已禁用的角色");
        var assignments = await context.UserRoles.Where(item => item.UserId == userId).ToListAsync(ct);
        var adminRoleId = await context.Roles.Where(item => item.Code == "ADMIN").Select(item => (Guid?)item.Id).FirstOrDefaultAsync(ct);
        if (adminRoleId.HasValue && assignments.Any(item => item.IsActive && item.RoleId == adminRoleId) && !wanted.Contains(adminRoleId.Value) &&
            await context.UserRoles.CountAsync(item => item.IsActive && item.RoleId == adminRoleId && item.UserId != userId, ct) == 0)
            throw new ConflictException("不能取消最后一名管理员的 ADMIN 角色");
        foreach (var assignment in assignments.Where(item => item.IsActive && !wanted.Contains(item.RoleId))) { assignment.IsActive = false; assignment.RevokedAt = DateTime.UtcNow; }
        foreach (var roleId in wanted)
        {
            var assignment = assignments.FirstOrDefault(item => item.RoleId == roleId);
            if (assignment is null) context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = userId, RoleId = roleId, AssignedAt = DateTime.UtcNow, AssignedBy = operatorId });
            else if (!assignment.IsActive) { assignment.IsActive = true; assignment.RevokedAt = null; assignment.AssignedAt = DateTime.UtcNow; assignment.AssignedBy = operatorId; }
        }
        await context.SaveChangesAsync(ct);
        await permissionCache.ClearUserPermissionCacheAsync(userId, ct);
        await context.SaveChangesAsync(ct);
        await TryAuditAsync(operatorId, "AssignUserRole", userId.ToString(), new { user.UserName, RoleIds = wanted });
    }

    public async Task EnsureUserHasRoleAsync(long userId, string roleCode, long? operatorId = null, CancellationToken ct = default)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(roleCode)) return;
        var role = await context.Roles.FirstOrDefaultAsync(item => item.Code == roleCode && item.Enabled, ct);
        if (role is null) return;
        var assignment = await context.UserRoles.FirstOrDefaultAsync(item => item.UserId == userId && item.RoleId == role.Id, ct);
        if (assignment?.IsActive == true) return;
        if (assignment is null) context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = userId, RoleId = role.Id, AssignedAt = DateTime.UtcNow, AssignedBy = operatorId });
        else { assignment.IsActive = true; assignment.RevokedAt = null; assignment.AssignedAt = DateTime.UtcNow; assignment.AssignedBy = operatorId; }
        await context.SaveChangesAsync(ct);
        await permissionCache.ClearUserPermissionCacheAsync(userId, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task<MeResultDto?> GetCurrentUserInfoAsync(long userId, CancellationToken ct = default)
    {
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId, ct);
        if (user is null) return null;
        var employee = await (from item in context.Employees.AsNoTracking()
                              join department in context.Departments.AsNoTracking() on item.DepartmentId equals department.Id
                              join position in context.Positions.AsNoTracking() on item.PositionId equals position.Id
                              where item.UserId == userId select new { department.Name, Position = position.Name }).FirstOrDefaultAsync(ct);
        var permissions = await authorization.GetUserRolesAndPermissionsAsync(userId, ct);
        return new MeResultDto { Id = user.Id, UserName = user.UserName, DisplayName = user.DisplayName, Department = employee?.Name, Position = employee?.Position, Roles = permissions.Roles, Permissions = permissions.Permissions };
    }

    private async Task<Dictionary<long, List<string>>> GetRoleMapAsync(List<long> userIds, CancellationToken ct) =>
        await (from assignment in context.UserRoles.AsNoTracking()
               join role in context.Roles.AsNoTracking() on assignment.RoleId equals role.Id
               where userIds.Contains(assignment.UserId) && assignment.IsActive && role.Enabled
               group role.Code by assignment.UserId into groupBy
               select new { UserId = groupBy.Key, Roles = groupBy.ToList() }).ToDictionaryAsync(item => item.UserId, item => item.Roles, ct);

    private async Task TryAuditAsync(long? operatorId, string action, string targetId, object content)
    {
        try { await auditLog.LogAsync(operatorId?.ToString(), action, "User", targetId, content); } catch { }
    }
}
