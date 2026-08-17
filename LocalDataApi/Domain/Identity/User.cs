namespace LocalDataApi.Domain.Identity;

/// <summary>平台认证账号。Id 是唯一运行时用户标识。</summary>
public sealed class User
{
    public long Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public byte Status { get; set; } = UserStatus.Active;
    public string? PasswordHash { get; set; }
    public string? PasswordSalt { get; set; }
    public string? PasswordAlgorithm { get; set; }
    public DateTime? PasswordUpdatedAtUtc { get; set; }
    public bool MustChangePassword { get; set; }
    public int LoginFailCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public string? LastLoginIp { get; set; }
    public int PermissionVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public bool IsActive => Status == UserStatus.Active;
}

public static class UserStatus
{
    public const byte Active = 1;
    public const byte Disabled = 2;
    public const byte Archived = 3;
}
