using System.Security.Cryptography;
using System.Text;

namespace LocalDataApi.Utils
{
    /// <summary>
    /// 密码哈希工具：使用 PBKDF2（Rfc2898DeriveBytes）加盐存储，避免明文密码。
    /// </summary>
    public static class PasswordHelper
    {
        private const int SaltSize = 16;    // 128 bit 盐值
        private const int KeySize = 32;     // 256 bit 哈希
        private const int Iterations = 100_000;

        /// <summary>
        /// 根据明文密码生成哈希与盐值（均为 Base64 字符串）。
        /// </summary>
        public static void CreateHash(string password, out string hash, out string salt)
        {
            var saltBytes = new byte[SaltSize];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(saltBytes);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA256);
            var hashBytes = pbkdf2.GetBytes(KeySize);

            salt = Convert.ToBase64String(saltBytes);
            hash = Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// 校验明文密码是否与已存储的哈希/盐值匹配（常量时间比较）。
        /// </summary>
        public static bool Verify(string password, string hash, string salt)
        {
            try
            {
                var saltBytes = Convert.FromBase64String(salt);
                using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA256);
                var computed = pbkdf2.GetBytes(KeySize);

                var stored = Convert.FromBase64String(hash);
                if (stored.Length != computed.Length)
                    return false;

                int diff = 0;
                for (int i = 0; i < stored.Length; i++)
                    diff |= stored[i] ^ computed[i];
                return diff == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
