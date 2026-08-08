namespace LocalDataApi.Domain.Identity
{
    /// <summary>
    /// 功能权限点。编码格式: 模块.资源.动作(如 PMC.Schedule.Update)。
    /// 禁用采用 Enabled=false,禁止物理删除。
    /// </summary>
    public class Permission
    {
        /// <summary>主键</summary>
        public Guid Id { get; set; }

        /// <summary>权限编码(唯一,如 PMC.Schedule.Update)</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>业务模块(如 PMC / Identity / ERP / WeChatWork)</summary>
        public string Module { get; set; } = string.Empty;

        /// <summary>业务资源(如 Schedule / User)</summary>
        public string Resource { get; set; } = string.Empty;

        /// <summary>操作动作(如 View / Create / Update)</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>显示名称</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>说明</summary>
        public string? Description { get; set; }

        /// <summary>是否启用(停用后不影响存量数据,仅不再参与新的授权)</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>创建时间</summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>修改时间</summary>
        public DateTime ModifyTime { get; set; } = DateTime.Now;
    }
}
