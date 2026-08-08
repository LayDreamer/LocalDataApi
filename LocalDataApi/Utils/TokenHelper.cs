using System.Security.Cryptography;
using System.Text;

namespace LocalDataApi.Utils
{
    /// <summary>
    /// 令牌载荷(校验成功后的解析结果)。
    /// </summary>
    public sealed class TokenPayload
    {
        /// <summary>用户ID</summary>
        public string UserId { get; init; } = string.Empty;

        /// <summary>用户名</summary>
        public string UserName { get; init; } = string.Empty;

        /// <summary>签发时的权限版本号(用于权限缓存刷新判断)</summary>
        public int PermissionVersion { get; init; }
    }

    /// <summary>
    /// 简易令牌工具：基于 HMAC-SHA256 签名生成不透明令牌。
    /// 旧格式: Base64(用户名|签发Ticks|过期Ticks|签名) —— 4 段,兼容历史令牌。
    /// 新格式: Base64(用户ID|用户名|签发Ticks|过期Ticks|权限版本|签名) —— 6 段,支持 RBAC。
    /// </summary>
    public static class TokenHelper
    {
        /// <summary>
        /// 生成令牌(兼容旧调用): 用户名|签发Ticks|过期Ticks|签名。
        /// </summary>
        public static string CreateToken(string userName, string secret, int expiryMinutes = 1440)
        {
            // 兼容旧调用:用户ID缺省时回退为用户名
            return CreateToken(userName, userName, 0, secret, expiryMinutes);
        }

        /// <summary>
        /// 生成令牌(RBAC): 用户ID|用户名|签发Ticks|过期Ticks|权限版本|签名。
        /// </summary>
        /// <param name="userId">用户ID(写入令牌主体,权限校验依据)。</param>
        /// <param name="userName">用户名。</param>
        /// <param name="permissionVersion">签发时的权限版本号;权限变化后旧令牌凭版本号触发缓存刷新。</param>
        /// <param name="secret">签名密钥。</param>
        /// <param name="expiryMinutes">令牌有效期(分钟),默认 1440(1 天)。</param>
        public static string CreateToken(string userId, string userName, int permissionVersion, string secret, int expiryMinutes = 1440)
        {
            var issued = DateTime.UtcNow.Ticks;
            var expiry = DateTime.UtcNow.AddMinutes(expiryMinutes).Ticks;
            var data = $"{userId}|{userName}|{issued}|{expiry}|{permissionVersion}";
            var sig = Sign(data, secret);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{data}|{sig}"));
        }

        /// <summary>
        /// 校验令牌并取出用户名(兼容新旧两种格式);失败返回 false。
        /// </summary>
        public static bool TryValidate(string token, string secret, out string userName)
        {
            if (!TryValidateFull(token, secret, out var payload) || payload == null)
            {
                userName = string.Empty;
                return false;
            }
            userName = payload.UserName;
            return true;
        }

        /// <summary>
        /// 校验令牌并返回完整载荷(用户ID/用户名/权限版本)。
        /// 兼容: 旧格式(4 段)解析为用户ID=用户名、权限版本=0。
        /// 失败(格式错误 / 签名不符 / 已过期)返回 false。
        /// </summary>
        public static bool TryValidateFull(string token, string secret, out TokenPayload? payload)
        {
            payload = null;
            try
            {
                var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var parts = raw.Split('|');
                if (parts.Length != 4 && parts.Length != 6)
                    return false;

                // 统一数据段: 新格式(6段)为前 5 段,旧格式(4段)为前 3 段
                var dataParts = parts.Length == 6 ? parts[..5] : parts[..3];
                var data = string.Join('|', dataParts);
                var expected = Sign(data, secret);
                if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(expected),
                        Encoding.UTF8.GetBytes(parts[^1])))
                    return false;

                // 校验是否过期: 新格式 expiry 在索引 3,旧格式在索引 2
                var expiryIndex = parts.Length == 6 ? 3 : 2;
                if (!long.TryParse(parts[expiryIndex], out var expiryTicks) || expiryTicks <= DateTime.UtcNow.Ticks)
                    return false;

                if (parts.Length == 6)
                {
                    // 新格式: 用户ID|用户名|签发|过期|权限版本
                    var version = int.TryParse(parts[4], out var v) ? v : 0;
                    payload = new TokenPayload { UserId = parts[0], UserName = parts[1], PermissionVersion = version };
                }
                else
                {
                    // 旧格式: 用户名|签发|过期
                    payload = new TokenPayload { UserId = parts[0], UserName = parts[0], PermissionVersion = 0 };
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string Sign(string data, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
        }
    }
}
