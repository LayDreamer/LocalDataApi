namespace LocalDataApi.Dto
{
    // ==================== 角色管理 ====================

    /// <summary>角色信息</summary>
    public class RoleDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Description { get; set; }
        public bool IsBuiltIn { get; set; }
        public bool IsSystem { get; set; }
        public bool Enabled { get; set; }
        public DateTime CreateTime { get; set; }
        /// <summary>该角色已绑定的权限ID列表(查询角色详情时返回)</summary>
        public List<Guid> PermissionIds { get; set; } = new();
    }

    /// <summary>创建角色请求</summary>
    public class RoleCreateDto
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>修改角色请求</summary>
    public class RoleUpdateDto
    {
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public bool? Enabled { get; set; }
    }

    /// <summary>为角色分配权限请求(覆盖式更新)</summary>
    public class AssignPermissionsRequestDto
    {
        public List<Guid> PermissionIds { get; set; } = new();
    }

    // ==================== 权限管理 ====================

    /// <summary>权限点信息</summary>
    public class PermissionDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Module { get; set; } = "";
        public string Resource { get; set; } = "";
        public string Action { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Description { get; set; }
        public bool Enabled { get; set; }
    }

    // ==================== 用户管理 ====================

    /// <summary>为用户分配角色请求(覆盖式更新)</summary>
    public class AssignRolesRequestDto
    {
        public List<Guid> RoleIds { get; set; } = new();
    }

    /// <summary>用户列表项</summary>
    public class UserListItemDto
    {
        public string? Id { get; set; }
        public string? UserName { get; set; }
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? IsActive { get; set; }
        public string? PrimaryDepartmentName { get; set; }
        public string? Position { get; set; }
        /// <summary>角色编码列表(如 ["Scheduler"])</summary>
        public List<string> Roles { get; set; } = new();
        public DateTime? CreateDate { get; set; }
    }

    /// <summary>用户详情</summary>
    public class UserDetailDto
    {
        public string? Id { get; set; }
        public string? UserName { get; set; }
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? IsActive { get; set; }
        public string? Position { get; set; }
        public bool IsLeader { get; set; }
        public Guid? PrimaryDepartmentId { get; set; }
        public string? PrimaryDepartmentName { get; set; }
        public string? WeChatWorkUserId { get; set; }
        public int PermissionVersion { get; set; }
        /// <summary>角色ID列表</summary>
        public List<Guid> RoleIds { get; set; } = new();
        /// <summary>角色编码列表</summary>
        public List<string> Roles { get; set; } = new();
        /// <summary>有效权限编码列表</summary>
        public List<string> Permissions { get; set; } = new();
    }

    /// <summary>用户分页查询请求</summary>
    public class UserQueryDto : PagedRequestDtoBase
    {
        /// <summary>关键字(匹配用户名/显示名)</summary>
        public string? Keyword { get; set; }
        /// <summary>按部门过滤</summary>
        public Guid? DepartmentId { get; set; }
    }

    // ==================== 部门管理 ====================

    /// <summary>部门信息</summary>
    public class DepartmentDto
    {
        public Guid Id { get; set; }
        public string CorpDepartmentId { get; set; } = "";
        public string Name { get; set; } = "";
        public Guid? ParentId { get; set; }
        public string? Path { get; set; }
        public string? LeaderUserId { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>部门树节点</summary>
    public class DepartmentTreeNodeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public Guid? ParentId { get; set; }
        public string? Path { get; set; }
        public bool IsActive { get; set; }
        public List<DepartmentTreeNodeDto> Children { get; set; } = new();
    }

    /// <summary>部门同步结果</summary>
    public class DepartmentSyncResultDto
    {
        public int Total { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Disabled { get; set; }
        public string? Message { get; set; }
    }

    // ==================== 当前用户 ====================

    /// <summary>当前用户信息(me 接口)</summary>
    public class MeResultDto
    {
        public string? Id { get; set; }
        public string? UserName { get; set; }
        public string? DisplayName { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
    }

    /// <summary>当前用户权限(me/permissions 接口)</summary>
    public class MePermissionsDto
    {
        public List<string> Roles { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
    }
}
