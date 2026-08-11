using LocalDataApi.Utils;
using Microsoft.AspNetCore.Http;

namespace LocalDataApi.Application.Identity
{
    /// <summary>
    /// 当前请求用户服务。从 Authorization 请求头解析令牌,提供当前用户ID/用户名。
    /// 未登录或令牌失效时返回 null(由调用方决定 401 处理)。
    /// </summary>
    public sealed class CurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _tokenSecret;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            // 注意: IConfiguration[index] 在值为空字符串时返回 "" 而非 null,故用 IsNullOrWhiteSpace 判断回退,
            // 必须与 UserService 构造函数的取值逻辑保持一致(否则签名密钥不一致导致所有令牌校验失败 → 401)。
            var secret = configuration["Auth:Secret"];
            _tokenSecret = string.IsNullOrWhiteSpace(secret) ? "LocalDataApi-Default-Dev-Secret-Change-Me" : secret;
        }

        /// <summary>当前请求令牌载荷(未登录为 null)。</summary>
        public TokenPayload? Payload
        {
            get
            {
                var auth = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
                if (string.IsNullOrWhiteSpace(auth))
                    return null;

                var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? auth[7..]
                    : auth;

                return TokenHelper.TryValidateFull(token, _tokenSecret, out var payload) ? payload : null;
            }
        }

        /// <summary>当前用户ID(未登录为 null)。</summary>
        public string? UserId => Payload?.UserId;

        /// <summary>当前用户名(未登录为 null)。</summary>
        public string? UserName => Payload?.UserName;
    }
}
