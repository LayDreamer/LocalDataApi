namespace LocalDataApi.Domain.Identity
{
    /// <summary>
    /// 用户-角色关联(UserId + RoleId 唯一)。用于替换旧的 User.Role 文本字段。
    /// </summary>
    public class UserRole
    {
        /// <summary>主键</summary>
        public Guid Id { get; set; }

        /// <summary>用户ID(对应 用户管理.Id)</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>角色ID</summary>
        public Guid RoleId { get; set; }

        /// <summary>分配时间</summary>
        public DateTime AssignedAt { get; set; } = DateTime.Now;

        /// <summary>分配操作人(用户ID)</summary>
        public string? AssignedBy { get; set; }

        /// <summary>是否有效(撤销后置为 false,保留审计痕迹)</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>撤销时间</summary>
        public DateTime? RevokedAt { get; set; }
    }
}
