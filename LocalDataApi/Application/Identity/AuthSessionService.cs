using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LocalDataApi.Application.Identity;

public sealed class AuthSessionService : IAuthSessionService
{
    private const string DefaultDevelopmentSecret = "LocalDataApi-Default-Dev-Secret-Change-Me";
    private readonly AppDbContext _context;
    private readonly AuthOptions _options;
    private readonly byte[] _signingKey;

    public AuthSessionService(AppDbContext context, IOptions<AuthOptions> options)
    {
        _context = context;
        _options = options.Value;
        var secret = string.IsNullOrWhiteSpace(_options.Secret) ? DefaultDevelopmentSecret : _options.Secret;
        _signingKey = Encoding.UTF8.GetBytes(secret);
    }

    public async Task<AuthSessionIssue> CreateAsync(User user, bool rememberMe, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var now = DateTimeOffset.UtcNow;
        var absolute = now.AddDays(rememberMe ? _options.RememberMeAbsoluteExpiryDays : _options.NormalAbsoluteExpiryDays);
        var idle = Min(now.Add(rememberMe ? TimeSpan.FromDays(_options.RememberMeIdleExpiryDays) : TimeSpan.FromHours(_options.NormalIdleExpiryHours)), absolute);
        var session = new AuthSession
        {
            Id = Guid.NewGuid(), UserId = user.Id ?? throw new InvalidOperationException("用户 ID 不能为空"),
            CreatedAtUtc = now, LastActivityAtUtc = now,
            IdleExpiresAtUtc = idle, AbsoluteExpiresAtUtc = absolute, RememberMe = rememberMe,
            IpAddress = Truncate(ipAddress, 128), UserAgent = Truncate(userAgent, 512)
        };
        _context.AuthSessions.Add(session);
        await _context.SaveChangesAsync(ct);
        return Issue(session, user.UserName ?? user.Id, now);
    }

    public async Task<AuthSessionValidationResult> ValidateAccessAsync(string userId, Guid sessionId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var result = await (from session in _context.AuthSessions.AsNoTracking()
                            join user in _context.用户管理.AsNoTracking() on session.UserId equals user.Id
                            where session.Id == sessionId && session.UserId == userId
                            select new { Session = session, user.IsActive }).FirstOrDefaultAsync(ct);
        if (result == null || result.Session.RevokedAtUtc.HasValue) return new(false, "AUTH_SESSION_REVOKED");
        if (result.IsActive != "true") return new(false, "AUTH_ACCOUNT_DISABLED");
        if (result.Session.AbsoluteExpiresAtUtc <= now) return new(false, "AUTH_SESSION_ABSOLUTE_EXPIRED");
        if (result.Session.IdleExpiresAtUtc <= now) return new(false, "AUTH_SESSION_IDLE_EXPIRED");
        return new(true);
    }

    public async Task RevokeAsync(Guid sessionId, string reason, CancellationToken ct = default)
    {
        var session = await _context.AuthSessions.AsTracking().FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session != null && !session.RevokedAtUtc.HasValue) await RevokeTrackedAsync(session, reason, DateTimeOffset.UtcNow, ct);
    }

    public async Task RevokeAllAsync(string userId, string reason, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var sessions = await _context.AuthSessions.AsTracking().Where(s => s.UserId == userId && !s.RevokedAtUtc.HasValue).ToListAsync(ct);
        foreach (var session in sessions) { session.RevokedAtUtc = now; session.RevokedReason = reason; }
        if (sessions.Count > 0) await _context.SaveChangesAsync(ct);
    }

    private async Task RevokeTrackedAsync(AuthSession session, string reason, DateTimeOffset now, CancellationToken ct)
    {
        session.RevokedAtUtc = now; session.RevokedReason = reason;
        await _context.SaveChangesAsync(ct);
    }

    private AuthSessionIssue Issue(AuthSession session, string userName, DateTimeOffset now)
    {
        var accessExpires = now.AddMinutes(_options.AccessTokenExpiryMinutes);
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, session.UserId), new Claim(ClaimTypes.NameIdentifier, session.UserId), new Claim(ClaimTypes.Name, userName), new Claim(JwtRegisteredClaimNames.Sid, session.Id.ToString("D")) };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(_signingKey), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(claims: claims, notBefore: now.UtcDateTime, expires: accessExpires.UtcDateTime, signingCredentials: credentials);
        return new(session.Id, new JwtSecurityTokenHandler().WriteToken(jwt));
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;
    private static string? Truncate(string? value, int maxLength) => string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, maxLength)];
}
