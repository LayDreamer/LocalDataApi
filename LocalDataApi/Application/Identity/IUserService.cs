using LocalDataApi.Dto;

namespace LocalDataApi.Application.Identity;

public interface IUserService
{
    /// <summary>
    /// 用户登录:校验账号密码,返回令牌与用户信息。
    /// </summary>
    Task<LoginResultDto> LoginAsync(LoginRequestDto request, string? ipAddress = null, string? userAgent = null);

    /// <summary>
    /// 注册新用户。
    /// </summary>
    Task<(bool Success, string Message)> RegisterAsync(RegisterUserDto dto);

    /// <summary>
    /// 修改密码(需校验原密码,用户名取自登录令牌)。
    /// </summary>
    Task<(bool Success, string Message)> ChangePasswordAsync(string userName, string oldPassword, string newPassword);

    /// <summary>
    /// 更新个人资料(显示名 / 邮箱 / 手机号)。
    /// </summary>
    Task<(bool Success, string Message)> UpdateProfileAsync(string userName, UpdateProfileDto dto);

    /// <summary>
    /// 管理员重置用户密码:生成随机临时密码并覆盖存储,清空登录失败计数与锁定,返回明文临时密码。
    /// 不强制用户改密(MustChangePassword=false),用户可在「修改密码」中随时自行修改。
    /// </summary>
    Task<(bool Success, string Message, string? TempPassword)> ResetPasswordAsync(string userId);

    /// <summary>
    /// 企业微信工作台免登:通过授权 code 换取企微身份并登录(账号不存在时自动建号)。
    /// </summary>
    Task<LoginResultDto> LoginByWeChatWorkAsync(string code, string? ipAddress = null, string? userAgent = null);
}
