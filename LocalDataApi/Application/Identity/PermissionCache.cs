using Microsoft.Extensions.Caching.Memory;

namespace LocalDataApi.Application.Identity
{
    /// <summary>
    /// 用户权限缓存(单例)。缓存 UserId → 有效权限编码集合,
    /// 避免每次请求都访问数据库计算权限(性能验收要求)。
    /// 权限/角色变化时由服务层主动清除对应用户的缓存。
    /// </summary>
    public sealed class PermissionCache
    {
        private readonly IMemoryCache _cache;

        // 滑动过期:30 分钟内未被访问的缓存自动回收(兜底,防止长期不一致)
        private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(30);

        public PermissionCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        private static string Key(string userId) => $"rbac:permissions:{userId}";

        /// <summary>尝试读取用户权限缓存。</summary>
        public bool TryGet(string userId, out IReadOnlySet<string>? permissions)
        {
            permissions = null;
            if (string.IsNullOrWhiteSpace(userId))
                return false;
            return _cache.TryGetValue(Key(userId), out permissions);
        }

        /// <summary>写入用户权限缓存。</summary>
        public void Set(string userId, IReadOnlySet<string> permissions)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return;
            _cache.Set(Key(userId), permissions, new MemoryCacheEntryOptions
            {
                SlidingExpiration = SlidingExpiration
            });
        }

        /// <summary>清除单个用户权限缓存(用户角色变化时调用)。</summary>
        public void Remove(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return;
            _cache.Remove(Key(userId));
        }

        /// <summary>批量清除用户权限缓存(角色权限变化时调用)。</summary>
        public void RemoveMany(IEnumerable<string> userIds)
        {
            foreach (var userId in userIds)
            {
                Remove(userId);
            }
        }
    }
}
