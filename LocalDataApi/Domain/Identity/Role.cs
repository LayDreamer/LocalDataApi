namespace LocalDataApi.Domain.Identity
{
    /// <summary>
    /// 系统角色(Code 唯一;IsSystem 系统角色禁止删除)。
    /// </summary>
    public class Role
    {
        /// <summary>主键</summary>
        public Guid Id { get; set; }

        /// <summary>角色编码(唯一,如 ADMIN / SCHEDULER)</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>角色名称(唯一,如 系统管理员)</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>显示名称</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>描述</summary>
        public string? Description { get; set; }

        /// <summary>是否内置角色(内置角色不参与通用删除流程)</summary>
        public bool IsBuiltIn { get; set; }

        /// <summary>是否系统角色(系统角色禁止删除/禁用/清空权限)</summary>
        public bool IsSystem { get; set; }

        /// <summary>是否启用</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>创建时间</summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>修改时间</summary>
        public DateTime ModifyTime { get; set; } = DateTime.Now;
    }
}
