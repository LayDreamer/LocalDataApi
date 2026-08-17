using System.Text.RegularExpressions;

namespace LocalDataApi.Utils
{
    /// <summary>
    /// 密码强度校验(生产安全基线)。
    /// 规则: 长度 8~64;至少包含大写字母、小写字母、数字中的三类(满足两类 + 特殊字符亦可)。
    /// 拒绝常见弱密码(与用户名/显示名相同、连续/重复字符等)。
    /// </summary>
    public static partial class PasswordValidation
    {
        // 常见弱密码黑名单(小写比较)
        private static readonly HashSet<string> CommonWeak = new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "123456", "12345678", "qwerty", "abc123", "admin",
            "welcome", "iloveyou", "111111", "000000", "passw0rd", "qazwsx"
        };

        [GeneratedRegex(@"[a-z]")]
        private static partial Regex Lower();

        [GeneratedRegex(@"[A-Z]")]
        private static partial Regex Upper();

        [GeneratedRegex(@"\d")]
        private static partial Regex Digit();

        [GeneratedRegex(@"[^a-zA-Z0-9]")]
        private static partial Regex Special();

        /// <summary>
        /// 校验密码强度。返回 (是否通过, 错误说明)。
        /// </summary>
        /// <param name="password">明文密码。</param>
        /// <param name="context">可选上下文(用户名/显示名),用于拒绝"密码等于账号"。</param>
        public static (bool Valid, string Error) Validate(string? password, string? context = null)
        {
            if (string.IsNullOrWhiteSpace(password))
                return (false, "密码不能为空");

            if (password.Length < 8 || password.Length > 64)
                return (false, "密码长度必须为 8~64 位");

            // 类别计数
            var categories = 0;
            if (Lower().IsMatch(password)) categories++;
            if (Upper().IsMatch(password)) categories++;
            if (Digit().IsMatch(password)) categories++;
            if (Special().IsMatch(password)) categories++;
            // 要求至少满足 3 类字符组合
            if (categories < 3)
                return (false, "密码至少包含大写字母、小写字母、数字、特殊字符中的 3 类");

            if (CommonWeak.Contains(password))
                return (false, "该密码过于常见,请更换");

            // 拒绝与账号相同
            if (!string.IsNullOrWhiteSpace(context) &&
                password.Equals(context, StringComparison.OrdinalIgnoreCase))
                return (false, "密码不能与账号名相同");

            // 拒绝连续 4 位相同或递增/递减序列
            if (HasRepeatedOrSequential(password, 4))
                return (false, "密码不能包含连续或重复的简单序列");

            return (true, string.Empty);
        }

        private static bool HasRepeatedOrSequential(string password, int runLength)
        {
            if (password.Length < runLength) return false;
            for (int i = 0; i + runLength <= password.Length; i++)
            {
                var window = password.Substring(i, runLength);
                // 全相同: aaaa
                bool allSame = true;
                // 递增: abcd; 递减: dcba
                bool ascending = true;
                bool descending = true;
                for (int j = 1; j < window.Length; j++)
                {
                    var prev = window[j - 1];
                    var cur = window[j];
                    if (prev != cur) allSame = false;
                    if (cur != prev + 1) ascending = false;
                    if (cur != prev - 1) descending = false;
                }
                if (allSame || ascending || descending) return true;
            }
            return false;
        }
    }
}
