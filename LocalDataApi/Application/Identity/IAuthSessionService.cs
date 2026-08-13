using LocalDataApi.Domain.Identity;

namespace LocalDataApi.Application.Identity;

public interface IAuthSessionService
{
    Task<AuthSessionIssue> CreateAsync(User user, bool rememberMe, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<AuthSessionValidationResult> ValidateAccessAsync(string userId, Guid sessionId, CancellationToken ct = default);
    Task RevokeAsync(Guid sessionId, string reason, CancellationToken ct = default);
    Task RevokeAllAsync(string userId, string reason, CancellationToken ct = default);
}
