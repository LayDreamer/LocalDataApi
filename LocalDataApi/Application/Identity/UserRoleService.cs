using LocalDataApi.Application.Common;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using LocalDataApi.Utils;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Identity
{
    /// <summary>
    /// 用户角色管理服务(用户查询 + 用户角色绑定)。
    /// </summary>
    public interface IUserRoleService
    {
        /// <summary>分页查询用户列表(含角色编码)。</summary>
        Task<PagedResult<UserListItemDto>> QueryUsersAsync(UserQueryDto query, CancellationToken ct = default);

        /// <summary>查询用户详情(含部门/角色/有效权限)。</summary>
        Task<UserDetailDto> GetUserDetailAsync(string userId, CancellationToken ct = default);

        /// <summary>覆盖式分配用户角色(自动刷新权限版本与缓存;保护最后一个管理员)。</summary>
        Task AssignRolesAsync(string userId, AssignRolesRequestDto dto, string? operatorId = null, CancellationToken ct = default);

        /// <summary>确保用户拥有指定角色(登录自动绑定默认角色时调用;已拥有则跳过)。</summary>
        Task EnsureUserHasRoleAsync(string userId, string roleCode, string? operatorId = null, CancellationToken ct = default);

        /// <summary>获取当前登录用户信息(me 接口;用户不存在返回 null)。</summary>
        Task<MeResultDto?> GetCurrentUserInfoAsync(string userId, CancellationToken ct = default);
    }

    /// <summary>用户角色管理服务实现。</summary>
    public sealed class UserRoleService : IUserRoleService
    {
        private readonly AppDbContext _context;
        private readonly IPermissionCacheService _permissionCache;
        private readonly IAuditLogService _auditLog;
        private readonly AuthorizationService _authorization;

        public UserRoleService(AppDbContext context, IPermissionCacheService permissionCache, IAuditLogService auditLog, AuthorizationService authorization)
        {
            _context = context;
            _permissionCache = permissionCache;
            _auditLog = auditLog;
            _authorization = authorization;
        }

        public async Task<PagedResult<UserListItemDto>> QueryUsersAsync(UserQueryDto query, CancellationToken ct = default)
        {
            var baseQuery = _context.用户管理.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var keyword = query.Keyword.Trim();
                baseQuery = baseQuery.Where(u =>
                    (u.UserName != null && u.UserName.Contains(keyword)) ||
                    (u.DisplayName != null && u.DisplayName.Contains(keyword)));
            }

            if (query.DepartmentId.HasValue)
            {
                var departmentId = query.DepartmentId.Value;
                baseQuery = baseQuery.Where(u => u.PrimaryDepartmentId == departmentId);
            }

            var total = await baseQuery.CountAsync(ct);
            var items = await baseQuery
                .OrderByDescending(u => u.CreateDate)
                .ToPageItemsAsync(query, ct);

            var result = new List<UserListItemDto>(items.Count);
            if (items.Count > 0)
            {
                var userIds = items.Where(u => u.Id != null).Select(u => u.Id!).ToList();
                var roleMap = await GetUserRoleMapAsync(userIds, ct);

                foreach (var user in items)
                {
                    result.Add(new UserListItemDto
                    {
                        Id = user.Id,
                        UserName = user.UserName,
                        DisplayName = user.DisplayName,
                        Email = user.Email,
                        PhoneNumber = user.PhoneNumber,
                        IsActive = user.IsActive,
                        PrimaryDepartmentName = user.PrimaryDepartmentName,
                        Position = user.Position,
                        Roles = roleMap.TryGetValue(user.Id ?? "", out var roles) ? roles : new List<string>(),
                        CreateDate = DateTime.TryParse(user.CreateDate, out var d) ? d : null
                    });
                }
            }

            return new PagedResult<UserListItemDto>
            {
                Items = result,
                Total = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<UserDetailDto> GetUserDetailAsync(string userId, CancellationToken ct = default)
        {
            var user = await _context.用户管理.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct)
                ?? throw new NotFoundException("用户不存在");

            var roles = await (
                from ur in _context.UserRoles
                join r in _context.Roles on ur.RoleId equals r.Id
                where ur.UserId == userId && ur.IsActive && r.Enabled
                select new { r.Id, r.Code }).ToListAsync(ct);

            var permissions = await _authorization.GetUserPermissionsAsync(userId, ct);

            return new UserDetailDto
            {
                Id = user.Id,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                Position = user.Position,
                IsLeader = user.IsLeader,
                PrimaryDepartmentId = user.PrimaryDepartmentId,
                PrimaryDepartmentName = user.PrimaryDepartmentName,
                WeChatWorkUserId = user.WeChatWorkUserId,
                PermissionVersion = user.PermissionVersion,
                RoleIds = roles.Select(r => r.Id).ToList(),
                Roles = roles.Select(r => r.Code).ToList(),
                Permissions = permissions.ToList()
            };
        }

        public async Task AssignRolesAsync(string userId, AssignRolesRequestDto dto, string? operatorId = null, CancellationToken ct = default)
        {
            var user = await _context.用户管理.AsTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct)
                ?? throw new NotFoundException("用户不存在");

            var roleIds = dto.RoleIds?.Distinct().ToList() ?? new List<Guid>();

            // 校验角色都存在且启用
            if (roleIds.Count > 0)
            {
                var validCount = await _context.Roles.AsNoTracking()
                    .CountAsync(r => roleIds.Contains(r.Id) && r.Enabled, ct);
                if (validCount != roleIds.Count)
                    throw new ValidationException("存在无效或已禁用的角色");
            }

            var existing = await _context.UserRoles.AsTracking()
                .Where(ur => ur.UserId == userId)
                .ToListAsync(ct);
            var active = existing.Where(ur => ur.IsActive).ToList();

            // 保护最后一个管理员:移除 Admin 角色前检查
            var adminRoleId = await _context.Roles.AsNoTracking()
                .Where(r => r.Code == "ADMIN")
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync(ct);
            if (adminRoleId.HasValue &&
                active.Any(ur => ur.RoleId == adminRoleId.Value) &&
                !roleIds.Contains(adminRoleId.Value))
            {
                var otherAdmins = await _context.UserRoles.AsNoTracking()
                    .CountAsync(ur => ur.RoleId == adminRoleId.Value && ur.IsActive && ur.UserId != userId, ct);
                if (otherAdmins == 0)
                    throw new ConflictException("不能取消最后一名管理员的 Admin 角色");
            }

            var targetSet = roleIds.ToHashSet();
            var existingSet = existing.Where(ur => ur.IsActive).Select(ur => ur.RoleId).ToHashSet();

            // 撤销:现有有效但目标不包含
            foreach (var ur in active.Where(ur => !targetSet.Contains(ur.RoleId)))
            {
                ur.IsActive = false;
                ur.RevokedAt = DateTime.Now;
            }

            // 新增:目标有但现有无(或已撤销)
            foreach (var roleId in targetSet.Where(r => !existingSet.Contains(r)))
            {
                var record = existing.FirstOrDefault(ur => !ur.IsActive && ur.RoleId == roleId);
                if (record != null)
                {
                    // 复用已撤销记录(保留审计轨迹)
                    record.IsActive = true;
                    record.AssignedAt = DateTime.Now;
                    record.AssignedBy = operatorId;
                    record.RevokedAt = null;
                }
                else
                {
                    _context.UserRoles.Add(new UserRole
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        RoleId = roleId,
                        AssignedAt = DateTime.Now,
                        AssignedBy = operatorId,
                        IsActive = true
                    });
                }
            }

            var changed = active.Any(ur => !targetSet.Contains(ur.RoleId)) ||
                          targetSet.Any(r => !existingSet.Contains(r));
            if (changed)
            {
                // 先保存角色关联,再提升权限版本并清缓存,避免旧权限被并发请求重新缓存。
                await _context.SaveChangesAsync(ct);
                await _permissionCache.ClearUserPermissionCacheAsync(userId, ct);
                await _context.SaveChangesAsync(ct);
            }
            else
            {
                await _context.SaveChangesAsync(ct);
            }

            await TryAuditAsync(operatorId, "AssignUserRole", "User", userId,
                new { UserName = user.UserName, RoleIds = roleIds });
        }

        public async Task EnsureUserHasRoleAsync(string userId, string roleCode, string? operatorId = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleCode))
                return;

            var role = await _context.Roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Code == roleCode && r.Enabled, ct);
            if (role == null)
                return; // 默认角色未初始化时静默跳过

            var existing = await _context.UserRoles.AsTracking()
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == role.Id, ct);

            if (existing != null)
            {
                if (existing.IsActive)
                    return; // 已存在且有效,无需重复绑定

                // 复用已撤销记录,避免违反 (UserId,RoleId) 唯一索引
                existing.IsActive = true;
                existing.AssignedAt = DateTime.Now;
                existing.AssignedBy = operatorId;
                existing.RevokedAt = null;
            }
            else
            {
                _context.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    RoleId = role.Id,
                    AssignedAt = DateTime.Now,
                    AssignedBy = operatorId,
                    IsActive = true
                });
            }

            await _context.SaveChangesAsync(ct);
            await _permissionCache.ClearUserPermissionCacheAsync(userId, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<MeResultDto?> GetCurrentUserInfoAsync(string userId, CancellationToken ct = default)
        {
            var user = await _context.用户管理.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user == null)
                return null;

            var (roles, permissions) = await _authorization.GetUserRolesAndPermissionsAsync(userId, ct);

            return new MeResultDto
            {
                Id = user.Id,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Department = user.PrimaryDepartmentName,
                Position = user.Position,
                Roles = roles,
                Permissions = permissions
            };
        }

        /// <summary>批量查询多个用户的有效角色编码(分组映射)。</summary>
        private async Task<Dictionary<string, List<string>>> GetUserRoleMapAsync(List<string> userIds, CancellationToken ct)
        {
            var map = new Dictionary<string, List<string>>();
            foreach (var batch in userIds.Chunk(100))
            {
                var rows = await (
                    from ur in _context.UserRoles
                    join r in _context.Roles on ur.RoleId equals r.Id
                    where batch.Contains(ur.UserId) && ur.IsActive && r.Enabled
                    select new { ur.UserId, r.Code }).ToListAsync(ct);

                foreach (var row in rows)
                {
                    if (!map.TryGetValue(row.UserId, out var list))
                    {
                        list = new List<string>();
                        map[row.UserId] = list;
                    }
                    if (!list.Contains(row.Code))
                        list.Add(row.Code);
                }
            }
            return map;
        }

        private async Task TryAuditAsync(string? operatorId, string action, string targetType, string? targetId, object? content)
        {
            try
            {
                await _auditLog.LogAsync(operatorId, action, targetType, targetId, content);
            }
            catch
            {
                // 忽略审计异常
            }
        }
    }
}
