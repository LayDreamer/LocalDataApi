namespace LocalDataApi.Dto;

/// <summary>
/// ERP 用户校验响应(不含密码字段)。
/// </summary>
public class ERPUserDto
{
    public string? ID { get; set; }
    public string? username { get; set; }
    public string? usercode { get; set; }
}
