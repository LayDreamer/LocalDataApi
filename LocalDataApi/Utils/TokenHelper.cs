using System.Security.Cryptography;
using System.Text;

namespace LocalDataApi.Utils
{
    /// <summary>
    /// 简易令牌工具：基于 HMAC-SHA256 对 "用户名|签发时间" 签名生成不透明令牌，
    /// 用于登录页登录态的签发与校验。
    /// </summary>
    public static class TokenHelper
    {
        /// <summary>
        /// 生成令牌：Base64(用户名|签发Ticks|签名)
        /// </summary>
        public static string CreateToken(string userName, string secret)
        {
            var issued = DateTime.UtcNow.Ticks;
            var data = $"{userName}|{issued}";
            var sig = Sign(data, secret);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{data}|{sig}"));
        }

        /// <summary>
        /// 校验令牌并取出用户名；失败返回 false。
        /// </summary>
        public static bool TryValidate(string token, string secret, out string userName)
        {
            userName = string.Empty;
            try
            {
                var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var parts = raw.Split('|');
                if (parts.Length != 3)
                    return false;

                var data = $"{parts[0]}|{parts[1]}";
                var expected = Sign(data, secret);
                if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(expected),
                        Encoding.UTF8.GetBytes(parts[2])))
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
