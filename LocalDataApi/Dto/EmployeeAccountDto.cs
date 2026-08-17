namespace LocalDataApi.Dto;

public sealed class BindEmployeeUserRequestDto
{
    public long UserId { get; set; }
}

public sealed class EmployeeAccountDto
{
    public long EmployeeId { get; set; }
    public bool IsBound { get; set; }
    public long? UserId { get; set; }
    public string? UserName { get; set; }
    public string? DisplayName { get; set; }
    public bool? IsActive { get; set; }
}
