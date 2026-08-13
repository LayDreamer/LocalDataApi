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
        /// <summary>主部门名称(RBAC)</summary>
        public string? PrimaryDepartmentName { get; set; }
        /// <summary>职位(RBAC)</summary>
        public string? Position { get; set; }
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

        // ========== RBAC 扩展(2026-08-08) ==========

        // 用户ID(前端直接使用,无需再从 User 中取)
        public string? UserId { get; set; }

        // 用户显示名
        public string? UserName { get; set; }

        // 角色编码列表(如 ["Viewer"])
        public List<string> Roles { get; set; } = new();

        // 有效权限编码列表(如 ["PMC.Schedule.View"])
        public List<string> Permissions { get; set; } = new();

        // 是否需要强制修改密码(种子 admin / 被重置后为真,前端据此跳转改密页)
        public bool MustChangePassword { get; set; }
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

    /// <summary>
    /// 企业微信工作台免登请求(携带网页授权 code)
    /// </summary>
    public class WeChatWorkLoginDto
    {
        // 企业微信 OAuth2 授权回调携带的 code
        public string Code { get; set; } = "";
    }

}
