using LocalDataApi.Application.Common;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Identity
{
    /// <summary>
    /// 权限点服务(权限字典查询 + 权限点启用/停用)。
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>查询全部权限点(可按模块过滤)。</summary>
        Task<List<PermissionDto>> GetPermissionsAsync(string? module = null, CancellationToken ct = default);

        /// <summary>查询全部权限编码(含停用;用于前端初始化校验/CI检查/权限差异分析)。</summary>
        Task<List<string>> GetAllPermissionCodesAsync(CancellationToken ct = default);

        /// <summary>启用/停用权限点(记录审计;停用后绑定该权限的用户权限实时失效)。</summary>
        Task<PermissionDto> UpdatePermissionAsync(Guid id, bool enabled, string? operatorId = null, CancellationToken ct = default);
    }

    /// <summary>权限点服务实现。</summary>
    public sealed class PermissionService : IPermissionService
    {
        private readonly AppDbContext _context;
        private readonly IPermissionCacheService _permissionCache;
        private readonly IAuditLogService _auditLog;

        public PermissionService(AppDbContext context, IPermissionCacheService permissionCache, IAuditLogService auditLog)
        {
            _context = context;
            _permissionCache = permissionCache;
            _auditLog = auditLog;
        }

        public async Task<List<PermissionDto>> GetPermissionsAsync(string? module = null, CancellationToken ct = default)
        {
            var query = _context.Permissions.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(module))
            {
                query = query.Where(p => p.Module == module);
            }

            var permissions = await query
                .OrderBy(p => p.Module)
                .ThenBy(p => p.Resource)
                .ThenBy(p => p.Action)
                .ToListAsync(ct);

            return permissions.Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Module = p.Module,
                Resource = p.Resource,
                Action = p.Action,
                DisplayName = p.DisplayName,
                Description = p.Description,
                Enabled = p.Enabled
            }).ToList();
        }

        public async Task<List<string>> GetAllPermissionCodesAsync(CancellationToken ct = default)
        {
            return await _context.Permissions.AsNoTracking()
                .OrderBy(p => p.Code)
                .Select(p => p.Code)
                .ToListAsync(ct);
        }

        public async Task<PermissionDto> UpdatePermissionAsync(Guid id, bool enabled, string? operatorId = null, CancellationToken ct = default)
        {
            var permission = await _context.Permissions.AsTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct)
                ?? throw new NotFoundException("权限点不存在");

            var changed = permission.Enabled != enabled;
            permission.Enabled = enabled;
            permission.ModifyTime = DateTime.Now;

            if (changed)
            {
                // 权限状态变化:清除绑定该权限的所有用户缓存,权限实时生效/失效
                await _permissionCache.ClearPermissionCacheAsync(id, ct);
            }

            await _context.SaveChangesAsync(ct);

            try
            {
                await _auditLog.LogAsync(operatorId, "UpdatePermission", "Permission", permission.Id.ToString(),
                    new { permission.Code, OldEnabled = !enabled, NewEnabled = enabled });
            }
            catch
            {
                // 忽略审计异常
            }

            return new PermissionDto
            {
                Id = permission.Id,
                Code = permission.Code,
                Module = permission.Module,
                Resource = permission.Resource,
                Action = permission.Action,
                DisplayName = permission.DisplayName,
                Description = permission.Description,
                Enabled = permission.Enabled
            };
        }
    }
}
