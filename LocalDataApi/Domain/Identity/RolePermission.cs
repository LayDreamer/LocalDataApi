namespace LocalDataApi.Domain.Identity
{
    /// <summary>
    /// 角色-权限关联(RoleId + PermissionId 唯一)。
    /// </summary>
    public class RolePermission
    {
        /// <summary>主键</summary>
        public Guid Id { get; set; }

        /// <summary>角色ID</summary>
        public Guid RoleId { get; set; }

        /// <summary>权限ID</summary>
        public Guid PermissionId { get; set; }

        /// <summary>创建时间</summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;
    }
}
