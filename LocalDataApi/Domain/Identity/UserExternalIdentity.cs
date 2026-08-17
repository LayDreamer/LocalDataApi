namespace LocalDataApi.Domain.Identity;

public sealed class UserExternalIdentity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ExternalSubject { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}
