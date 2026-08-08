using System.Security.Cryptography;
using System.Text;

namespace LocalDataApi.Utils
{
    /// <summary>
    /// 简易令牌工具：基于 HMAC-SHA256 对 "用户名|签发时间|过期时间" 签名生成不透明令牌，
    /// 用于登录页登录态的签发与校验。
    /// </summary>
    public static class TokenHelper
    {
        /// <summary>
        /// 生成令牌：Base64(用户名|签发Ticks|过期Ticks|签名)
        /// </summary>
        /// <param name="userName">用户名（写入令牌主体）。</param>
        /// <param name="secret">签名密钥。</param>
        /// <param name="expiryMinutes">令牌有效期（分钟），默认 1440（1 天）。</param>
        public static string CreateToken(string userName, string secret, int expiryMinutes = 1440)
        {
            var issued = DateTime.UtcNow.Ticks;
            var expiry = DateTime.UtcNow.AddMinutes(expiryMinutes).Ticks;
            var data = $"{userName}|{issued}|{expiry}";
            var sig = Sign(data, secret);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{data}|{sig}"));
        }

        /// <summary>
        /// 校验令牌并取出用户名；失败（格式错误 / 签名不符 / 已过期）返回 false。
        /// </summary>
        /// <param name="token">待校验令牌。</param>
        /// <param name="secret">签名密钥。</param>
        /// <param name="userName">校验成功时输出用户名。</param>
        public static bool TryValidate(string token, string secret, out string userName)
        {
            userName = string.Empty;
            try
            {
                var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var parts = raw.Split('|');
                if (parts.Length != 4)
                    return false;

                var data = $"{parts[0]}|{parts[1]}|{parts[2]}";
                var expected = Sign(data, secret);
                if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(expected),
                        Encoding.UTF8.GetBytes(parts[3])))
                    return false;

                // 校验是否过期
                if (!long.TryParse(parts[2], out var expiryTicks) || expiryTicks <= DateTime.UtcNow.Ticks)
                    return false;

                userName = parts[0];
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
