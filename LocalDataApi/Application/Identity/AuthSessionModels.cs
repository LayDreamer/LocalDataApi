namespace LocalDataApi.Application.Identity;

public sealed class TokenPayload
{
    public string UserId { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public int PermissionVersion { get; init; }
}

public sealed record AuthSessionIssue(
    Guid SessionId,
    string AccessToken);

public sealed record AuthSessionValidationResult(bool IsValid, string? ErrorCode = null);
