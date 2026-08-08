using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Identity
{
    /// <summary>
    /// 权限点查询服务(权限字典)。
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>查询全部权限点(可按模块过滤)。</summary>
        Task<List<PermissionDto>> GetPermissionsAsync(string? module = null, CancellationToken ct = default);
    }

    /// <summary>权限点查询服务实现。</summary>
    public sealed class PermissionService : IPermissionService
    {
        private readonly AppDbContext _context;

        public PermissionService(AppDbContext context)
        {
            _context = context;
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
    }
}
