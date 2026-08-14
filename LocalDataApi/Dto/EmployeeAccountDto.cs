namespace LocalDataApi.Dto;

public sealed class BindEmployeeUserRequestDto
{
    public long UserIdentityId { get; set; }
}

public sealed class EmployeeAccountDto
{
    public long EmployeeId { get; set; }
    public bool IsBound { get; set; }
    public long? UserIdentityId { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? DisplayName { get; set; }
    public string? IsActive { get; set; }
}
