using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace LocalDataApi.Application.Identity;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor)
{
    public TokenPayload? Payload
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true ||
                !long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
                return null;

            return new TokenPayload
            {
                UserId = userId,
                UserName = principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                PermissionVersion = int.TryParse(principal.FindFirstValue("permission_version"), out var version) ? version : 0
            };
        }
    }

    public long? UserId => Payload?.UserId;
    public string? UserName => Payload?.UserName;
    public Guid? SessionId => Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sid), out var id) ? id : null;
}
