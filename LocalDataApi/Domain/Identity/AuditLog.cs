namespace LocalDataApi.Domain.Identity
{
    /// <summary>
    /// 审计日志。记录用户角色变化、权限变化、部门同步等敏感操作。
    /// </summary>
    public class AuditLog
    {
        /// <summary>主键</summary>
        public Guid Id { get; set; }

        /// <summary>操作人用户ID(可为空,如系统自动同步)</summary>
        public string? UserId { get; set; }

        /// <summary>行为标识(如 AssignUserRole / AssignRolePermission / SyncDepartment)</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>对象类型(如 User / Role / Permission / Department)</summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>对象ID</summary>
        public string? TargetId { get; set; }

        /// <summary>变更内容(JSON 文本)</summary>
        public string? Content { get; set; }

        /// <summary>发生时间</summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;
    }
}
