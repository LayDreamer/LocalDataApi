using LocalDataApi.Application.Common;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Identity
{
    /// <summary>
    /// 角色管理服务(角色 CRUD + 角色权限分配)。
    /// </summary>
    public interface IRoleService
    {
        /// <summary>查询角色列表(含角色已绑定的权限ID)。</summary>
        Task<List<RoleDto>> GetRolesAsync(CancellationToken ct = default);

        /// <summary>查询单个角色信息,不依赖权限关联。</summary>
        Task<RoleDto> GetRoleAsync(Guid id, CancellationToken ct = default);

        /// <summary>查询角色已绑定的权限ID。</summary>
        Task<List<Guid>> GetRolePermissionIdsAsync(Guid id, CancellationToken ct = default);

        /// <summary>创建角色。</summary>
        Task<RoleDto> CreateRoleAsync(RoleCreateDto dto, string? operatorId = null, CancellationToken ct = default);

        /// <summary>复制角色及其权限,新角色始终为非系统业务角色。</summary>
        Task<RoleDto> CopyRoleAsync(Guid id, CopyRoleRequestDto dto, string? operatorId = null, CancellationToken ct = default);

        /// <summary>修改角色(名称/显示名/描述/启用状态)。</summary>
        Task<RoleDto> UpdateRoleAsync(Guid id, RoleUpdateDto dto, string? operatorId = null, CancellationToken ct = default);

        /// <summary>删除角色(系统角色禁止删除;已绑定用户的角色禁止删除)。</summary>
        Task DeleteRoleAsync(Guid id, string? operatorId = null, CancellationToken ct = default);

        /// <summary>为角色分配权限(覆盖式更新;刷新受影响用户权限版本与缓存)。</summary>
        Task AssignPermissionsAsync(Guid roleId, AssignPermissionsRequestDto dto, string? operatorId = null, CancellationToken ct = default);
    }

    /// <summary>角色管理服务实现。</summary>
    public sealed class RoleService : IRoleService
    {
        private readonly AppDbContext _context;
        private readonly IPermissionCacheService _permissionCache;
        private readonly IAuditLogService _auditLog;

        public RoleService(AppDbContext context, IPermissionCacheService permissionCache, IAuditLogService auditLog)
        {
            _context = context;
            _permissionCache = permissionCache;
            _auditLog = auditLog;
        }

        public async Task<List<RoleDto>> GetRolesAsync(CancellationToken ct = default)
        {
            var roles = await _context.Roles.AsNoTracking()
                .OrderBy(r => r.IsSystem)
                .ThenBy(r => r.CreateTime)
                .ToListAsync(ct);

            var roleIds = roles.Select(r => r.Id).ToList();
            var rolePermissionMap = await _context.RolePermissions.AsNoTracking()
                .Where(rp => roleIds.Contains(rp.RoleId))
                .GroupBy(rp => rp.RoleId)
                .Select(g => new { RoleId = g.Key, PermissionIds = g.Select(x => x.PermissionId).ToList() })
                .ToDictionaryAsync(g => g.RoleId, g => g.PermissionIds, ct);

            return roles.Select(r => new RoleDto
            {
                Id = r.Id,
                Code = r.Code,
                Name = r.Name,
                DisplayName = r.DisplayName,
                Description = r.Description,
                IsBuiltIn = r.IsBuiltIn,
                IsSystem = r.IsSystem,
                Enabled = r.Enabled,
                CreateTime = r.CreateTime,
                PermissionIds = rolePermissionMap.TryGetValue(r.Id, out var ids) ? ids : new List<Guid>()
            }).ToList();
        }

        public async Task<RoleDto> GetRoleAsync(Guid id, CancellationToken ct = default)
        {
            var role = await _context.Roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new NotFoundException("角色不存在");

            return ToDto(role);
        }

        public async Task<List<Guid>> GetRolePermissionIdsAsync(Guid id, CancellationToken ct = default)
        {
            if (!await _context.Roles.AsNoTracking().AnyAsync(r => r.Id == id, ct))
                throw new NotFoundException("角色不存在");

            return await _context.RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleId == id)
                .OrderBy(rp => rp.PermissionId)
                .Select(rp => rp.PermissionId)
                .ToListAsync(ct);
        }

        public async Task<RoleDto> CreateRoleAsync(RoleCreateDto dto, string? operatorId = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Name))
                throw new ValidationException("角色编码和名称不能为空");

            var code = dto.Code.Trim().ToUpperInvariant();
            if (await _context.Roles.AsNoTracking().AnyAsync(r => r.Code == code, ct))
                throw new ConflictException($"角色编码 {code} 已存在");

            if (await _context.Roles.AsNoTracking().AnyAsync(r => r.Name == dto.Name.Trim(), ct))
                throw new ConflictException($"角色名称 {dto.Name.Trim()} 已存在");

            var now = DateTime.Now;
            var role = new Role
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = dto.Name.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? dto.Name.Trim() : dto.DisplayName.Trim(),
                Description = dto.Description,
                IsBuiltIn = false,
                IsSystem = false,
                Enabled = true,
                CreateTime = now,
                ModifyTime = now
            };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync(ct);

            await TryAuditAsync(operatorId, "CreateRole", "Role", role.Id.ToString(), new { role.Code, role.Name });

            return new RoleDto
            {
                Id = role.Id,
                Code = role.Code,
                Name = role.Name,
                DisplayName = role.DisplayName,
                Description = role.Description,
                IsBuiltIn = role.IsBuiltIn,
                IsSystem = role.IsSystem,
                Enabled = role.Enabled,
                CreateTime = role.CreateTime
            };
        }

        public async Task<RoleDto> CopyRoleAsync(Guid id, CopyRoleRequestDto dto, string? operatorId = null, CancellationToken ct = default)
        {
            var source = await _context.Roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new NotFoundException("角色不存在");

            var existing = await _context.Roles.AsNoTracking()
                .Select(r => new { r.Code, r.Name })
                .ToListAsync(ct);
            var codes = existing.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var names = existing.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var code = string.IsNullOrWhiteSpace(dto.Code) ? $"{source.Code}_COPY" : dto.Code.Trim().ToUpperInvariant();
            var name = string.IsNullOrWhiteSpace(dto.Name) ? $"{source.Name}-副本" : dto.Name.Trim();
            var sequence = 2;
            var baseCode = code;
            var baseName = name;
            while (codes.Contains(code) || names.Contains(name))
            {
                code = $"{baseCode}_{sequence}";
                name = $"{baseName}{sequence}";
                sequence++;
            }

            var now = DateTime.Now;
            var copy = new Role
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                DisplayName = name,
                Description = source.Description,
                IsBuiltIn = false,
                IsSystem = false,
                Enabled = true,
                CreateTime = now,
                ModifyTime = now
            };
            _context.Roles.Add(copy);

            var permissionIds = await _context.RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleId == id)
                .Select(rp => rp.PermissionId)
                .ToListAsync(ct);
            foreach (var permissionId in permissionIds)
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = copy.Id,
                    PermissionId = permissionId,
                    CreateTime = now
                });
            }

            await _context.SaveChangesAsync(ct);
            await TryAuditAsync(operatorId, "CopyRole", "Role", copy.Id.ToString(),
                new { SourceRoleId = source.Id, copy.Code, copy.Name, PermissionCount = permissionIds.Count });

            var result = ToDto(copy);
            result.PermissionIds = permissionIds;
            return result;
        }

        public async Task<RoleDto> UpdateRoleAsync(Guid id, RoleUpdateDto dto, string? operatorId = null, CancellationToken ct = default)
        {
            var role = await _context.Roles.AsTracking().FirstOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new NotFoundException("角色不存在");

            if (dto.Enabled == false && role.IsSystem)
                throw new ValidationException("系统角色禁止禁用");

            if (!string.IsNullOrWhiteSpace(dto.Name) && dto.Name.Trim() != role.Name)
            {
                if (await _context.Roles.AsNoTracking().AnyAsync(r => r.Id != id && r.Name == dto.Name.Trim(), ct))
                    throw new ConflictException($"角色名称 {dto.Name.Trim()} 已存在");
                role.Name = dto.Name.Trim();
            }
            if (!string.IsNullOrWhiteSpace(dto.DisplayName))
                role.DisplayName = dto.DisplayName.Trim();
            if (dto.Description != null)
                role.Description = dto.Description;
            var enabledChanged = dto.Enabled.HasValue && dto.Enabled.Value != role.Enabled;
            if (dto.Enabled.HasValue)
                role.Enabled = dto.Enabled.Value;

            role.ModifyTime = DateTime.Now;
            await _context.SaveChangesAsync(ct);
            if (enabledChanged)
            {
                await _permissionCache.ClearRolePermissionCacheAsync(id, ct);
                await _context.SaveChangesAsync(ct);
            }

            await TryAuditAsync(operatorId, "UpdateRole", "Role", role.Id.ToString(), new { role.Code, role.Name, role.Enabled });

            var permissionIds = await _context.RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync(ct);

            return new RoleDto
            {
                Id = role.Id,
                Code = role.Code,
                Name = role.Name,
                DisplayName = role.DisplayName,
                Description = role.Description,
                IsBuiltIn = role.IsBuiltIn,
                IsSystem = role.IsSystem,
                Enabled = role.Enabled,
                CreateTime = role.CreateTime,
                PermissionIds = permissionIds
            };
        }

        public async Task DeleteRoleAsync(Guid id, string? operatorId = null, CancellationToken ct = default)
        {
            var role = await _context.Roles.AsTracking().FirstOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new NotFoundException("角色不存在");

            if (role.IsSystem)
                throw new ValidationException("系统角色禁止删除");

            var boundCount = await _context.UserRoles.AsNoTracking()
                .CountAsync(ur => ur.RoleId == id && ur.IsActive, ct);
            if (boundCount > 0)
                throw new ConflictException($"该角色已绑定 {boundCount} 个用户,请先解除绑定");

            _context.Roles.Remove(role);
            // 级联清理角色权限关联(避免残留)
            var rolePermissions = await _context.RolePermissions.AsTracking()
                .Where(rp => rp.RoleId == id).ToListAsync(ct);
            _context.RolePermissions.RemoveRange(rolePermissions);
            await _context.SaveChangesAsync(ct);

            await TryAuditAsync(operatorId, "DeleteRole", "Role", role.Id.ToString(), new { role.Code, role.Name });
        }

        public async Task AssignPermissionsAsync(Guid roleId, AssignPermissionsRequestDto dto, string? operatorId = null, CancellationToken ct = default)
        {
            var role = await _context.Roles.AsTracking().FirstOrDefaultAsync(r => r.Id == roleId, ct)
                ?? throw new NotFoundException("角色不存在");

            var permissionIds = dto.PermissionIds?.Distinct().ToList() ?? new List<Guid>();
            if (role.IsSystem && permissionIds.Count == 0)
                throw new ValidationException("系统角色禁止清空权限");

            // 校验权限点都存在且启用
            if (permissionIds.Count > 0)
            {
                var validCount = await _context.Permissions.AsNoTracking()
                    .CountAsync(p => permissionIds.Contains(p.Id) && p.Enabled, ct);
                if (validCount != permissionIds.Count)
                    throw new ValidationException("存在无效或已停用的权限点");
            }

            var existing = await _context.RolePermissions.AsTracking()
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync(ct);

            var existingSet = existing.Select(rp => rp.PermissionId).ToHashSet();
            var targetSet = permissionIds.ToHashSet();

            // 需要删除的关联
            var toRemove = existing.Where(rp => !targetSet.Contains(rp.PermissionId)).ToList();
            if (toRemove.Count > 0)
                _context.RolePermissions.RemoveRange(toRemove);

            // 需要新增的关联
            foreach (var pid in targetSet.Where(p => !existingSet.Contains(p)))
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = roleId,
                    PermissionId = pid,
                    CreateTime = DateTime.Now
                });
            }

            // 覆盖式更新:即使无变化也记录操作;有变化才刷新权限版本与缓存
            var changed = toRemove.Count > 0 || targetSet.Any(p => !existingSet.Contains(p));
            if (changed)
            {
                role.ModifyTime = DateTime.Now;
                await _context.SaveChangesAsync(ct);
                await _permissionCache.ClearRolePermissionCacheAsync(roleId, ct);
                await _context.SaveChangesAsync(ct);
            }
            else
            {
                await _context.SaveChangesAsync(ct);
            }

            await TryAuditAsync(operatorId, "AssignRolePermission", "Role", roleId.ToString(),
                new { RoleCode = role.Code, PermissionIds = permissionIds });
        }

        private static RoleDto ToDto(Role role) => new()
        {
            Id = role.Id,
            Code = role.Code,
            Name = role.Name,
            DisplayName = role.DisplayName,
            Description = role.Description,
            IsBuiltIn = role.IsBuiltIn,
            IsSystem = role.IsSystem,
            Enabled = role.Enabled,
            CreateTime = role.CreateTime
        };

        /// <summary>审计日志失败不影响主流程。</summary>
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
