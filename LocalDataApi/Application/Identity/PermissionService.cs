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
        Task<List<PermissionDto>> GetPermissionsAsync(string? module = null, string? keyword = null, CancellationToken ct = default);

        /// <summary>按模块、资源、权限点三级结构查询权限树。</summary>
        Task<List<PermissionTreeNodeDto>> GetPermissionTreeAsync(string? keyword = null, CancellationToken ct = default);

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

        public async Task<List<PermissionDto>> GetPermissionsAsync(string? module = null, string? keyword = null, CancellationToken ct = default)
        {
            var query = _context.Permissions.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(module))
            {
                query = query.Where(p => p.Module == module);
            }
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var term = keyword.Trim();
                query = query.Where(p => p.Code.Contains(term) ||
                                         p.DisplayName.Contains(term) ||
                                         (p.Description != null && p.Description.Contains(term)) ||
                                         p.Module.Contains(term) ||
                                         p.Resource.Contains(term));
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
                Name = p.DisplayName,
                DisplayName = p.DisplayName,
                Description = p.Description,
                Enabled = p.Enabled
            }).ToList();
        }

        public async Task<List<PermissionTreeNodeDto>> GetPermissionTreeAsync(string? keyword = null, CancellationToken ct = default)
        {
            var permissions = await GetPermissionsAsync(keyword: keyword, ct: ct);

            return permissions
                .GroupBy(p => p.Module)
                .OrderBy(g => g.Key)
                .Select(module => new PermissionTreeNodeDto
                {
                    Key = $"module:{module.Key}",
                    Name = GetModuleName(module.Key),
                    NodeType = "module",
                    Module = module.Key,
                    Children = module
                        .GroupBy(p => p.Resource)
                        .OrderBy(g => g.Key)
                        .Select(resource => new PermissionTreeNodeDto
                        {
                            Key = $"resource:{module.Key}.{resource.Key}",
                            Name = GetResourceName(resource.Key),
                            NodeType = "resource",
                            Module = module.Key,
                            Resource = resource.Key,
                            Children = resource
                                .OrderBy(p => p.Action)
                                .Select(p => new PermissionTreeNodeDto
                                {
                                    Key = p.Id.ToString(),
                                    Name = p.DisplayName,
                                    NodeType = "permission",
                                    PermissionId = p.Id,
                                    Code = p.Code,
                                    Module = p.Module,
                                    Resource = p.Resource,
                                    Action = p.Action,
                                    Description = p.Description,
                                    Enabled = p.Enabled,
                                    Disabled = !p.Enabled
                                }).ToList()
                        }).ToList()
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
                // 先持久化权限状态,再清缓存,避免并发请求把旧权限重新写回缓存。
                await _context.SaveChangesAsync(ct);
                await _permissionCache.ClearPermissionCacheAsync(id, ct);
                await _context.SaveChangesAsync(ct);
            }
            else
            {
                await _context.SaveChangesAsync(ct);
            }

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
                Name = permission.DisplayName,
                DisplayName = permission.DisplayName,
                Description = permission.Description,
                Enabled = permission.Enabled
            };
        }

        private static string GetModuleName(string module) => module switch
        {
            "Identity" => "系统管理",
            "PMC" => "PMC 管理",
            "ERP" => "ERP 管理",
            "WeChatWork" => "企业微信",
            _ => module
        };

        private static string GetResourceName(string resource) => resource switch
        {
            "User" => "用户管理",
            "Role" => "角色管理",
            "Permission" => "权限管理",
            "Department" => "组织管理",
            "Schedule" => "排产管理",
            "WorkOrder" => "工单管理",
            "DeliveryReview" => "交期评审",
            "ExternalProduction" => "外产管理",
            "Material" => "物料管理",
            "Message" => "消息管理",
            "SmartSheet" => "智能表格",
            _ => resource
        };
    }
}
