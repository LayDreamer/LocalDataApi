using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LocalDataApi.Domain.Identity
{
    /// <summary>
    /// 系统用户（用于登录页及相关登录逻辑）
    /// </summary>
    public class User
    {
        // 主键
        public string? Id { get; set; }

        // 用户名（登录账号）
        public string? UserName { get; set; }

        // 密码哈希（不存储明文密码）
        public string? PasswordHash { get; set; }

        // 密码盐值（用于哈希加盐）
        public string? PasswordSalt { get; set; }

        // 邮箱
        public string? Email { get; set; }

        // 手机号
        public string? PhoneNumber { get; set; }

        // 显示名称
        public string? DisplayName { get; set; }

        // 角色（如 Admin / User 等）
        public string? Role { get; set; }

        // 账号是否启用（禁用后不可登录，存储 "true"/"false"）
        public string? IsActive { get; set; } = "true";

        // 连续登录失败次数（用于防爆破锁定，存储字符串数字）
        public string? LoginFailCount { get; set; }

        // 锁定截止时间（为空表示未锁定，存储 ISO8601 字符串）
        public string? LockoutEnd { get; set; }

        // 最后登录时间
        public string? LastLoginTime { get; set; }

        // 最后登录IP
        public string? LastLoginIp { get; set; }

        // 创建时间（存储 ISO8601 字符串）
        public string? CreateDate { get; set; }

        // 修改时间（存储 ISO8601 字符串）
        public string? ModifyDate { get; set; }

        // 企业微信 UserId（工作台免登绑定，用于识别企微身份对应的系统账号）
        public string? WeChatWorkUserId { get; set; }
    }
}
