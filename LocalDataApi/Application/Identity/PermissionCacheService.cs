using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Identity
{
    /// <summary>
    /// 权限缓存统一失效服务(scoped)。
    /// 封装"权限版本 +1 + 清除缓存"的原子操作,确保角色/权限/用户状态变化后权限实时生效,
    /// 无需等待缓存滑动过期。调用方负责最终 SaveChanges(与业务事务同批提交)。
    /// </summary>
    public interface IPermissionCacheService
    {
        /// <summary>清除单个用户权限缓存并提升其权限版本(用户角色变化/用户禁用/删除时调用)。</summary>
        Task ClearUserPermissionCacheAsync(string userId, CancellationToken ct = default);

        /// <summary>清除绑定某角色的所有用户权限缓存并提升权限版本(角色权限变化时调用)。</summary>
        Task ClearRolePermissionCacheAsync(Guid roleId, CancellationToken ct = default);

        /// <summary>清除绑定某权限的所有用户权限缓存并提升权限版本(权限点启用/停用时调用)。</summary>
        Task ClearPermissionCacheAsync(Guid permissionId, CancellationToken ct = default);
    }

    /// <summary>权限缓存统一失效服务实现。</summary>
    public sealed class PermissionCacheService : IPermissionCacheService
    {
        private readonly AppDbContext _context;
        private readonly PermissionCache _cache;

        public PermissionCacheService(AppDbContext context, PermissionCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task ClearUserPermissionCacheAsync(string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return;

            var user = await _context.用户管理.AsTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user != null)
            {
                user.PermissionVersion += 1;
                user.ModifyDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }

            _cache.Remove(userId);
        }

        public async Task ClearRolePermissionCacheAsync(Guid roleId, CancellationToken ct = default)
        {
            var userIds = await _context.UserRoles.AsNoTracking()
                .Where(ur => ur.RoleId == roleId && ur.IsActive)
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync(ct);

            await BumpVersionsAndClearAsync(userIds, ct);
        }

        public async Task ClearPermissionCacheAsync(Guid permissionId, CancellationToken ct = default)
        {
            var roleIds = await _context.RolePermissions.AsNoTracking()
                .Where(rp => rp.PermissionId == permissionId)
                .Select(rp => rp.RoleId)
                .Distinct()
                .ToListAsync(ct);
            if (roleIds.Count == 0)
                return;

            var userIds = await _context.UserRoles.AsNoTracking()
                .Where(ur => roleIds.Contains(ur.RoleId) && ur.IsActive)
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync(ct);

            await BumpVersionsAndClearAsync(userIds, ct);
        }

        /// <summary>批量提升用户权限版本并清除缓存(分页避免 IN 列表过大;修改由调用方统一提交)。</summary>
        private async Task BumpVersionsAndClearAsync(List<string> userIds, CancellationToken ct)
        {
            if (userIds.Count == 0)
                return;

            foreach (var batch in userIds.Chunk(100))
            {
                var users = await _context.用户管理.AsTracking()
                    .Where(u => batch.Contains(u.Id!))
                    .ToListAsync(ct);
                foreach (var user in users)
                {
                    user.PermissionVersion += 1;
                    user.ModifyDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }

            _cache.RemoveMany(userIds);
        }
    }
}
