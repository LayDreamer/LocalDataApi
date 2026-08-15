namespace LocalDataApi.Domain.Identity;

public sealed class UserLegacyMap
{
    public string LegacyUserId { get; set; } = string.Empty;
    public long UserId { get; set; }
    public DateTime MigratedAtUtc { get; set; }
    public User User { get; set; } = null!;
}
