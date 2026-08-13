namespace LocalDataApi.Application.Identity;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public string Secret { get; init; } = string.Empty;
    public int AccessTokenExpiryMinutes { get; init; } = 30;
    public int NormalIdleExpiryHours { get; init; } = 8;
    public int NormalAbsoluteExpiryDays { get; init; } = 7;
    public int RememberMeIdleExpiryDays { get; init; } = 7;
    public int RememberMeAbsoluteExpiryDays { get; init; } = 30;
}
