using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace LocalDataApi.Application.Identity
{
    /// <summary>
    /// 当前请求用户服务。从 Authorization 请求头解析令牌,提供当前用户ID/用户名。
    /// 未登录或令牌失效时返回 null(由调用方决定 401 处理)。
    /// </summary>
    public sealed class CurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>当前请求令牌载荷(未登录为 null)。</summary>
        public TokenPayload? Payload
        {
            get
            {
                var principal = _httpContextAccessor.HttpContext?.User;
                if (principal?.Identity?.IsAuthenticated != true)
                    return null;
                var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(userId))
                    return null;
                var permissionVersion = int.TryParse(principal.FindFirstValue("permission_version"), out var value) ? value : 0;
                return new TokenPayload
                {
                    UserId = userId,
                    UserName = principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                    PermissionVersion = permissionVersion
                };
            }
        }

        /// <summary>当前用户ID(未登录为 null)。</summary>
        public string? UserId => Payload?.UserId;

        /// <summary>当前用户名(未登录为 null)。</summary>
        public string? UserName => Payload?.UserName;

        public Guid? SessionId => Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sid), out var sessionId)
            ? sessionId
            : null;
    }
}
