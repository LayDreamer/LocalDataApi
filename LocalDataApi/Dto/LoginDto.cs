namespace LocalDataApi.Dto
{
    /// <summary>
    /// 登录请求（登录页表单提交内容）
    /// </summary>
    public class LoginRequestDto
    {
        // 用户名（登录账号）
        public string? UserName { get; set; }

        // 密码（明文，仅用于校验，不入库）
        public string? Password { get; set; }

        // 记住我（可选，用于延长登录态）
        public bool RememberMe { get; set; }
    }

    /// <summary>
    /// 注册请求
    /// </summary>
    public class RegisterUserDto
    {
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Role { get; set; }
    }

    /// <summary>
    /// 登录成功后返回的用户信息（不含敏感字段）
    /// </summary>
    public class UserInfoDto
    {
        public string? Id { get; set; }
        public string? UserName { get; set; }
        public string? DisplayName { get; set; }
        public string? Role { get; set; }
        public string? Email { get; set; }
    }

    /// <summary>
    /// 登录结果
    /// </summary>
    public class LoginResultDto
    {
        // 是否登录成功
        public bool Success { get; set; }

        // 提示信息（失败原因 / 成功提示）
        public string? Message { get; set; }

        // 登录令牌（前端后续请求携带）
        public string? Token { get; set; }

        // 登录用户信息
        public UserInfoDto? User { get; set; }
    }

    /// <summary>
    /// 修改密码请求（身份从登录令牌获取）
    /// </summary>
    public class ChangePasswordDto
    {
        // 原密码
        public string? OldPassword { get; set; }

        // 新密码
        public string? NewPassword { get; set; }
    }

    /// <summary>
    /// 更新个人资料请求（显示名 / 邮箱 / 手机号）
    /// </summary>
    public class UpdateProfileDto
    {
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
    }

    /// <summary>
    /// ERP 用户登录校验请求（tb_control_user）
    /// </summary>
    public class ERPUserLoginDto
    {
        public string? Username { get; set; }
        public string? Upwd { get; set; }
    }
}
