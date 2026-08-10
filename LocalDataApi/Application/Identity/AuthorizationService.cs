using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Identity
{
    /// <summary>
    /// 权限校验服务。负责计算用户的有效权限编码集合(缓存优先)与权限判断。
    /// 权限链路: User → UserRole(有效) → Role(启用) → RolePermission → Permission(启用) → Code
    /// </summary>
    public sealed class AuthorizationService
    {
        private readonly AppDbContext _context;
        private readonly PermissionCache _cache;

        public AuthorizationService(AppDbContext context, PermissionCache cache)
        {
            _context = context;
            _cache = cache;
        }

        /// <summary>
        /// 获取用户有效权限编码集合(缓存优先;缓存未命中时查库重建并写入缓存)。
        /// </summary>
        public async Task<IReadOnlySet<string>> GetUserPermissionsAsync(string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return new HashSet<string>();

            if (_cache.TryGet(userId, out var cached) && cached != null)
                return cached;

            var codes = await (
                from ur in _context.UserRoles
                join r in _context.Roles on ur.RoleId equals r.Id
                join rp in _context.RolePermissions on r.Id equals rp.RoleId
                join p in _context.Permissions on rp.PermissionId equals p.Id
                where ur.UserId == userId
                      && ur.IsActive
                      && r.Enabled
                      && p.Enabled
                select p.Code).Distinct().ToListAsync(ct);

            var set = new HashSet<string>(codes, StringComparer.Ordinal);
            _cache.Set(userId, set);
            return set;
        }

        /// <summary>判断用户是否拥有指定权限(Fail Close:空权限声明拒绝访问)。</summary>
        public async Task<bool> HasPermissionAsync(string userId, string permissionCode, CancellationToken ct = default)
        {
            // Fail Close:权限声明错误(空/空白) → 拒绝,而非放行
            if (string.IsNullOrWhiteSpace(permissionCode))
                return false;
            var permissions = await GetUserPermissionsAsync(userId, ct);
            return permissions.Contains(permissionCode);
        }

        /// <summary>判断用户是否拥有任一权限(多个权限编码为 OR 关系;空声明 Fail Close)。</summary>
        public async Task<bool> HasAnyPermissionAsync(string userId, IEnumerable<string> permissionCodes, CancellationToken ct = default)
        {
            var codes = permissionCodes?.Where(c => !string.IsNullOrWhiteSpace(c)).ToArray();
            // Fail Close:未声明任何有效权限 → 拒绝
            if (codes == null || codes.Length == 0)
                return false;
            var permissions = await GetUserPermissionsAsync(userId, ct);
            return codes.Any(c => permissions.Contains(c));
        }

        /// <summary>
        /// 获取用户的角色编码与有效权限编码(登录返回与 me 接口使用)。
        /// </summary>
        public async Task<(List<string> Roles, List<string> Permissions)> GetUserRolesAndPermissionsAsync(string userId, CancellationToken ct = default)
        {
            var roles = await (
                from ur in _context.UserRoles
                join r in _context.Roles on ur.RoleId equals r.Id
                where ur.UserId == userId && ur.IsActive && r.Enabled
                select r.Code).Distinct().ToListAsync(ct);

            var permissions = (await GetUserPermissionsAsync(userId, ct)).ToList();
            return (roles, permissions);
        }
    }
}
