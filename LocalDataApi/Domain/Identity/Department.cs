namespace LocalDataApi.Domain.Identity
{
    /// <summary>
    /// 组织部门(支持树结构,软删除)。数据来源为企业微信组织架构同步。
    /// </summary>
    public class Department
    {
        /// <summary>主键</summary>
        public Guid Id { get; set; }

        /// <summary>企业微信部门ID(唯一)</summary>
        public string CorpDepartmentId { get; set; } = string.Empty;

        /// <summary>部门名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>父部门ID(根部门为 null)</summary>
        public Guid? ParentId { get; set; }

        /// <summary>部门路径(如 "/1/2/3",用于快速检索子树)</summary>
        public string? Path { get; set; }

        /// <summary>部门负责人(企业微信 UserId)</summary>
        public string? LeaderExternalUserId { get; set; }

        public long? LeaderUserId { get; set; }

        /// <summary>是否启用(企微删除后置为 false,禁止物理删除)</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>创建时间</summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>修改时间</summary>
        public DateTime ModifyTime { get; set; } = DateTime.Now;
    }
}
