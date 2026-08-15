using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LocalDataApi.Application.Identity;

public sealed class AuthSessionService(AppDbContext context, IOptions<AuthOptions> options) : IAuthSessionService
{
    private readonly AuthOptions _options = options.Value;
    private readonly byte[] _signingKey = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(options.Value.Secret)
        ? "LocalDataApi-Default-Dev-Secret-Change-Me" : options.Value.Secret);

    public async Task<AuthSessionIssue> CreateAsync(User user, bool rememberMe, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var absolute = now.AddDays(rememberMe ? _options.RememberMeAbsoluteExpiryDays : _options.NormalAbsoluteExpiryDays);
        var idle = now.Add(rememberMe ? TimeSpan.FromDays(_options.RememberMeIdleExpiryDays) : TimeSpan.FromHours(_options.NormalIdleExpiryHours));
        var session = new AuthSession
        {
            Id = Guid.NewGuid(), UserId = user.Id, CreatedAtUtc = now, LastActivityAtUtc = now,
            IdleExpiresAtUtc = idle <= absolute ? idle : absolute, AbsoluteExpiresAtUtc = absolute,
            RememberMe = rememberMe, IpAddress = Truncate(ipAddress, 128), UserAgent = Truncate(userAgent, 512)
        };
        context.AuthSessions.Add(session);
        await context.SaveChangesAsync(ct);
        return Issue(session, user.UserName, now);
    }

    public async Task<AuthSessionValidationResult> ValidateAccessAsync(long userId, Guid sessionId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var result = await (from session in context.AuthSessions.AsNoTracking()
                            join user in context.Users.AsNoTracking() on session.UserId equals user.Id
                            where session.Id == sessionId && session.UserId == userId
                            select new { Session = session, user.Status }).FirstOrDefaultAsync(ct);
        if (result is null || result.Session.RevokedAtUtc.HasValue) return new(false, "AUTH_SESSION_REVOKED");
        if (result.Status != UserStatus.Active) return new(false, "AUTH_ACCOUNT_DISABLED");
        if (result.Session.AbsoluteExpiresAtUtc <= now) return new(false, "AUTH_SESSION_ABSOLUTE_EXPIRED");
        if (result.Session.IdleExpiresAtUtc <= now) return new(false, "AUTH_SESSION_IDLE_EXPIRED");
        return new(true);
    }

    public async Task RevokeAsync(Guid sessionId, string reason, CancellationToken ct = default)
    {
        var session = await context.AuthSessions.FirstOrDefaultAsync(item => item.Id == sessionId, ct);
        if (session is null || session.RevokedAtUtc.HasValue) return;
        session.RevokedAtUtc = DateTimeOffset.UtcNow;
        session.RevokedReason = reason;
        await context.SaveChangesAsync(ct);
    }

    public async Task RevokeAllAsync(long userId, string reason, CancellationToken ct = default)
    {
        var sessions = await context.AuthSessions.Where(item => item.UserId == userId && !item.RevokedAtUtc.HasValue).ToListAsync(ct);
        foreach (var session in sessions) { session.RevokedAtUtc = DateTimeOffset.UtcNow; session.RevokedReason = reason; }
        if (sessions.Count > 0) await context.SaveChangesAsync(ct);
    }

    private AuthSessionIssue Issue(AuthSession session, string userName, DateTimeOffset now)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, session.UserId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new Claim(ClaimTypes.Name, userName),
            new Claim(JwtRegisteredClaimNames.Sid, session.Id.ToString("D"))
        };
        var token = new JwtSecurityToken(claims: claims, notBefore: now.UtcDateTime,
            expires: now.AddMinutes(_options.AccessTokenExpiryMinutes).UtcDateTime,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(_signingKey), SecurityAlgorithms.HmacSha256));
        return new(session.Id, new JwtSecurityTokenHandler().WriteToken(token));
    }

    private static string? Truncate(string? value, int length) => string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, length)];
}
